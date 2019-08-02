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
    bit<14> ecmp_select;
    bit<32> dstIpv4Addr;
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
            default: accept;
        }
    }
    state parse_ppp {
        packet.extract(hdr.ppp);
        transition select(hdr.ppp.pppType) {
            0x0021: parse_ipv4;
        }
    }
    state parse_ipv4 {
        packet.extract(hdr.ipv4);
        meta.dstIpv4Addr = hdr.ipv4.dstAddr;
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
        meta.dstIpv4Addr = hdr.arp_ipv4.tpa;
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
    register<bit<16>>(1) currentid;

    action drop() {
        mark_to_drop();
    }
    action set_ecmp_select(bit<16> ecmp_base, bit<32> ecmp_count) {
        hash(meta.ecmp_select,
	    HashAlgorithm.crc32,
	    ecmp_base,
	    { hdr.ipv4.srcAddr,
	      hdr.ipv4.dstAddr,
              hdr.ipv4.protocol,
              hdr.tcp.srcPort,
              hdr.tcp.dstPort },
	    ecmp_count);
    }
    action set_nhop(bit<9> port) {
        standard_metadata.egress_spec = port;
		if (hdr.ipv4.isValid())
            hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
    }
    table ecmp_group {
        key = {
            meta.dstIpv4Addr: lpm;
        }
        actions = {
            drop;
            set_ecmp_select;
        }
        size = 1024;
    }
    table ecmp_nhop {
        key = {
            meta.ecmp_select: exact;
            meta.dstIpv4Addr: lpm;
        }
        actions = {
            drop;
            set_nhop;
        }
        size = 1024;
    }
    action set_identification() {
        currentid.read(hdr.ipv4.identification, 0);
        currentid.write(0, hdr.ipv4.identification+1);
    }
    table tab_set_identification {
        key = {
            hdr.ipv4.isValid(): exact;
            standard_metadata.ingress_port: exact;
        }
        actions = {
            NoAction;
            set_identification;
        }
        default_action = NoAction();
    }

    table tab_observe {
        key = {
            hdr.ppp.pppType                : ternary;
            standard_metadata.ingress_port : ternary;
            standard_metadata.egress_spec  : ternary;
	    hdr.ipv4.srcAddr  : ternary;
	    hdr.ipv4.dstAddr  : ternary;
            hdr.ipv4.protocol : ternary;
            hdr.tcp.srcPort   : ternary;
            hdr.tcp.dstPort   : ternary;
            meta.ecmp_select : ternary;
        }
        actions = {
            NoAction;
        }
    }
    apply {
        if ((hdr.ipv4.isValid() && hdr.ipv4.ttl > 0) || (hdr.arp_ipv4.isValid())) {
            ecmp_group.apply(); // get ecmp_select: 0 or 1
            ecmp_nhop.apply(); // map from (ecmp_select, dst) to port
            tab_set_identification.apply();
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
