/* -*- P4_16 -*- */
#include <core.p4>
#include <v1model.p4>

const bit<8>  HULAPP_PROTOCOL = 254; 
const bit<8>  HULAPP_BACKGROUND_PROTOCOL = 252;
const bit<8>  TCP_PROTOCOL = 6;
const bit<8>  UDP_PROTOCOL = 17;
const bit<16> TYPE_IPV4 = 0x0800;
const bit<16> TYPE_ARP  = 0x0806;
const bit<48> FLOWLET_TIMEOUT = 50000;
const bit<48> LINK_TIMEOUT = 800000;
//#define TAU_EXPONENT 9 // twice the probe frequency. if probe freq = 256 microsec, the TAU should be 512 microsec, and the TAU_EXPONENT would be 9
#define TAU_EXPONENT 19 // twice the probe frequency. if probe freq = 256 millisec, the TAU should be 512 millisec, and the TAU_EXPONENT would be 19
//#define TAU_EXPONENT 20 // twice the probe frequency. if probe freq = 512 millisec, the TAU should be 1024 millisec, and the TAU_EXPONENT would be 20
//#define TAU_EXPONENT 18 // twice the probe frequency. if probe freq = 128 millisec, the TAU should be 256 millisec, and the TAU_EXPONENT would be 18
//#define TAU_EXPONENT 17 // twice the probe frequency. if probe freq = 64 millisec, the TAU should be 128 millisec, and the TAU_EXPONENT would be 17
//#define TAU_EXPONENT 16 // twice the probe frequency. if probe freq = 32 millisec, the TAU should be 64 millisec, and the TAU_EXPONENT would be 16
const bit<32> UTIL_RESET_TIME_THRESHOLD = 512000;

/*************************************************************************
*********************** H E A D E R S  ***********************************
*************************************************************************/

typedef bit<9>  egressSpec_t;
typedef bit<48> macAddr_t;
typedef bit<32> ip4Addr_t;

header ethernet_t {
    macAddr_t dstAddr;
    macAddr_t srcAddr;
    bit<16>   etherType;
}

header ppp_t {
    bit<16>   pppType;
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
    macAddr_t  sha;
    ip4Addr_t spa;
    macAddr_t  tha;
    ip4Addr_t tpa;
}

header ipv4_t {
    bit<4>    version;
    bit<4>    ihl;
    bit<8>    diffserv;
    bit<16>   totalLen;
    bit<16>   identification;
    bit<3>    flags;
    bit<13>   fragOffset;
    bit<8>    ttl;
    bit<8>    protocol;
    bit<16>   hdrChecksum;
    ip4Addr_t srcAddr;
    ip4Addr_t dstAddr;
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

header udp_t {
    bit<16> srcPort;
    bit<16> dstPort;
    bit<16> length;
    bit<16> checksum;
}

//Background traffic
header hulapp_background_t {
    bit<32> port;
}

header hulapp_t {
    bit<16>  dst_tor;    //The sending TOR
    bit<32>  seq_no;     //Probe sequence number
    bit<32>  util;     //The path util
}

struct metadata {
    ip4Addr_t          ipv4DstAddr;
    bit<9>             outbound_port;
    bit<16>            dst_switch_id;

    bit<16>            dst_tor;
    bit<1>             debug_probe_best;
    bit<9>             debug_probe_port;
    bit<16>            debug_probe_dst_tor;
    bit<9>             debug_pkt_ingress_port;
    bit<9>             debug_pkt_egress_port;
    bit<32>            debug_pkt_fidx;
    bool               debug_pkt_flowlet_create;
    bool               debug_pkt_flowlet_cached;
    bool               debug_pkt_flowlet_thrash;
    bit<32>            debug_probe_util;
    bit<32>            debug_pkt_util;
    bit<48>            debug_pkt_time;
}

//The headers used in Hula++
struct headers {
    ethernet_t          ethernet;
    ppp_t               ppp;
    ipv4_t              ipv4;
    hulapp_background_t hulapp_background;
    hulapp_t            hulapp;
    arp_t               arp;
    arp_ipv4_t          arp_ipv4;
    tcp_t               tcp;
    udp_t               udp;
}

/*************************************************************************
*********************** P A R S E R  ***********************************
*************************************************************************/

parser ParserImpl(packet_in packet,
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
            TYPE_IPV4            : parse_ipv4;
            TYPE_ARP             : parse_arp;
            _                    : accept;
        }
    }

    state parse_ppp {
        packet.extract(hdr.ppp);
        transition select(hdr.ppp.pppType) {
            TYPE_IPV4            : parse_ipv4;
            _                    : accept;
        }
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
        meta.ipv4DstAddr = hdr.arp_ipv4.tpa;
        transition accept;
    }

    state parse_ipv4 {
        packet.extract(hdr.ipv4);
        meta.ipv4DstAddr = hdr.ipv4.dstAddr;
        transition select (hdr.ipv4.protocol) {
            HULAPP_PROTOCOL            : parse_hulapp;
            HULAPP_BACKGROUND_PROTOCOL : parse_hulapp_background;
            TCP_PROTOCOL               : parse_tcp;
            UDP_PROTOCOL               : parse_udp;
            _                          : accept;
        }
    }

    state parse_hulapp_background {
        packet.extract(hdr.hulapp_background);
        transition accept;
    }

    state parse_hulapp {
        packet.extract(hdr.hulapp);
        transition accept;
    }

    state parse_tcp {
        packet.extract(hdr.tcp);
        transition accept;
    }

    state parse_udp {
        packet.extract(hdr.udp);
        transition accept;
    }
}

