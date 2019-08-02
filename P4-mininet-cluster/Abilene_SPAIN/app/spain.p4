/* -*- P4_16 -*- */
#include <core.p4>
#include <v1model.p4>

/*************************************************************************
*********************** H E A D E R S  ***********************************
*************************************************************************/

header ethernet_t {
    bit<48> dstAddr;
    bit<48> srcAddr;
    bit<16> etherType;
}

header ppp_t {
    bit<16> pppType;
}

const bit<16> TYPE_IPV4             = 0x0800;
const bit<16> TYPE_VLAN_TAGGED_IPV4 = 0x0050;

header ipv4_t {
    bit<4>  version;
    bit<4>  ihl;
    bit<8>  diffserv;
    bit<16> totalLen;
    bit<16> identification;
    bit<3>  flags;
    bit<13> fragOffset;
    bit<8>  ttl;
    bit<8>  protocol;
    bit<16> hdrChecksum;
    bit<32> srcAddr;
    bit<32> dstAddr;
}

header tcp_t {
    bit<16> srcPort;
    bit<16> dstPort;
    bit<32> seqNo;
    bit<32> ackNo;
    bit<4>  dataOffset;
    bit<3>  res;
    bit<3>  ecn;
    bit<6>  ctrl;
    bit<16> window;
    bit<16> checksum;
    bit<16> urgentPtr;
}

const bit<16> ARP_HTYPE_ETHERNET = 0x0001;
const bit<16> ARP_PTYPE_IPV4     = 0x0800;

const bit<8>  ARP_HLEN_ETHERNET  = 6;
const bit<8>  ARP_PLEN_IPV4      = 4;
const bit<16> ARP_OPER_REQUEST   = 1;
const bit<16> ARP_OPER_REPLY     = 2;

header arp_t {
    bit<16> htype;
    bit<16> ptype;
    bit<8>  hlen;
    bit<8>  plen;
    bit<16> oper;
}

header arp_ipv4_t {
    bit<48> sha;
    bit<32> spa;
    bit<48> tha;
    bit<32> tpa;
}

struct metadata {
    bit<16> vlanid;
}

struct headers {
    ppp_t      ppp;
    ethernet_t ethernet;
    arp_t      arp;
    arp_ipv4_t arp_ipv4;
    ipv4_t     ipv4;
    tcp_t      tcp;
}

/*************************************************************************
*********************** P A R S E R  ***********************************
*************************************************************************/

parser MyParser(packet_in packet,
                out headers hdr,
                inout metadata meta,
                inout standard_metadata_t standard_metadata) {
    
    state start {
        transition parse_ethernet;
//        transition parse_ppp;
    }
    state parse_ethernet {
        packet.extract(hdr.ethernet);
        transition select(hdr.ethernet.etherType) {
            0x800  : parse_ipv4;
            0x0806 : parse_arp;
            TYPE_VLAN_TAGGED_IPV4: parse_ipv4;
            default: accept;
        }
    }
    state parse_ppp {
        packet.extract(hdr.ppp);
        transition select(hdr.ppp.pppType) {
            TYPE_IPV4:             parse_ipv4;
            TYPE_VLAN_TAGGED_IPV4: parse_ipv4;
        }
    }
    state parse_ipv4 {
        packet.extract(hdr.ipv4);
        meta.vlanid      = hdr.ipv4.identification;
        transition select(hdr.ipv4.protocol) {
            6: parse_tcp;
            default: accept;
        }
    }
    state parse_tcp {
        packet.extract(hdr.tcp);
        transition accept;
    }
    state parse_arp {
        packet.extract(hdr.arp);
        transition select(hdr.arp.htype, hdr.arp.ptype,
                          hdr.arp.hlen,  hdr.arp.plen) {
            (ARP_HTYPE_ETHERNET, ARP_PTYPE_IPV4,
             ARP_HLEN_ETHERNET,  ARP_PLEN_IPV4) : parse_arp_ipv4;
            default : accept;
        }
    }
    state parse_arp_ipv4 {
        packet.extract(hdr.arp_ipv4);
        transition accept;
    }
}

/*************************************************************************
************   C H E C K S U M    V E R I F I C A T I O N   *************
*************************************************************************/

control MyVerifyChecksum(inout headers hdr, inout metadata meta) {
    apply { }
}

/*************************************************************************
**************  I N G R E S S   P R O C E S S I N G   *******************
*************************************************************************/

