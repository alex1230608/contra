/* -*- P4_16 -*- */
#include <core.p4>
#include <v1model.p4>

const bit<8>  HULAPP_PROTOCOL = 254; 
const bit<8>  HULAPP_DATA_PROTOCOL = 253;
const bit<8>  HULAPP_BACKGROUND_PROTOCOL = 252;
const bit<8>  HULAPP_TCP_DATA_PROTOCOL = 251;
const bit<8>  HULAPP_UDP_DATA_PROTOCOL = 250;
const bit<8>  TCP_PROTOCOL = 6;
const bit<8>  UDP_PROTOCOL = 17;
const bit<16> TYPE_IPV4 = 0x0800;
const bit<16> TYPE_ARP  = 0x0806;
const bit<16> TYPE_HULAPP_TCP_DATA = 0x2345;
const bit<16> TYPE_HULAPP_UDP_DATA = 0x2344;
const bit<9>  LOOP_THRESHOLD = 3;
const bit<48> FLOWLET_TIMEOUT = 50000;
const bit<48> LINK_TIMEOUT = 800000;
//#define TAU_EXPONENT 9 // twice the probe frequency. if probe freq = 256 microsec, the TAU should be 512 microsec, and the TAU_EXPONENT would be 9
#define TAU_EXPONENT 19 // twice the probe frequency. if probe freq = 256 millisec, the TAU should be 512 millisec, and the TAU_EXPONENT would be 19
//#define TAU_EXPONENT 18 // twice the probe frequency. if probe freq = 128 millisec, the TAU should be 256 millisec, and the TAU_EXPONENT would be 18
//#define TAU_EXPONENT 17 // twice the probe frequency. if probe freq = 64 millisec, the TAU should be 128 millisec, and the TAU_EXPONENT would be 17
//#define TAU_EXPONENT 16 // twice the probe frequency. if probe freq = 32 millisec, the TAU should be 64 millisec, and the TAU_EXPONENT would be 16
//#define TAU_EXPONENT 15 // twice the probe frequency. if probe freq = 16 millisec, the TAU should be 32 millisec, and the TAU_EXPONENT would be 15
//#define TAU_EXPONENT 20 // twice the probe frequency. if probe freq = 512 millisec, the TAU should be 1024 millisec, and the TAU_EXPONENT would be 20
//#define TAU_EXPONENT 21 // twice the probe frequency. if probe freq = 1024 millisec, the TAU should be 2048 millisec, and the TAU_EXPONENT would be 21
const bit<32> UTIL_RESET_TIME_THRESHOLD = 512000;
#define MAX_HOPS 25
const bit<16> TYPE_SWID = 0x8100;



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

//HulaPP data traffic
header hulapp_data_t {
    //bit<32> data_id;   // only for test
}

header hulapp_t {
    bit<16>   dst_tor;    //The sending TOR
    bit<32>  seq_no;     //Probe sequence number
    bit<32>  util;     //The path util
}

struct metadata {
    ip4Addr_t          ipv4DstAddr;
    bit<9>             outbound_port;

    bit<16>             dst_tor;
    bit<16>             dst_switch_id;
    bit<8> remove_tags;
}

header switchid_t {
    bit<16> sw_id;
    bit<16> type1;
}