/*************************************************************************
************   C H E C K S U M    V E R I F I C A T I O N   *************
*************************************************************************/

control verifyChecksum(inout headers hdr, inout metadata meta) {
    apply {  }
}

/*************************************************************************
**************  I N G R E S S   P R O C E S S I N G   *******************
*************************************************************************/

control ingress(inout headers hdr, inout metadata meta, inout standard_metadata_t standard_metadata) {

    register<bit<32>>(45) current_seq_no;
    // Decision table
    register<bit<32>>(45) decision_f2;       // Function value
    // Choices table
    register<bit<9>>(45) choices_nhop;      // Next hop port
    register<bit<32>>(45) choices_f2;        // Function value

    // Flowlet routing table
    register<bit<9>>(1024) flowlet_nhop;       // Flowlet next hop
    register<bit<32>>(1024) flowlet_dst;       // Flowlet destination
    register<bit<48>>(1024) flowlet_time;      // Flowlet time of last packet

    // LinkTable for Failure Detection
    register<bit<48>>(8) link_time;              // time of last probe using the link

    // Metric util
    register<bit<32>>(7) local_util;     // Local util per port.
    register<bit<48>>(7) last_packet_time;

/*----------------------------------------------------------------------*/
/*Some basic actions*/

    action drop() {
        mark_to_drop();
    }

    action add_hulapp_header() {
        hdr.hulapp.setValid();
        //An extra hop in the probe takes up 16bits, or 1 word.
        hdr.ipv4.ihl = hdr.ipv4.ihl + 1;
    }

/*----------------------------------------------------------------------*/
/*If Hula++ probe, mcast it to the right set of next hops*/

    // Write to the standard_metadata's mcast field!
    action set_hulapp_mcast(bit<16> mcast_id) {
      standard_metadata.mcast_grp = mcast_id;
    }

    table tab_hulapp_mcast {
        key = {
            standard_metadata.ingress_port: exact;
        }
        actions = {
          set_hulapp_mcast; 
          drop; 
          NoAction; 
        }
        default_action = drop();
    }

/*----------------------------------------------------------------------*/
/*Update the mac address based on the port*/

    action update_macs(macAddr_t dstAddr) {
        hdr.ethernet.srcAddr = hdr.ethernet.dstAddr;
        hdr.ethernet.dstAddr = dstAddr;
    }

    table tab_port_to_mac {
        key = {
            meta.outbound_port: exact;
        }   
        actions = {
            update_macs;
            NoAction;
        }
        size = 1024;
        default_action = NoAction();
    }

/*----------------------------------------------------------------------*/
/*At leaf switch, forward data packet to end host*/

    action forward_to_end_hosts(egressSpec_t port) {
        standard_metadata.egress_spec = port;
    }

    action mcast_to_all_end_hosts(bit<16> mcast_id) {
      standard_metadata.mcast_grp = mcast_id;
    }

    table tab_forward_to_end_hosts {
        key = {
//            hdr.ppp.pppType              : exact;
            hdr.ethernet.etherType       : exact;
            hdr.ipv4.protocol            : ternary;
            standard_metadata.egress_spec: exact;
            hdr.ipv4.dstAddr             : ternary;
        }
        actions = {
            forward_to_end_hosts;
            mcast_to_all_end_hosts;
            NoAction;
        }
        default_action = NoAction();
    }

/*----------------------------------------------------------------------*/
/*Update the destination switch ID from the ip prefix*/

    action update_id(bit<16> id) {
        meta.dst_switch_id = id;
    }

    table tab_prefix_to_id {
        key = {
            meta.ipv4DstAddr: lpm;
        }   
        actions = {
            update_id;
            drop;
            NoAction;
        }
        size = 1024;
        default_action = NoAction();
    }

/*----------------------------------------------------------------------*/
/*If data traffic, do normal forwarding*/

    action ipv4_forward(macAddr_t dstAddr, egressSpec_t port) {
        standard_metadata.egress_spec = port;
//        hdr.ethernet.srcAddr = hdr.ethernet.dstAddr;
//        hdr.ethernet.dstAddr = dstAddr;
        hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
    }

    table tab_ipv4_lpm {
        key = {
            hdr.ipv4.dstAddr: lpm;
        }   
        actions = {
            ipv4_forward;
            drop;
            NoAction;
        }
        size = 1024;
        default_action = NoAction();
    }

/*----------------------------------------------------------------------*/
/*Table used to observe some registers' value*/

    table tab_observe_metadata {
        key = { 
//            standard_metadata.ns3_node_id: ternary;
            meta.debug_probe_port: ternary;
            meta.debug_probe_dst_tor: ternary;
            meta.debug_pkt_ingress_port: ternary;
            meta.debug_pkt_egress_port: ternary;
            meta.debug_pkt_fidx: ternary;
            meta.debug_pkt_flowlet_create: ternary;
            meta.debug_pkt_flowlet_cached: ternary;
            meta.debug_pkt_flowlet_thrash: ternary;
            meta.debug_probe_util: ternary;
            meta.debug_pkt_util: ternary;
            meta.debug_pkt_time: ternary;
            meta.debug_probe_best: ternary;
        }
        actions = {
            NoAction;
        }
        default_action = NoAction();
    }
/*Table used to observe some registers' value*/

    table tab_observe_metadata2 {
        key = { 
//            standard_metadata.ns3_node_id: ternary;
            meta.debug_probe_port: ternary;
            meta.debug_probe_dst_tor: ternary;
            meta.debug_pkt_ingress_port: ternary;
            meta.debug_pkt_egress_port: ternary;
            meta.debug_pkt_fidx: ternary;
            meta.debug_pkt_flowlet_create: ternary;
            meta.debug_pkt_flowlet_cached: ternary;
            meta.debug_pkt_flowlet_thrash: ternary;
            meta.debug_probe_util: ternary;
            meta.debug_pkt_util: ternary;
            meta.debug_pkt_time: ternary;
            meta.debug_probe_best: ternary;
        }
        actions = {
            NoAction;
        }
        default_action = NoAction();
    }

/*----------------------------------------------------------------------*/
/*Applying the tables*/

    apply {

        if (hdr.tcp.isValid() || hdr.udp.isValid() || hdr.arp_ipv4.isValid()) {

            meta.debug_pkt_ingress_port = standard_metadata.ingress_port;

            tab_prefix_to_id.apply();
            bit<32> dst = (bit<32>) meta.dst_switch_id;

            if (hdr.ipv4.isValid()) {
                bit<16> srcPort;
                bit<16> dstPort;
                if (hdr.tcp.isValid()) {
                    srcPort = hdr.tcp.srcPort;
                    dstPort = hdr.tcp.dstPort;
                } else {
                    srcPort = hdr.udp.srcPort;
                    dstPort = hdr.udp.dstPort;
                }
                // Compute flowlet hash index
                bit<32> hash_index;
                hash(hash_index, 
                     HashAlgorithm.crc32,
                     (bit<10>) 0,
                     { hdr.ipv4.srcAddr,
                       hdr.ipv4.dstAddr,
                       hdr.ipv4.protocol,
                       srcPort,
                       dstPort },
                     (bit<32>) 1023);

                meta.debug_pkt_flowlet_create = false;
                meta.debug_pkt_flowlet_cached = false;
                meta.debug_pkt_flowlet_thrash = false;

                bit<32> fidx = ((bit<32>) hash_index);

                meta.debug_pkt_fidx = fidx;

                bit<48> ftime;
                bit<32> fdst;
                bit<9>  fport;
                flowlet_time.read(ftime, fidx);
                flowlet_dst.read(fdst, fidx);
                flowlet_nhop.read(fport, fidx);

                bit<32> cidx = dst;
                bit<9>  cport;
                choices_nhop.read(cport, cidx);

                // Check link timeout for link failure detection
                bit<48> ltime;
                link_time.read(ltime, (bit<32>) fport);

                bool initial_time = (ftime == 0);
                bool time_expired = initial_time || (standard_metadata.ingress_global_timestamp - ftime > FLOWLET_TIMEOUT);
                bool link_failed = (standard_metadata.ingress_global_timestamp - ltime > LINK_TIMEOUT);

                if (!time_expired && dst == fdst && !link_failed) {
                    meta.debug_pkt_flowlet_cached = true;
                    standard_metadata.egress_spec = fport;
                    flowlet_time.write(fidx, standard_metadata.ingress_global_timestamp);
                } else {
                    // We use the choices table to lookup the next hop
                    standard_metadata.egress_spec = cport;
                    // Update flowlet table if expired
                    if (time_expired || link_failed) {
                        meta.debug_pkt_flowlet_create = true;
                        flowlet_time.write(fidx, standard_metadata.ingress_global_timestamp);
                        flowlet_dst.write(fidx, dst);
                        flowlet_nhop.write(fidx, standard_metadata.egress_spec);
                    }
                    else {
                        meta.debug_pkt_flowlet_thrash = true;
                    }
                }
                // Remember the outbound port for mac translation
                meta.outbound_port = standard_metadata.egress_spec;

            } else {  // no ip header => arp req/reply => forward without flowlet routing
                bit<32> cidx = dst;
                choices_nhop.read(standard_metadata.egress_spec, cidx);

                // Remember the outbound port for mac translation
                meta.outbound_port = standard_metadata.egress_spec;
            }

            // Update the path utilization if necessary
            if (standard_metadata.egress_spec != 1) {
                bit<32> tmp_util = 0;
                bit<48> tmp_time = 0;
                bit<32> time_diff = 0;
                local_util.read(tmp_util, (bit<32>) standard_metadata.egress_spec - 2);
                last_packet_time.read(tmp_time, (bit<32>) standard_metadata.egress_spec - 2);
                time_diff = (bit<32>)(standard_metadata.ingress_global_timestamp - tmp_time);
                bit<32> temp = tmp_util*time_diff;
                tmp_util = time_diff > UTIL_RESET_TIME_THRESHOLD ?
                           0 : standard_metadata.packet_length + tmp_util - (temp >> TAU_EXPONENT);
                last_packet_time.write((bit<32>) standard_metadata.egress_spec - 2,
                                       standard_metadata.ingress_global_timestamp);
                local_util.write((bit<32>) standard_metadata.egress_spec - 2, tmp_util);

                meta.debug_pkt_util = tmp_util;
                meta.debug_pkt_time = standard_metadata.ingress_global_timestamp;
            }

            //tab_port_to_mac.apply();
            if (hdr.ipv4.isValid())
                hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
            meta.debug_pkt_egress_port = standard_metadata.egress_spec;
            tab_observe_metadata.apply();

            tab_forward_to_end_hosts.apply();
        } // end of processing hula data packet

        else if (hdr.ipv4.isValid()) { // processing probe and background traffic

            if (hdr.ipv4.protocol == HULAPP_BACKGROUND_PROTOCOL && standard_metadata.ingress_port == 1) {
                standard_metadata.egress_spec = (bit<9>)hdr.hulapp_background.port;
                hdr.ipv4.ttl = hdr.ipv4.ttl - 1;



                // Update the path utilization if necessary
                if (standard_metadata.egress_spec != 1) {
                    bit<32> tmp_util = 0;
                    bit<48> tmp_time = 0;
                    bit<32> time_diff = 0;
                    local_util.read(tmp_util, (bit<32>) standard_metadata.egress_spec - 2);
                    last_packet_time.read(tmp_time, (bit<32>) standard_metadata.egress_spec - 2);
                    time_diff = (bit<32>)(standard_metadata.ingress_global_timestamp - tmp_time);
                    bit<32> temp = tmp_util*time_diff;
                    tmp_util = time_diff > UTIL_RESET_TIME_THRESHOLD ?
                               0 : standard_metadata.packet_length + tmp_util - (temp >> TAU_EXPONENT);
                    last_packet_time.write((bit<32>) standard_metadata.egress_spec - 2,
                                           standard_metadata.ingress_global_timestamp);
                    local_util.write((bit<32>) standard_metadata.egress_spec - 2, tmp_util);

                    meta.debug_pkt_util = tmp_util;
                    meta.debug_pkt_time = standard_metadata.ingress_global_timestamp;
                }

            }

            else if (hdr.ipv4.protocol == HULAPP_PROTOCOL) {

                meta.debug_probe_port = standard_metadata.ingress_port;
                meta.debug_probe_dst_tor = hdr.hulapp.dst_tor;

                // Update LinkTable for Link Failure Detection
                link_time.write((bit<32>) standard_metadata.ingress_port, standard_metadata.ingress_global_timestamp);

                // Update Util
                bit<32> tmp_util = 0;
                bit<48> tmp_time;
                if (standard_metadata.ingress_port != 1) {
                    local_util.read(tmp_util, (bit<32>) standard_metadata.ingress_port - 2);
                    last_packet_time.read(tmp_time, (bit<32>) standard_metadata.ingress_port - 2);
                    if ((bit<32>)(standard_metadata.ingress_global_timestamp - tmp_time) > UTIL_RESET_TIME_THRESHOLD)
                        tmp_util = 0;
                }
                hdr.hulapp.util = hdr.hulapp.util > tmp_util ? hdr.hulapp.util : tmp_util;
                meta.debug_probe_util = hdr.hulapp.util;

                bit<32> probe_f2 = hdr.hulapp.util;
                bit<32> x2;
                bit<32> y;
                current_seq_no.read(y, (bit<32>) hdr.hulapp.dst_tor);
                bit<32> dst = (bit<32>) hdr.hulapp.dst_tor;
                bit<32> cidx = dst;

                // Update choices table
                choices_f2.read(x2, cidx);
                bool eq_seq = (hdr.hulapp.seq_no == y);
                bool gt_seq = (hdr.hulapp.seq_no > y);
                bool better_f = (probe_f2 < x2);
                if ((eq_seq && better_f) || gt_seq) {
                    choices_nhop.write(cidx, standard_metadata.ingress_port);
                    choices_f2.write(cidx, probe_f2);
                    current_seq_no.write(dst, hdr.hulapp.seq_no);
                }

                // Update decision table
                decision_f2.read(x2, dst);
                bit<1> update = 0;
                better_f = (probe_f2 < x2);
                if ((eq_seq && better_f) || gt_seq) {
                    decision_f2.write(dst, probe_f2);
                    current_seq_no.write(dst, hdr.hulapp.seq_no);
                    update = 1;
                }

                // Debug
                meta.debug_probe_best = update;

                //Multicast the Hula++ probe 
                if (update == 1) {
                    tab_hulapp_mcast.apply();
                } else {
                    mark_to_drop();
                }
                tab_observe_metadata2.apply();
            }

            else {  // not hula data packet, not hulapp probe, not background, but with ipv4 header => should not happen in our test
                tab_ipv4_lpm.apply();
            }

        }

        if (standard_metadata.egress_spec == 0 && standard_metadata.mcast_grp == 0) // avoid loopback
        {
            drop();
        }
    }

/*----------------------------------------------------------------------*/

}

/*************************************************************************
****************  E G R E S S   P R O C E S S I N G   *******************
*************************************************************************/

control egress(inout headers hdr, inout metadata meta, inout standard_metadata_t standard_metadata) {
    apply {  }
}

/*************************************************************************
*************   C H E C K S U M    C O M P U T A T I O N   **************
*************************************************************************/

control computeChecksum(
inout headers  hdr,
inout metadata meta)
{
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

control DeparserImpl(packet_out packet, in headers hdr) {
    apply {
        packet.emit(hdr.ethernet);
        packet.emit(hdr.ppp);
        packet.emit(hdr.ipv4);
        packet.emit(hdr.hulapp_background);
        packet.emit(hdr.hulapp);
        packet.emit(hdr.arp);
        packet.emit(hdr.arp_ipv4);
        packet.emit(hdr.tcp);
        packet.emit(hdr.udp);
    }
}

/*************************************************************************
***************************  S W I T C H  *******************************
*************************************************************************/

V1Switch(
ParserImpl(),
verifyChecksum(),
ingress(),
egress(),
computeChecksum(),
DeparserImpl()
) main;