control MyIngress(inout headers hdr,
                  inout metadata meta,
                  inout standard_metadata_t standard_metadata) {
    action drop() {
        mark_to_drop();
    }


    // @ source: Match on IP to determine VLAN
    // srcIP = thisSwitch => set base and max based on (src,dst)
    // table_add tab_vlan_assign random_vlan [vlan=-1/pppType=0x0021] [src_ip] [dst_ip] => [base] [max] [priority=100]
    action random_vlan(bit<16> base, bit<16> count) {
        hash(meta.vlanid,
	    HashAlgorithm.crc32,
	    base,
	    { hdr.ipv4.srcAddr,
	      hdr.ipv4.dstAddr,
              hdr.ipv4.protocol,
              hdr.tcp.srcPort,
              hdr.tcp.dstPort },
	    count);
        //hdr.ppp.pppType = TYPE_VLAN_TAGGED_IPV4;
        hdr.ethernet.etherType = TYPE_VLAN_TAGGED_IPV4;
        hdr.ipv4.identification = meta.vlanid;
    }
    table tab_vlan_assign {
        key = {
            //hdr.ppp.pppType    : exact;
            hdr.ethernet.etherType : exact;
            hdr.ipv4.isValid() : exact;
            hdr.ipv4.srcAddr   : ternary;
            hdr.ipv4.dstAddr   : ternary;
        }
        actions = {
            random_vlan;
            NoAction;
        }
        default_action = NoAction();
    }

    // @ on path: match on VLAN to determine port to next hop
    // dstIP = *, VLAN = n => egressPort = result(n, thisSwitch)
    // table_add tab_vlan_nhop set_nhop [dstIP=0&&&0] [vlan] => [nhop] [priority = 100]
    // @ destination: match on dest IP to determine outgoing port
    // dstIP = dstHost, VLAN = n => egressPort = result(thisSwitch, dstHost)
    // table_add tab_vlan_nhop set_nhop [dstIP] [vlan=0&&&0] => [nhop] [priority=10]
    action set_nhop_untag_vlan(bit<9> port) {
        standard_metadata.egress_spec = port;
        //hdr.ppp.pppType = TYPE_IPV4;
        hdr.ethernet.etherType = TYPE_IPV4;
        if (hdr.ipv4.isValid())
            hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
    }
    action set_nhop(bit<9> port) {
        standard_metadata.egress_spec = port;
        if (hdr.ipv4.isValid())
            hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
    }
    table tab_vlan_nhop {
        key = {
            //hdr.ppp.pppType  : exact;
            hdr.ethernet.etherType  : exact;
            hdr.ipv4.dstAddr : ternary;
            meta.vlanid      : ternary;
        }
        actions = {
            set_nhop_untag_vlan;
            set_nhop;
            drop;
        }
        default_action = drop();
        size = 4096;
    }

    table tab_observe {
        key = {
            //hdr.ppp.pppType                : ternary;
            hdr.ethernet.etherType       : ternary;
            standard_metadata.ingress_port : ternary;
            standard_metadata.egress_spec  : ternary;
	    hdr.ipv4.srcAddr  : ternary;
	    hdr.ipv4.dstAddr  : ternary;
            hdr.ipv4.protocol : ternary;
            hdr.tcp.srcPort   : ternary;
            hdr.tcp.dstPort   : ternary;
            meta.vlanid : ternary;
        }
        actions = {
            NoAction;
        }
    }
    apply {
        if ((hdr.ipv4.isValid() && hdr.ipv4.ttl > 0) || (hdr.arp_ipv4.isValid())) {
            tab_vlan_assign.apply();
            tab_vlan_nhop.apply();
        }
        tab_observe.apply();
    }
}

/*************************************************************************
****************  E G R E S S   P R O C E S S I N G   *******************
*************************************************************************/

control MyEgress(inout headers hdr,
                 inout metadata meta,
                 inout standard_metadata_t standard_metadata) {
    
    apply {
    }
}

/*************************************************************************
*************   C H E C K S U M    C O M P U T A T I O N   **************
*************************************************************************/

control MyComputeChecksum(inout headers hdr, inout metadata meta) {
     apply {
	update_checksum(
	    hdr.ipv4.isValid(),
            { hdr.ipv4.version,
	      hdr.ipv4.ihl,
              hdr.ipv4.diffserv,
              hdr.ipv4.totalLen,
              hdr.ipv4.identification,
              hdr.ipv4.flags,
              hdr.ipv4.fragOffset,
              hdr.ipv4.ttl,
              hdr.ipv4.protocol,
              hdr.ipv4.srcAddr,
              hdr.ipv4.dstAddr },
            hdr.ipv4.hdrChecksum,
            HashAlgorithm.csum16);
    }
}

/*************************************************************************
***********************  D E P A R S E R  *******************************
*************************************************************************/

control MyDeparser(packet_out packet, in headers hdr) {
    apply {
        packet.emit(hdr.ppp);
        packet.emit(hdr.ethernet);
        packet.emit(hdr.arp);
        packet.emit(hdr.arp_ipv4);
        packet.emit(hdr.ipv4);
        packet.emit(hdr.tcp);
    }
}

/*************************************************************************
***********************  S W I T C H  *******************************
*************************************************************************/

V1Switch(
MyParser(),
MyVerifyChecksum(),
MyIngress(),
MyEgress(),
MyComputeChecksum(),
MyDeparser()
) main;