//The headers used in Hula++
struct headers {
    ethernet_t          ethernet;
    switchid_t[MAX_HOPS] switchid_tags;
    ppp_t               ppp;
    ipv4_t              ipv4;
    hulapp_background_t hulapp_background;
    hulapp_data_t       hulapp_data; 
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
    }

    state parse_ethernet {
        packet.extract(hdr.ethernet);
        transition select(hdr.ethernet.etherType) {
            TYPE_IPV4            : parse_ipv4;
            TYPE_SWID            : parse_swids;
            TYPE_ARP             : parse_arp;
            TYPE_HULAPP_TCP_DATA : parse_hulapp_data;
            TYPE_HULAPP_UDP_DATA : parse_hulapp_data;
            _                    : accept;
        }
    }

    state parse_swids{
    packet.extract(hdr.switchid_tags.next);
		transition select(hdr.switchid_tags.last.type1) {
			TYPE_IPV4: parse_ipv4;
			TYPE_SWID: parse_swids;
			TYPE_ARP             : parse_arp;
			TYPE_HULAPP_TCP_DATA : parse_hulapp_data;
			TYPE_HULAPP_UDP_DATA : parse_hulapp_data;
			_                    : accept;
		}
    }

    state parse_ppp {
        packet.extract(hdr.ppp);
        transition select(hdr.ppp.pppType) {
            TYPE_IPV4            : parse_ipv4;
            TYPE_HULAPP_TCP_DATA : parse_hulapp_data;
            TYPE_HULAPP_UDP_DATA : parse_hulapp_data;
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
            HULAPP_TCP_DATA_PROTOCOL   : parse_hulapp_data;
            HULAPP_UDP_DATA_PROTOCOL   : parse_hulapp_data;
            HULAPP_DATA_PROTOCOL       : parse_hulapp_data;
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

    state parse_hulapp_data {
        packet.extract(hdr.hulapp_data);
        transition select (hdr.ipv4.protocol) {
            HULAPP_TCP_DATA_PROTOCOL : parse_tcp;
            HULAPP_UDP_DATA_PROTOCOL : parse_udp;
            _                        : accept;
        }
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

    // Packet Id for tracking the path
    register<bit<16>>(1) currentid;

    // Most recent sequence number
    register<bit<32>>(45) current_seq_no;
    register<bit<32>>(45) decision_seq_no;

    // Decision table
    register<bit<32>>(45) decision_f1;       // Function value

    // Choices table
    register<bit<9>>(45) choices_nhop;      // Next hop port
    register<bit<32>>(45) choices_f1;        // Function value

    // Flowlet routing table
    register<bit<9>>(1024)  flowlet_nhop;           // Flowlet next hop
    register<bit<16>>(1024) flowlet_dst;            // Flowlet destination
    register<bit<48>>(1024) flowlet_time;           // Flowlet time of last packet
    register<bit<9>>(1024)  flowlet_hopcount;       // Flowlet lazy loop prevention count
    register<bit<1>>(1024)  flowlet_hopcount_valid; // Flowlet loop prevention valid

    // LinkTable for Failure Detection
    register<bit<48>>(5) link_time;              // time of last probe using the link

    // Metric util
    register<bit<32>>(4) local_util;     // Local util per port.
    register<bit<48>>(4) last_packet_time;

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
        }
        actions = {
          set_hulapp_mcast; 
          drop; 
          NoAction; 
        }
        default_action = set_hulapp_mcast(1);
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
/*Remove hula header between IP and TCP/UDP header*/

    action remove_hula_tcp() {
        if (hdr.ipv4.isValid())
            hdr.ipv4.protocol = TCP_PROTOCOL;
        else if (hdr.ethernet.isValid())
            hdr.ethernet.etherType = TYPE_ARP;
        hdr.hulapp_data.setInvalid();
    }

    action remove_hula_udp() {
        if (hdr.ipv4.isValid())
            hdr.ipv4.protocol = UDP_PROTOCOL;
        else if (hdr.ethernet.isValid())
            hdr.ethernet.etherType = TYPE_ARP;
        hdr.hulapp_data.setInvalid();
    }

    table tab_remove_hula_header {
        key = {
            hdr.ethernet.etherType       : exact;
            hdr.ipv4.protocol            : ternary;
            standard_metadata.egress_spec: exact;
        }
        actions = {
            remove_hula_tcp;
            remove_hula_udp;
            NoAction;
        }
        const entries = {
            (TYPE_HULAPP_TCP_DATA, _,                        1) : remove_hula_tcp();
            (TYPE_IPV4,            HULAPP_TCP_DATA_PROTOCOL, 1) : remove_hula_tcp();
            (TYPE_HULAPP_UDP_DATA, _,                        1) : remove_hula_udp();
            (TYPE_IPV4,            HULAPP_UDP_DATA_PROTOCOL, 1) : remove_hula_udp();
        }
        size = 4;
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
/*Inject hula header between IP and TCP/UDP header*/

    action inject_hula_below_ipv4_above_tcp() {
        hdr.ipv4.protocol = HULAPP_TCP_DATA_PROTOCOL;
        hdr.hulapp_data.setValid();
//        currentid.read(hdr.ipv4.identification, 0);
//        currentid.write(0, hdr.ipv4.identification+1);
        //hdr.hulapp_data.data_id = hdr.tcp.seqNo; // use tcp.seqNo as our data_id
    }

    action inject_hula_below_ipv4_above_udp() {
        hdr.ipv4.protocol = HULAPP_UDP_DATA_PROTOCOL;
        hdr.hulapp_data.setValid();
//        currentid.read(hdr.ipv4.identification, 0);
//        currentid.write(0, hdr.ipv4.identification+1);
    }

    action inject_hula_above_arp() {
        hdr.ethernet.etherType = TYPE_HULAPP_TCP_DATA;
        hdr.hulapp_data.setValid();
        //hdr.hulapp_data.data_id = 0; // use tcp.seqNo as our data_id
    }

    table tab_inject_hula_header {
        key = {
            hdr.ethernet.etherType          : exact;
            hdr.ipv4.protocol               : ternary;
        }
        actions = {
            inject_hula_below_ipv4_above_tcp;
            inject_hula_below_ipv4_above_udp;
            inject_hula_above_arp;
            NoAction;
        }
        const entries = {
            (TYPE_IPV4, TCP_PROTOCOL) : inject_hula_below_ipv4_above_tcp();
            (TYPE_IPV4, UDP_PROTOCOL) : inject_hula_below_ipv4_above_udp();
            (TYPE_ARP,  _)            : inject_hula_above_arp();
        }
        size = 3;
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
/* add SWID between under ethernet */
    action add_switchid_tag(bit<16> sw_id) {
      hdr.switchid_tags.push_front(1);
      hdr.switchid_tags[0].setValid();
      hdr.switchid_tags[0].sw_id = sw_id;
      hdr.switchid_tags[0].type1 = hdr.ethernet.etherType;
      hdr.ethernet.etherType = TYPE_SWID;
    }

    table swid_tag {
            key={
            hdr.ethernet.etherType          : exact;
            }
        actions = {
            add_switchid_tag;
            }
        size = 2;
        }
/*----------------------------------------------------------------------*/

/*----------------------------------------------------------------------*/
/*Applying the tables*/

    apply {


        bit<9> hop_count = 255 - (bit<9>) hdr.ipv4.ttl;

        if (hdr.hulapp_data.isValid()
            || (hdr.tcp.isValid() || hdr.udp.isValid())
                 && (standard_metadata.ingress_port == 1
                     || standard_metadata.ingress_port >= 5)          // TCP/UDP data packet from outside
            || hdr.arp_ipv4.isValid()
                 && (standard_metadata.ingress_port == 1
                     || standard_metadata.ingress_port >= 5)) {  // arp request/reply from outside
            tab_inject_hula_header.apply();
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
                     (bit<32>) 0,
                     { hdr.ipv4.srcAddr,
                       hdr.ipv4.dstAddr,
                       hdr.ipv4.protocol,
                       srcPort,
                       dstPort },
                     (bit<32>) 1023);


                // Compute flowlet information
                bit<32> fidx = hash_index;

                bit<48> ftime;
                bit<16> fdst;
                bit<9>  fport;
                bit<9>  fcount;
                bit<1>  fvalid;
                flowlet_time.read(ftime, fidx);
                flowlet_dst.read(fdst, fidx);
                flowlet_nhop.read(fport, fidx);
                flowlet_hopcount.read(fcount, hash_index);
                flowlet_hopcount_valid.read(fvalid, hash_index);

                // Update min hop count if necessary
                if (hop_count < fcount) {
                    flowlet_hopcount_valid.write(hash_index, 1);
                    flowlet_hopcount.write(hash_index, hop_count);
                }

                // Compute the best choices entry information
                bit<32> cidx = dst;
                bit<9> cport;
                choices_nhop.read(cport, cidx);

                // Check link timeout for link failure detection
                bit<48> ltime;
                link_time.read(ltime, (bit<32>) fport);

                // Check various conditions
                bit<48> flowlet_elapsed = standard_metadata.ingress_global_timestamp - ftime;
                bool initial_time = (ftime == 0);
                bool maybe_loop = (fcount + LOOP_THRESHOLD < hop_count) || (standard_metadata.ingress_port == fport);
                bool different_nhop = (cport != fport);
                bool needs_flush = (fvalid == 1) && maybe_loop && different_nhop;
                bool time_expired = initial_time || (flowlet_elapsed > FLOWLET_TIMEOUT);
                bool link_failed = (standard_metadata.ingress_global_timestamp - ltime > LINK_TIMEOUT);

                if (!time_expired && !needs_flush && dst == (bit<32>) fdst && !link_failed) {
                    standard_metadata.egress_spec = fport;
                    flowlet_time.write(fidx, standard_metadata.ingress_global_timestamp);

                } else {
                    // We use the choices table to lookup the next hop
                    standard_metadata.egress_spec = cport;

                    // Update flowlet table if expired
                    if (time_expired || needs_flush || link_failed) {
                        flowlet_time.write(fidx, standard_metadata.ingress_global_timestamp);
                        flowlet_dst.write(fidx, (bit<16>) dst);
                        flowlet_nhop.write(fidx, standard_metadata.egress_spec);
                        flowlet_hopcount.write(hash_index, hop_count);
                        flowlet_hopcount_valid.write(hash_index, 1);
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

            }

            //tab_port_to_mac.apply();
            if (hdr.ipv4.isValid()) {
                hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
                swid_tag.apply();
            }

            // Remove all tags from packets at last hop
            if(hdr.ipv4.isValid() && standard_metadata.egress_spec == 1){
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
                if(hdr.ethernet.etherType==TYPE_SWID){
                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;
                      hdr.switchid_tags.pop_front(1);
                }
            }

            tab_remove_hula_header.apply();

            tab_forward_to_end_hosts.apply();
        } // end of processing hulapp_data

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

                }

            }

            else if (hdr.ipv4.protocol == HULAPP_PROTOCOL) {

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

                bit<32> dst = (bit<32>) hdr.hulapp.dst_tor;

                // Compute the overall decision function f
                bit<32> tmp_util0 = hdr.hulapp.util;

                bit<32> probe_decision_f1 = tmp_util0;

                // Compute the probe-local, choices evaluation function
                bit<32> probe_f1 = 9999999;
                    probe_f1 = hdr.hulapp.util;

                bit<1> update = 0;

                // Update choices table
                bit<32> x1;
                bit<32> y;
                bit<32> cidx = dst;
                current_seq_no.read(y, cidx);
                choices_f1.read(x1, cidx);
                bool eq_seq = (hdr.hulapp.seq_no == y);
                bool gt_seq = (hdr.hulapp.seq_no > y);
                bool better_f = (probe_f1 < x1);
                if ((eq_seq && better_f) || gt_seq) {
                    choices_nhop.write(cidx, standard_metadata.ingress_port);
                    choices_f1.write(cidx, probe_f1);
                    current_seq_no.write(cidx, hdr.hulapp.seq_no);
                    update = 1;
                }

                // Update decision table
                decision_seq_no.read(y, (bit<32>) hdr.hulapp.dst_tor);
                eq_seq = (hdr.hulapp.seq_no == y);
                gt_seq = (hdr.hulapp.seq_no > y);
                decision_f1.read(x1, dst);
                better_f = (probe_decision_f1 < x1);
                if ((eq_seq && better_f) || gt_seq) {
                    decision_f1.write(dst, probe_decision_f1);
                    decision_seq_no.write((bit<32>) hdr.hulapp.dst_tor, hdr.hulapp.seq_no);
                    update = 1;
                }

                //Multicast the Hula++ probe 
                if (update == 1) {
                    tab_hulapp_mcast.apply();
                } else {
                    mark_to_drop();
                }
            }

            else {  // not hulapp_data, not hulapp probe, not background, not hula ping/pong, but with ipv4 header => should not happen in our test
                tab_ipv4_lpm.apply();
                drop();
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
    apply {
    }
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
        packet.emit(hdr.switchid_tags);
        packet.emit(hdr.ppp);
        packet.emit(hdr.ipv4);
        packet.emit(hdr.hulapp_background);
        packet.emit(hdr.hulapp_data);
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