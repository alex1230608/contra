/* -*- P4_16 -*- */
#include <core.p4>
#include <v1model.p4>

const bit<8>  HULAPP_PROTOCOL = 254; 
const bit<8>  HULAPP_DATA_PROTOCOL = 253;
const bit<16> TYPE_IPV4 = 0x800;

//Hula++ probes carry a max. number of MAX_HOP hops on a path.
#define MAX_METRICS 255

/*************************************************************************
*********************** H E A D E R S  ***********************************
*************************************************************************/

typedef bit<9>  egressSpec_t;
typedef bit<48> macAddr_t;
typedef bit<32> ip4Addr_t;
typedef bit<32> switchID_t;


header ethernet_t {
    macAddr_t dstAddr;
    macAddr_t srcAddr;
    bit<16>   etherType;
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

//HulaPP data traffic
header hulapp_data_t {
    bit<32> dtag;      //Hula++ data traffic 
}

header metric_t {
    bit<32> metric;
}

header hulapp_t {
    bit<16>  count;      //Number of metrics a probe carries
    bit<16>  dst_tor;    //The sending TOR
    bit<32>  ptag;       //The probe tag 
    bit<32>  path_util;  //The path util
}


struct ingress_metadata_t {
    bit<16>  count;
}

struct parser_metadata_t {
    bit<16>  remaining;
    bit<32>  dtag;      //tag of data packet 
    bit<32>  ptag;      //tag of probe packet 
}

struct metadata {
    ingress_metadata_t   ingress_metadata;
    parser_metadata_t   parser_metadata;
}

//The headers used in Hula++
struct headers {
    ethernet_t         ethernet;
    ipv4_t             ipv4;
    hulapp_data_t      hulapp_data; 
    hulapp_t           hulapp;
    metric_t[MAX_METRICS] metrics; 
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
            TYPE_IPV4 : parse_ipv4;
            _         : accept;
        }
    }


    state parse_ipv4 {
        packet.extract(hdr.ipv4);

        transition select (hdr.ipv4.protocol) {
          HULAPP_PROTOCOL      : parse_hulapp;
          HULAPP_DATA_PROTOCOL : parse_hulapp_data;
          _                    : accept;
        }
    }

    state parse_hulapp_data {
        packet.extract(hdr.hulapp_data);
        meta.parser_metadata.dtag = hdr.hulapp_data.dtag;
    }

    state parse_hulapp {
        packet.extract(hdr.hulapp);
        meta.parser_metadata.remaining = hdr.hulapp.count;
        meta.parser_metadata.ptag = hdr.hulapp.ptag;
        transition select(meta.parser_metadata.remaining) {
            0 : accept;
            _ : parse_metrics;
        }
    }

    state parse_metrics {
        packet.extract(hdr.metrics.next);
        meta.parser_metadata.remaining = meta.parser_metadata.remaining  - 1;
        transition select(meta.parser_metadata.remaining) {
            0 : accept;
            _ : parse_metrics;
        }
    }
}


/*************************************************************************
************   C H E C K S U M    V E R I F I C A T I O N   *************
*************************************************************************/
control verifyChecksum(in headers hdr, inout metadata meta) {
    apply {  }
}


/*************************************************************************
**************  I N G R E S S   P R O C E S S I N G   *******************
*************************************************************************/

register<bit<32>>(2048) min_path_util; //Min path util on a port
register<bit<16>>(2048) best_nhop;     //Best next hop
register<bit<32>>(2048) link_util;     //Link util on a port.

control ingress(inout headers hdr, inout metadata meta, inout standard_metadata_t standard_metadata) {

/*----------------------------------------------------------------------*/
/*Some basic actions*/

    action drop() {
        mark_to_drop();
    }

    action add_hulapp_header() {

        hdr.hulapp.setValid();
        hdr.hulapp.count = 0;
        //An extra hop in the probe takes up 16bits, or 1 word.
        hdr.ipv4.ihl = hdr.ipv4.ihl + 1;
    }

/*----------------------------------------------------------------------*/
/*Process Hula++ probes*/

    action do_hulapp() {
       //TODO: Process HulaPP probe
    }


    /*If Hula++ probe, stamp swid into Hula++ header*/
    table tab_hulapp {
        key = { hdr.ipv4.protocol : exact; }
        actions        = { do_hulapp; NoAction; }
        default_action =  NoAction();
    }

/*----------------------------------------------------------------------*/
/*If Hula++ probe, mcast it to the right set of next hops*/

    // Write to the standard_metadata's mcast field!
    action set_hulapp_mcast(bit<16> mcast_id) {
      standard_metadata.mcast_grp = mcast_id;
    }

    table hulapp_mcast {

        key = {
          standard_metadata.ingress_port : exact; 
        }

        actions = {
          set_hulapp_mcast; 
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
        hdr.ethernet.srcAddr = hdr.ethernet.dstAddr;
        hdr.ethernet.dstAddr = dstAddr;
        hdr.ipv4.ttl = hdr.ipv4.ttl - 1;
    }


    table ipv4_lpm {
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
/*Applying the tables*/
    
    apply {
        if (hdr.ipv4.isValid()) {
            ipv4_lpm.apply();
            
            /*TODO: This step is taken care of by the probe generator!*/
            //Add Hula++ header, initialize count=0
            //add_hulapp_header();

            if (hdr.ipv4.protocol == HULAPP_PROTOCOL) {
              tab_hulapp.apply();

            
              //Update the min. path util metric:
              bit<16> the_dst_tor = hdr.hulapp.dst_tor;
              bit<32> the_path_util = hdr.hulapp.path_util;

              /*TODO: Update the Hula++ header with the current path util at this link*/
              //Ang: This may require the use of Counter types???

              //Update the min path util if we've found a better path
              bit<32> the_min_path_util;
              min_path_util.read(the_min_path_util, (bit<32>)the_dst_tor);

              if (the_path_util < the_min_path_util) {
                min_path_util.write((bit<32>)the_dst_tor, the_path_util);
              }

              //Multicast the Hula++ probe 
              hulapp_mcast.apply();
            }


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
    Checksum16() ipv4_checksum;

    apply {
        if (hdr.ipv4.isValid()) {
            hdr.ipv4.hdrChecksum = ipv4_checksum.get(
            {
                hdr.ipv4.version,
                hdr.ipv4.ihl,
                hdr.ipv4.diffserv,
                hdr.ipv4.totalLen,
                hdr.ipv4.identification,
                hdr.ipv4.flags,
                hdr.ipv4.fragOffset,
                hdr.ipv4.ttl,
                hdr.ipv4.protocol,
                hdr.ipv4.srcAddr,
                hdr.ipv4.dstAddr
            });
        }
    }
}

/*************************************************************************
***********************  D E P A R S E R  *******************************
*************************************************************************/

control DeparserImpl(packet_out packet, in headers hdr) {
    apply {
        packet.emit(hdr.ethernet);
        packet.emit(hdr.ipv4);
        packet.emit(hdr.hulapp_data);
        packet.emit(hdr.hulapp);
        packet.emit(hdr.metrics);
    }
}

/*************************************************************************
***********************  S W I T C H  *******************************
*************************************************************************/
V1Switch(
ParserImpl(),
verifyChecksum(),
ingress(),
egress(),
computeChecksum(),
DeparserImpl()
) main;

