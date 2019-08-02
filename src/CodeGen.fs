module CodeGen

open Ast 
open TypeCheck
open CGraph
open Printf
open System.Text

// TODO list
// Ignore for now
// - Pin flow to next-hop
// - Implement max, min, etc
// - Right now we minimize, do we want to maximize?
//
// Optimizations
// - Minimize the number of tags used in the product graph
// - Minimize the number of bits in dtag, ptag

type PathAttributes = Set<string>

type Metrics = seq<Ast.Expr>

type Tag = Tag of int

type Index = Index of int

type Device = string

type Port = Index * Device

type Context = 
   { Graph : CGraph.T 
     NodeMap : Map<Device, CGraph.CgState list>
     PortMap : Map<Device, seq<Port>> 
     Ast : Ast.T
     Type : Type
     Metrics : Metrics
     Attrs : PathAttributes 
     NodeCount : int
     NumProbes : int
     NumProbesBits : int
     ProbeMap : Map<int, Isotonicity.AttrType>
     DestinationMap : Map<string, Tag * Index>
     DeviceIdMap : Map<Device, int>
     McastIdMap : Map<CgState, int>
     isNS3 : bool
     NUM_END_HOSTS : int 
     swidEnabled : bool 
     mininet_server_mapping : int
     workload : int }


let LOCAL = 1
let ABILENE = 2
let FATTREE = 3
let SINGLE_POD = 4
let BT = 5
let BT_ALL_IPERF = 6
let ABILENE_ALL_IPERF = 7
let EMPTY = 8

let hasAttr (ctx : Context) s = 
   Seq.exists ((=) s) ctx.Attrs

let tupleSize (ctx : Context) = 
   match ctx.Type with 
   | Product es -> List.length es 
   | _ -> failwith "impossible"

let template f = 
   let sb = StringBuilder() 
   f sb
   sb.ToString()


let numDstSwitches ctx = 
   Map.count ctx.DestinationMap

let numBits (size : int) = 
  let bits = System.Math.Log((float) size, 2.)
  let roundedBits = (int) (System.Math.Round(bits, 0))
  (roundedBits+31)/32*32

let preambleTxt ctx =
   let settings = Args.getSettings()
   let flowlet_timeout = settings.FlowletTimeout  // 50000 (us) for abilene with 40Mbps sw links and 200 (us) for fattree-45 with 10Mbps sw links
   let probe_period = settings.ProbePeriod        // 256*1024 (us) for most of our settings
   let link_timeout = settings.LinkTimeout        // 800000 (us) for probe period 256*1024 us
   let tau_exp = (int) (System.Math.Round(System.Math.Log((float)probe_period, 2.), 0)) + 1  // if probe_freq = 256 us, tau should be 512 us, tau_exp should be 9
   let loop_threshold = 3
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      if ctx.isNS3 then
         p "#include <pronet1.p4>"
      else
         p "#include <core.p4>"
         p "#include <v1model.p4>"
      p ""
      p "const bit<8>  HULAPP_PROTOCOL = 254; "
      p "const bit<8>  HULAPP_DATA_PROTOCOL = 253;"
      p "const bit<8>  HULAPP_BACKGROUND_PROTOCOL = 252;"
      p "const bit<8>  HULAPP_TCP_DATA_PROTOCOL = 251;"
      p "const bit<8>  HULAPP_UDP_DATA_PROTOCOL = 250;"
      p "const bit<8>  TCP_PROTOCOL = 6;"
      p "const bit<8>  UDP_PROTOCOL = 17;"
      if ctx.isNS3 then
         p "const bit<16> TYPE_IPV4 = 0x0021;"
      else
         p "const bit<16> TYPE_IPV4 = 0x0800;"
      p "const bit<16> TYPE_ARP  = 0x0806;"
      p "const bit<16> TYPE_HULAPP_TCP_DATA = 0x2345;"
      p "const bit<16> TYPE_HULAPP_UDP_DATA = 0x2344;"
      p "const bit<9>  LOOP_THRESHOLD = %d;" loop_threshold
      p "const bit<48> FLOWLET_TIMEOUT = %d;" flowlet_timeout
      p "const bit<48> LINK_TIMEOUT = %d;" link_timeout

      if hasAttr ctx "latency" then
         p "const bit<8> HULAPP_PING_PROTOCOL = 250;"
         p "const bit<8> HULAPP_PONG_PROTOCOL = 249;"
         p "#define ALPHA_EXPONENT 1 // alpha = 1/(2^ALPHA_EXPONENT), for ALPHA_EXPONENT = 1, alpha = 1/2"

      if hasAttr ctx "util" then
         p "#define TAU_EXPONENT %d" tau_exp
         p "const bit<32> UTIL_RESET_TIME_THRESHOLD = %d;" (probe_period * 2)

      if ctx.swidEnabled then
         p "#define MAX_HOPS 25"
         p "const bit<16> TYPE_SWID = 0x8100;"
      p ""
   )

 
    
let headersTxt device numTagBits (ctx : Context) = 
   let settings = Args.getSettings()
   let numDstBits = 16

   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      let ports = Map.find device ctx.PortMap
      let numPorts = Seq.length ports // add one for host 
      p ""
      if hasAttr ctx "queue" then
         p "register<bit<32>>(%d) local_queue;     // Local queue per port." (numPorts + 1)
      p ""
      p "/*************************************************************************"
      p "*********************** H E A D E R S  ***********************************"
      p "*************************************************************************/"
      p ""
      p "typedef bit<9>  egressSpec_t;"
      p "typedef bit<48> macAddr_t;"
      p "typedef bit<32> ip4Addr_t;"
      p ""
      p "header ethernet_t {"
      p "    macAddr_t dstAddr;"
      p "    macAddr_t srcAddr;"
      p "    bit<16>   etherType;"
      p "}"
      p ""
      p "header ppp_t {"
      p "    bit<16>   pppType;"
      p "}"
      p ""
      p "const bit<16> ARP_HTYPE_ETHERNET = 0x0001;"
      p "const bit<16> ARP_PTYPE_IPV4     = 0x0800;"
      p ""
      p "const bit<8>  ARP_HLEN_ETHERNET  = 6;"
      p "const bit<8>  ARP_PLEN_IPV4      = 4;"
      p "const bit<16> ARP_OPER_REQUEST   = 1;"
      p "const bit<16> ARP_OPER_REPLY     = 2;"
      p ""
      p "header arp_t {"
      p "    bit<16> htype;"
      p "    bit<16> ptype;"
      p "    bit<8>  hlen;"
      p "    bit<8>  plen;"
      p "    bit<16> oper;"
      p "}"
      p ""
      p "header arp_ipv4_t {"
      p "    macAddr_t  sha;"
      p "    ip4Addr_t spa;"
      p "    macAddr_t  tha;"
      p "    ip4Addr_t tpa;"
      p "}"
      p ""
      p "header ipv4_t {"
      p "    bit<4>    version;"
      p "    bit<4>    ihl;"
      p "    bit<8>    diffserv;"
      p "    bit<16>   totalLen;"
      p "    bit<16>   identification;"
      p "    bit<3>    flags;"
      p "    bit<13>   fragOffset;"
      p "    bit<8>    ttl;"
      p "    bit<8>    protocol;"
      p "    bit<16>   hdrChecksum;"
      p "    ip4Addr_t srcAddr;"
      p "    ip4Addr_t dstAddr;"
      p "}"
      p ""
      p "header tcp_t {"
      p "    bit<16> srcPort;"
      p "    bit<16> dstPort;"
      p "    bit<32> seqNo;"
      p "    bit<32> ackNo;"
      p "    bit<4>  dataOffset;"
      p "    bit<3>  res;"
      p "    bit<3>  ecn;"
      p "    bit<6>  ctrl;"
      p "    bit<16> window;"
      p "    bit<16> checksum;"
      p "    bit<16> urgentPtr;"
      p "}"
      p ""
      p "header udp_t {"
      p "    bit<16> srcPort;"
      p "    bit<16> dstPort;"
      p "    bit<16> length;"
      p "    bit<16> checksum;"
      p "}"

      p "//Background traffic"
      p "header hulapp_background_t {"
      p "    bit<32> port;"
      p "}"
      p ""
      p "//HulaPP data traffic"
      p "header hulapp_data_t {"
      if (ctx.NumProbes > 1) then
         p "    bit<%d>            probe_id;" ctx.NumProbesBits
      if (numTagBits > 0) then
         p "    bit<%d> dtag;      //Hula++ data traffic " numTagBits
      p "    //bit<32> data_id;   // only for test"
      p "}"
      p ""
      p "header hulapp_t {"
      if (ctx.NumProbes > 1) then
         p "    bit<%d>            probe_id;" ctx.NumProbesBits
      p "    bit<%d>   dst_tor;    //The sending TOR" numDstBits
      p "    bit<32>  seq_no;     //Probe sequence number"
      if (numTagBits > 0) then
         p "    bit<%d>   ptag;       //The probe tag" numTagBits

      for attr in ctx.Attrs do 
         let s = attr
         match s with
         | "length" 
         | "util" ->
             p "    bit<32>  %s;     //The path %s" s s
         | _ -> failwith ("Attribute not implemented: " + s)

      p "}"
      p ""
      if hasAttr ctx "latency" then
         p "header hulapp_ping_t {"
         p "    bit<48> timestamp;"
         p "}"
         p ""
         p "header hulapp_pong_t {"
         p "    bit<32> latency;"
         p "}"
         p ""
      p "struct metadata {"
      p "    ip4Addr_t          ipv4DstAddr;"
      p "    bit<9>             outbound_port;"
      p ""
      p "    bit<%d>             dst_tor;" numDstBits
      p "    bit<16>             dst_switch_id;"
      if ctx.swidEnabled then
         p "    bit<8> remove_tags;"
      if (numTagBits > 0) then
         p "    bit<%d>             original_tag;" numTagBits
         p "    bit<%d>             local_tag;" numTagBits

      if settings.Debug then
         p "    bit<1>             debug_probe_choice_update;"
         p "    bit<1>             debug_probe_decision_update;"
         p "    bit<32>            debug_probe_seq_no;"
         p "    bit<%d>            debug_probe_id;" ctx.NumProbesBits
         p "    bit<9>             debug_probe_port;"
         p "    bit<16>            debug_probe_dst_tor;"
         if (numTagBits > 0) then
            p "    bit<%d>             debug_probe_ptag;" numTagBits
            p "    bit<%d>             debug_probe_new_ptag;" numTagBits
            p "    bit<%d>             debug_probe_local_ptag;" numTagBits
         p "    bit<9>             debug_pkt_ingress_port;"
         p "    bit<9>             debug_pkt_egress_port;"
         p "    bit<32>            debug_pkt_data_id;"
         if (numTagBits > 0) then
            p "    bit<%d>             debug_pkt_tag;" numTagBits
            p "    bit<%d>             debug_pkt_new_tag;" numTagBits
            p "    bit<%d>             debug_pkt_local_tag;" numTagBits
         p "    bit<32>            debug_pkt_fidx;"
         p "    bool               debug_pkt_flowlet_create;"
         p "    bool               debug_pkt_flowlet_cached;"
         p "    bool               debug_pkt_flowlet_thrash;"
         p "    bool               debug_pkt_flowlet_maybe_loop;"
         p "    bool               debug_pkt_flowlet_different_nhop;"
         p "    bit<1>             debug_pkt_flowlet_fvalid;"
         p "    bool               debug_pkt_flowlet_time_expired;"
         p "    bit<9>             debug_pkt_flowlet_hop_count;"
         p "    bit<9>             debug_pkt_flowlet_cport;"
         p "    bit<9>             debug_pkt_flowlet_fport;"

         if hasAttr ctx "util" then 
            p "    bit<32>            debug_probe_util;"
            p "    bit<32>            debug_pkt_util;"
            p "    bit<48>            debug_pkt_time;"
         if hasAttr ctx "queue" then
            p "    bit<32>            debug_probe_queue;"
         if hasAttr ctx "length" then 
            p "    bit<32>            debug_probe_length;"
         if hasAttr ctx "latency" then
            p "    bit<32>            debug_probe_latency;"
         p "    bit<32>            debug_probe_cidx;"
         for i = 1 to tupleSize ctx do
            p "    bit<32>            debug_probe_decision_f%d;" i
         for i = 1 to tupleSize ctx do
            p "    bit<32>            debug_probe_decision_x%d;" i
         for i = 1 to tupleSize ctx do
            p "    bit<32>            debug_probe_f%d;" i
         for i = 1 to tupleSize ctx do
            p "    bit<32>            debug_probe_x%d;" i
           
            
      p "}"
      if ctx.swidEnabled then
         p ""
         p "header switchid_t {"
         p "    bit<16> sw_id;"
         p "    bit<16> type1;"
         p "}"
      p ""
      p "//The headers used in Hula++"
      p "struct headers {"
      p "    ethernet_t          ethernet;"
      if ctx.swidEnabled then
         p "    switchid_t[MAX_HOPS] switchid_tags;"
      p "    ppp_t               ppp;"
      p "    ipv4_t              ipv4;"
      p "    hulapp_background_t hulapp_background;"
      p "    hulapp_data_t       hulapp_data; "
      p "    hulapp_t            hulapp;"
      if hasAttr ctx "latency" then
         p "    hulapp_ping_t       hulapp_ping;"
         p "    hulapp_pong_t       hulapp_pong;"
      p "    arp_t               arp;"
      p "    arp_ipv4_t          arp_ipv4;"
      p "    tcp_t               tcp;"
      p "    udp_t               udp;"
      p "}"
      p ""
   )

let parserTxt (ctx : Context) =
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "*********************** P A R S E R  ***********************************"
      p "*************************************************************************/"
      p ""
      p "parser ParserImpl(packet_in packet,"
      p "out headers hdr,"
      p "inout metadata meta,"
      p "inout standard_metadata_t standard_metadata) {"
      p ""
      p "    state start {"
      if ctx.isNS3 then
         p "        transition parse_ppp;"
      else
         p "        transition parse_ethernet;"
      p "    }"
      p ""
      p "    state parse_ethernet {"
      p "        packet.extract(hdr.ethernet);"
      p "        transition select(hdr.ethernet.etherType) {"
      p "            TYPE_IPV4            : parse_ipv4;"
      if ctx.swidEnabled then
         p "            TYPE_SWID            : parse_swids;"
      p "            TYPE_ARP             : parse_arp;"
      p "            TYPE_HULAPP_TCP_DATA : parse_hulapp_data;"
      p "            TYPE_HULAPP_UDP_DATA : parse_hulapp_data;"
      p "            _                    : accept;"
      p "        }"
      p "    }"
      p ""
      if ctx.swidEnabled then
         p "    state parse_swids{"
         p "    packet.extract(hdr.switchid_tags.next);"
         p "		transition select(hdr.switchid_tags.last.type1) {"
         p "			TYPE_IPV4: parse_ipv4;"
         p "			TYPE_SWID: parse_swids;"
         p "			TYPE_ARP             : parse_arp;"
         p "			TYPE_HULAPP_TCP_DATA : parse_hulapp_data;"
         p "			TYPE_HULAPP_UDP_DATA : parse_hulapp_data;"
         p "			_                    : accept;"
         p "		}"
         p "    }"
         p ""
      p "    state parse_ppp {"
      p "        packet.extract(hdr.ppp);"
      p "        transition select(hdr.ppp.pppType) {"
      p "            TYPE_IPV4            : parse_ipv4;"
      p "            TYPE_HULAPP_TCP_DATA : parse_hulapp_data;"
      p "            TYPE_HULAPP_UDP_DATA : parse_hulapp_data;"
      p "            _                    : accept;"
      p "        }"
      p "    }"
      p ""
      p "    state parse_arp {"
      p "        packet.extract(hdr.arp);"
      p "        transition select(hdr.arp.htype, hdr.arp.ptype,"
      p "                          hdr.arp.hlen,  hdr.arp.plen) {"
      p "            (ARP_HTYPE_ETHERNET, ARP_PTYPE_IPV4,"
      p "             ARP_HLEN_ETHERNET,  ARP_PLEN_IPV4) : parse_arp_ipv4;"
      p "            default : accept;"
      p "        }"
      p "    }"
      p ""
      p "    state parse_arp_ipv4 {"
      p "        packet.extract(hdr.arp_ipv4);"
      p "        meta.ipv4DstAddr = hdr.arp_ipv4.tpa;"
      p "        transition accept;"
      p "    }"
      p ""
      p "    state parse_ipv4 {"
      p "        packet.extract(hdr.ipv4);"
      p "        meta.ipv4DstAddr = hdr.ipv4.dstAddr;"
      p "        transition select (hdr.ipv4.protocol) {"
      p "            HULAPP_PROTOCOL            : parse_hulapp;"
      p "            HULAPP_TCP_DATA_PROTOCOL   : parse_hulapp_data;"
      p "            HULAPP_UDP_DATA_PROTOCOL   : parse_hulapp_data;"
      p "            HULAPP_DATA_PROTOCOL       : parse_hulapp_data;"
      p "            HULAPP_BACKGROUND_PROTOCOL : parse_hulapp_background;"
      if hasAttr ctx "latency" then
         p "            HULAPP_PING_PROTOCOL       : parse_hulapp_ping;"
         p "            HULAPP_PONG_PROTOCOL       : parse_hulapp_pong;"
      p "            TCP_PROTOCOL               : parse_tcp;"
      p "            UDP_PROTOCOL               : parse_udp;"
      p "            _                          : accept;"
      p "        }"
      p "    }"
      p ""
      p "    state parse_hulapp_background {"
      p "        packet.extract(hdr.hulapp_background);"
      p "        transition accept;"
      p "    }"
      p ""
      p "    state parse_hulapp_data {"
      p "        packet.extract(hdr.hulapp_data);"
      p "        transition select (hdr.ipv4.protocol) {"
      p "            HULAPP_TCP_DATA_PROTOCOL : parse_tcp;"
      p "            HULAPP_UDP_DATA_PROTOCOL : parse_udp;"
      p "            _                        : accept;"
      p "        }"
      p "    }"
      p ""
      p "    state parse_hulapp {"
      p "        packet.extract(hdr.hulapp);"
      p "        transition accept;"
      p "    }"
      p ""
      if hasAttr ctx "latency" then
         p "    state parse_hulapp_ping {"
         p "        packet.extract(hdr.hulapp_ping);"
         p "        transition accept;"
         p "    }"
         p ""
         p "    state parse_hulapp_pong {"
         p "        packet.extract(hdr.hulapp_pong);"
         p "        transition accept;"
         p "    }"
         p ""
      p "    state parse_tcp {"
      p "        packet.extract(hdr.tcp);"
      p "        transition accept;"
      p "    }"
      p ""
      p "    state parse_udp {"
      p "        packet.extract(hdr.udp);"
      p "        transition accept;"
      p "    }"
      p "}"
      p ""
   )

let checksumVerifyTxt =
   template (fun sb ->
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "************   C H E C K S U M    V E R I F I C A T I O N   *************"
      p "*************************************************************************/"
      p ""
      p "control verifyChecksum(inout headers hdr, inout metadata meta) {"
      p "    apply {  }"
      p "}"  
      p ""
   )

let createComparison tupSize left right =
   let rec aux i =
      let l = left + (string i)
      let r = right + (string i)
      let less = l + " < " + r
      let eq = l + " == " + r
      let inner = if i = tupSize then "" else " || (" + eq + " && " + (aux (i + 1)) + ")"
      "(" + less + inner + ")"
   aux 1

let getUpdateValue attr = 
   match attr.Name with 
   | "length" -> 1
   | "util" -> 20
   | _ -> failwith "Not implemented"

let getComparison attr = 
   match attr.Name with 
   | "queue" -> "<"
   | "length" -> "<"
   | "util" -> "<"
   | _ -> failwith "Not implemented"

let tagsForRegex (r : Ast.Re) (pg : CGraph.T) nodes : Set<int> = 
   let i = Map.find r pg.Regexes
   Seq.fold (fun acc n -> if n.Accept.Contains i then Set.add n.Id acc else acc) Set.empty nodes

let generateOptFunction nodes (ctx : Context) (i : int) : string = 
   let rec aux (inliner : Map<string, string>) i (e : Ast.Expr) = 
      match e.Node with
      | Let(id,e1,e2) -> 
         let s1 = aux inliner i e1
         let inliner' = Map.add id.Name s1 inliner
         aux inliner' i e2
      | If(e1,e2,e3) -> 
         let s1 = aux inliner i e1 
         let s2 = aux inliner i e2 
         let s3 = aux inliner i e3
         sprintf "(%s ? %s : %s)" s1 s2 s3
      | And(e1,e2) -> sprintf "(%s && %s)" (aux inliner i e1) (aux inliner i e2)
      | Or(e1,e2) -> sprintf "(%s || %s)" (aux inliner i e1) (aux inliner i e2)
      | Geq(e1,e2) -> sprintf "(%s >= %s)" (aux inliner i e1) (aux inliner i e2)
      | Gt(e1,e2) -> sprintf "(%s > %s)" (aux inliner i e1) (aux inliner i e2)
      | Leq(e1,e2) -> sprintf "(%s <= %s)" (aux inliner i e1) (aux inliner i e2)
      | Lt(e1,e2) -> sprintf "(%s < %s)" (aux inliner i e1) (aux inliner i e2)
      | Plus(e1,e2) -> sprintf "(%s + %s)" (aux inliner i e1) (aux inliner i e2)
      | Times(e1,e2) -> sprintf "(%s * %s)" (aux inliner i e1) (aux inliner i e2)
      | Ident id -> Map.find id.Name inliner
      | IntLiteral i -> sprintf "((bit<32>) %s)" (string i)
      | Matches r -> 
         let tags = tagsForRegex r ctx.Graph nodes
         if Set.isEmpty tags then "false"
         else 
            let strs = Seq.map (fun t -> sprintf "hdr.hulapp.ptag == %d" t) tags
            "(" + (Util.String.join strs " || ") + ")"
      // | Max es -> sprintf "max(%s)" (Util.String.join (List.map aux es) ",")
      // | Min es -> sprintf "min(%s)" (Util.String.join (List.map aux es) ",")
      | Tuple es -> sprintf "%s" (aux inliner i (List.item (i-1) es))
      | Not e -> sprintf "!(%s)" (aux inliner i e)
      | PathAttribute(id,tag) -> sprintf "tmp_%s%d" id.Name tag
      | _ -> failwith "Not implemented"
   aux Map.empty i ctx.Ast.OptFunction
 
let rec numberOfProbes (expr : Ast.Expr) : int = 
   let all = ref Set.empty
   Ast.iter (fun e -> 
      match e.Node with 
      | PathAttribute (_,tag) -> all := Set.add tag !all 
      | _ -> ()
   ) expr
   Set.count !all


let totalBits = ref Map.empty
let addBits device bits = 
   totalBits := Util.Map.adjust device 0 (fun c -> c + bits) !totalBits

let ingressTxt device nodes numTagBits (ctx : Context) =
   let settings = Args.getSettings()
   let tupSize = tupleSize ctx
   let ports = Map.find device ctx.PortMap
   let flowletTableSize = settings.FlowletTableSize
   let numTags = ctx.NodeMap.[device].Length
   let numProbes = ctx.NumProbes
   let numPorts = Seq.length ports // add one for host
   let numDsts = numDstSwitches ctx // add one for host
   let numPgInNeighbors = Seq.sumBy (fun n -> neighborsIn ctx.Graph n |> Seq.length) nodes
   let add = addBits device

   // Flowlet routing
   let numDstBits = 16
   let numFlowletBits = numBits (flowletTableSize * numTags)

   template (fun sb ->
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "**************  I N G R E S S   P R O C E S S I N G   *******************"
      p "*************************************************************************/"
      p ""
      p "control ingress(inout headers hdr, inout metadata meta, inout standard_metadata_t standard_metadata) {"
      p ""
      p "    // Packet Id for tracking the path"
      p "    register<bit<16>>(1) currentid;"
      p ""
      p "    // Most recent sequence number"
      p "    register<bit<32>>(%d) current_seq_no;" (numDsts * numProbes * numTags)
      add (32 * numDsts * numProbes * numTags)
      p "    register<bit<32>>(%d) decision_seq_no;" numDsts
      add (32 * numDsts)
      p ""
      p "    // Decision table"
      if (numTagBits > 0) then
         p "    register<bit<%d>>(%d) decision_tag;     // Local probe tag" numTagBits numDsts
         add (numTagBits * numDsts)
      if (ctx.NumProbes > 1) then
         p "    register<bit<%d>>(%d) decision_pid;     // Local probe id" ctx.NumProbesBits numDsts
         add (ctx.NumProbesBits * numDsts)
      for i = 1 to tupSize do 
         p "    register<bit<32>>(%d) decision_f%d;       // Function value" numDsts i
         add (32 * numDsts)
      p ""
      p "    // Choices table"
      p "    register<bit<9>>(%d) choices_nhop;      // Next hop port" (numDsts * numTags * numProbes)
      add (9 * numDsts * numTags * numProbes)
      if (numTagBits > 0) then
         p "    register<bit<%d>>(%d) choices_tag;      // Local probe tag" numTagBits (numDsts * numTags * numProbes)
         add (numTagBits * numDsts * numTags * numProbes)
      for i = 1 to tupSize do 
         p "    register<bit<32>>(%d) choices_f%d;        // Function value" (numDsts * numTags * numProbes) i
         add (32 * numDsts * numTags * numProbes)
      // If multiple probes, then we need to keep around the individual metrics
      if (ctx.NumProbes > 1) then
         for attr in ctx.Attrs do 
            match attr with 
            | "util" ->
               p "    register<bit<32>>(%d) choices_util;        // Utilization" (numDsts * numTags * numProbes)
               add (32 * numDsts * numTags * numProbes)
            | "queue" ->
               p "    register<bit<32>>(%d) choices_queue;       // Queue" (numDsts * numTags * numProbes)
               add (32 * numDsts * numTags * numProbes)
            | "length" ->
               p "    register<bit<32>>(%d) choices_length;      // Length" (numDsts * numTags * numProbes)
               add (32 * numDsts * numTags * numProbes)
            | "latency" ->
               p "    register<bit<32>>(%d) choices_latency;     // Latency" (numDsts * numTags * numProbes)
               add (32 * numDsts * numTags * numProbes)
            | _ -> failwith "unimplemented"

      p ""
      p "    // Flowlet routing table"
      p "    register<bit<9>>(%d)  flowlet_nhop;           // Flowlet next hop" (flowletTableSize * numTags * numProbes)
      add (9 * flowletTableSize * numTags * numProbes)
      p "    register<bit<%d>>(%d) flowlet_dst;            // Flowlet destination" numDstBits (flowletTableSize * numTags * numProbes)
      add (numDstBits * flowletTableSize * numTags * numProbes)
      if (numTagBits > 0) then
         p "    register<bit<%d>>(%d) flowlet_tag;            // Flowlet neighbor tag" numTagBits (flowletTableSize * numTags * numProbes)
      add (numTagBits * flowletTableSize * numTags * numProbes)
      p "    register<bit<48>>(%d) flowlet_time;           // Flowlet time of last packet" (flowletTableSize * numTags * numProbes)
      add (48 * flowletTableSize * numTags * numProbes)
      p "    register<bit<9>>(%d)  flowlet_hopcount;       // Flowlet lazy loop prevention count" flowletTableSize
      add (9 * flowletTableSize)
      p "    register<bit<1>>(%d)  flowlet_hopcount_valid; // Flowlet loop prevention valid" flowletTableSize
      add (flowletTableSize)
      p ""
      p "    // LinkTable for Failure Detection"
      p "    register<bit<48>>(%d) link_time;              // time of last probe using the link" (numPorts + 2) // add 2 for loopback port and control host port
      add (48 * (numPorts + 2))
      for attr in ctx.Attrs do 
         let s = attr
         match s with 
         | "length"
         | "util"
         | "latency" -> 
            p ""
            p "    // Metric %s" s
            // p "    register<bit<32>>(%d) best_%s;      // Best path %s per tor" numDsts s s
            p "    register<bit<32>>(%d) local_%s;     // Local %s per port." (numPorts + 1) s s
            add (32 * (numPorts + 1))
            if s = "util" then
               p "    register<bit<48>>(%d) last_packet_time;" (numPorts + 1)
               add (32 * (numPorts + 1))
         | "queue" ->
            p ""
         | _ -> failwith "Not implemented"

      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*Some basic actions*/" 
      p ""
      p "    action drop() {"
      p "        mark_to_drop();"
      p "    }" 
      p ""
      p "    action add_hulapp_header() {"
      p "        hdr.hulapp.setValid();"
      p "        //An extra hop in the probe takes up 16bits, or 1 word."
      p "        hdr.ipv4.ihl = hdr.ipv4.ihl + 1;"
      p "    }"
      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*If Hula++ probe, mcast it to the right set of next hops*/"
      p ""
      p "    // Write to the standard_metadata's mcast field!"
      p "    action set_hulapp_mcast(bit<16> mcast_id) {"
      p "      standard_metadata.mcast_grp = mcast_id;"
      p "    }"
      p ""
      p "    table tab_hulapp_mcast {"
      p "        key = {"
      if (numTagBits > 0) then
         p "          hdr.hulapp.ptag : exact;"
      p "        }"
      p "        actions = {"
      p "          set_hulapp_mcast; "
      p "          drop; "
      p "          NoAction; "
      p "        }"
      if (numTagBits > 0) then
         p "        const entries = {"
 
         for node in nodes do 
            let mcastId = ctx.McastIdMap.[node]
            p "            %d : set_hulapp_mcast(%d);" node.Id mcastId
            add (numDstBits + 16)
         p "        }"
         p "        size = %d;" (Seq.length nodes)
         p "        default_action = NoAction();"
      else
         for node in nodes do
            let mcastId = ctx.McastIdMap.[node]
            p "        default_action = set_hulapp_mcast(%d);" mcastId
      p "    }"
      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*Update the mac address based on the port*/"
      p ""
      p "    action update_macs(macAddr_t dstAddr) {"
      p "        hdr.ethernet.srcAddr = hdr.ethernet.dstAddr;"
      p "        hdr.ethernet.dstAddr = dstAddr;"
      p "    }"
      p ""
      p "    table tab_port_to_mac {"
      p "        key = {"
      p "            meta.outbound_port: exact;"
      p "        }   "
      p "        actions = {"
      p "            update_macs;"
      p "            NoAction;"
      p "        }"
      p "        size = 1024;"
      p "        default_action = NoAction();"
      p "    }"
      p ""
      add (numPorts * (48 + 9)) // we don't really need 1024 entries
      p "/*----------------------------------------------------------------------*/"
      p "/*Remove hula header between IP and TCP/UDP header*/"
      p ""
      p "    action remove_hula_tcp() {"
      p "        if (hdr.ipv4.isValid())"
      p "            hdr.ipv4.protocol = TCP_PROTOCOL;"
      p "        else if (hdr.ethernet.isValid())"
      p "            hdr.ethernet.etherType = TYPE_ARP;"
      p "        hdr.hulapp_data.setInvalid();"
      p "    }"
      p ""
      p "    action remove_hula_udp() {"
      p "        if (hdr.ipv4.isValid())"
      p "            hdr.ipv4.protocol = UDP_PROTOCOL;"
      p "        else if (hdr.ethernet.isValid())"
      p "            hdr.ethernet.etherType = TYPE_ARP;"
      p "        hdr.hulapp_data.setInvalid();"
      p "    }"
      p ""
      p "    table tab_remove_hula_header {"
      p "        key = {"
      if ctx.isNS3 then
         p "            hdr.ppp.pppType              : exact;"
      else
         p "            hdr.ethernet.etherType       : exact;"
      p "            hdr.ipv4.protocol            : ternary;"
      p "            standard_metadata.egress_spec: exact;"
      p "        }"
      p "        actions = {"
      p "            remove_hula_tcp;"
      p "            remove_hula_udp;"
      p "            NoAction;"
      p "        }"
      p "        const entries = {"
      p "            (TYPE_HULAPP_TCP_DATA, _,                        1) : remove_hula_tcp();"
      p "            (TYPE_IPV4,            HULAPP_TCP_DATA_PROTOCOL, 1) : remove_hula_tcp();"
      p "            (TYPE_HULAPP_UDP_DATA, _,                        1) : remove_hula_udp();"
      p "            (TYPE_IPV4,            HULAPP_UDP_DATA_PROTOCOL, 1) : remove_hula_udp();"
      p "        }"
      p "        size = 4;"
      p "        default_action = NoAction();"
      p "    }"
      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*At leaf switch, forward data packet to end host*/"
      p ""
      p "    action forward_to_end_hosts(egressSpec_t port) {"
      p "        standard_metadata.egress_spec = port;"
      p "    }"
      p ""
      p "    action mcast_to_all_end_hosts(bit<16> mcast_id) {"
      p "      standard_metadata.mcast_grp = mcast_id;"
      p "    }"
      p ""
      p "    table tab_forward_to_end_hosts {"
      p "        key = {"
      if ctx.isNS3 then
         p "            hdr.ppp.pppType              : exact;"
      else
         p "            hdr.ethernet.etherType       : exact;"
      p "            hdr.ipv4.protocol            : ternary;"
      p "            standard_metadata.egress_spec: exact;"
      p "            hdr.ipv4.dstAddr             : ternary;"
      p "        }"
      p "        actions = {"
      p "            forward_to_end_hosts;"
      p "            mcast_to_all_end_hosts;"
      p "            NoAction;"
      p "        }"
      p "        default_action = NoAction();"
      p "    }"
      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*Inject hula header between IP and TCP/UDP header*/"
      p ""
      p "    action inject_hula_below_ipv4_above_tcp() {"
      p "        hdr.ipv4.protocol = HULAPP_TCP_DATA_PROTOCOL;"
      p "        hdr.hulapp_data.setValid();"
      if (numTagBits > 0) then
         p "        hdr.hulapp_data.dtag = 1;"
      if ctx.isNS3 then
         p "        currentid.read(hdr.ipv4.identification, 0);"
         p "        currentid.write(0, hdr.ipv4.identification+1);"
      p "        //hdr.hulapp_data.data_id = hdr.tcp.seqNo; // use tcp.seqNo as our data_id"
      p "    }"
      p ""
      p "    action inject_hula_below_ipv4_above_udp() {"
      p "        hdr.ipv4.protocol = HULAPP_UDP_DATA_PROTOCOL;"
      p "        hdr.hulapp_data.setValid();"
      if (numTagBits > 0) then
         p "        hdr.hulapp_data.dtag = 1;"
      if ctx.isNS3 then
         p "        currentid.read(hdr.ipv4.identification, 0);"
         p "        currentid.write(0, hdr.ipv4.identification+1);"
      p "    }"
      p ""
      p "    action inject_hula_above_arp() {"
      if ctx.isNS3 then
         p "        hdr.ppp.pppType = TYPE_HULAPP_TCP_DATA;"
      else
         p "        hdr.ethernet.etherType = TYPE_HULAPP_TCP_DATA;"
      p "        hdr.hulapp_data.setValid();"
      if (numTagBits > 0) then
         p "        hdr.hulapp_data.dtag = 1;"
      p "        //hdr.hulapp_data.data_id = 0; // use tcp.seqNo as our data_id"
      p "    }"
      p ""
      p "    table tab_inject_hula_header {"
      p "        key = {"
      if ctx.isNS3 then
         p "            hdr.ppp.pppType                 : exact;"
      else
         p "            hdr.ethernet.etherType          : exact;"
      p "            hdr.ipv4.protocol               : ternary;"
      p "        }"
      p "        actions = {"
      p "            inject_hula_below_ipv4_above_tcp;"
      p "            inject_hula_below_ipv4_above_udp;"
      p "            inject_hula_above_arp;"
      p "            NoAction;"
      p "        }"
      p "        const entries = {"
      p "            (TYPE_IPV4, TCP_PROTOCOL) : inject_hula_below_ipv4_above_tcp();"
      p "            (TYPE_IPV4, UDP_PROTOCOL) : inject_hula_below_ipv4_above_udp();"
      p "            (TYPE_ARP,  _)            : inject_hula_above_arp();"
      p "        }"
      p "        size = 3;"
      p "        default_action = NoAction();"
      p "    }"
      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*Update the destination switch ID from the ip prefix*/"
      p ""
      p "    action update_id(bit<%d> id) {" numDstBits
      p "        meta.dst_switch_id = id;"
      p "    }"
      p ""
      p "    table tab_prefix_to_id {"
      p "        key = {"
      p "            meta.ipv4DstAddr: lpm;"
      p "        }   "
      p "        actions = {"
      p "            update_id;"
      p "            drop;"
      p "            NoAction;"
      p "        }"
      p "        size = 1024;"
      p "        default_action = NoAction();"
      p "    }"
      p ""
      add (numDsts * (32 + numDstBits))
      if (numTagBits > 0) then
         p "/*----------------------------------------------------------------------*/"
         p "/*Update the ptag*/"
         p ""
         p "    action update_ptag(bit<%d> ptag) {" numTagBits
         p "        hdr.hulapp.ptag = ptag;"
         p "    }"
         p ""
         p "    table tab_state_transition {"
         p "        key = {"
         p "            hdr.hulapp.ptag: exact;"
         p "        }   "
         p "        actions = {"
         p "            update_ptag;"
         p "            NoAction;"
         p "        }"
         p "        const entries = {"
   
         for node in nodes do
            let inPeers = neighborsIn ctx.Graph node
            for peer in inPeers do 
               p "            %d : update_ptag(%d);" peer.Id node.Id
               add (numTagBits * 2)

         p "        }"
         p "        size = %d;" numPgInNeighbors
         p "        default_action = NoAction();"
         p "    }"
         p ""
         p "/*----------------------------------------------------------------------*/"
         p "/*ptag to local_tag mapping*/"
         p ""
         p "    action update_local_tag(bit<%d> tag) {" numTagBits
         p "        meta.local_tag = tag;"
         p "    }"
         p ""
         p "    table tab_local_state_transition {"
         p "        key = {"
         p "            hdr.hulapp.ptag: exact;"
         p "        }   "
         p "        actions = {"
         p "            update_local_tag;"
         p "            NoAction;"
         p "        }"
         p "        const entries = {"
   
         for node in nodes do
            p "            %d : update_local_tag(%d);" node.Id node.State
            add (numTagBits * 2)
   
         p "        }"
         p "        size = %d;" (Seq.length nodes)
         p "        default_action = NoAction();"
         p "    }"
         p ""
   
         p "/*----------------------------------------------------------------------*/"
         p "/*reverse local tag to global tag*/"
         p ""
         p "    action update_local_reverse_tag(bit<%d> tag) {" numTagBits
         p "        meta.local_tag = tag;"
         p "    }"
         p ""
         p "    table tab_local_reverse_state_transition {"
         p "        key = {"
         p "            hdr.hulapp_data.dtag: exact;"
         p "        }   "
         p "        actions = {"
         p "            update_local_reverse_tag;"
         p "            NoAction;"
         p "        }"
         p "        const entries = {"
   
         for node in nodes do
            p "            %d : update_local_reverse_tag(%d);" node.Id node.State
            add (numTagBits*2)
   
         p "        }"
         p "        size = %d;" (Seq.length nodes)
         p "        default_action = NoAction();"
         p "    }"
         p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*If data traffic, do normal forwarding*/"
      p ""
      p "    action ipv4_forward(macAddr_t dstAddr, egressSpec_t port) {"
      p "        standard_metadata.egress_spec = port;"
      p "//        hdr.ethernet.srcAddr = hdr.ethernet.dstAddr;"
      p "//        hdr.ethernet.dstAddr = dstAddr;"
      p "        hdr.ipv4.ttl = hdr.ipv4.ttl - 1;"
      p "    }"
      p ""
      p "    table tab_ipv4_lpm {"
      p "        key = {"
      p "            hdr.ipv4.dstAddr: lpm;"
      p "        }   "
      p "        actions = {"
      p "            ipv4_forward;"
      p "            drop;"
      p "            NoAction;"
      p "        }"
      p "        size = 1024;"
      p "        default_action = NoAction();"
      p "    }"
      p ""
      add (1024 * 32)
      p "/*----------------------------------------------------------------------*/"
      if ctx.swidEnabled then
         p "/* add SWID between under ethernet */"
         p "    action add_switchid_tag(bit<16> sw_id) {"
         p "      hdr.switchid_tags.push_front(1);"
         p "      hdr.switchid_tags[0].setValid();"
         p "      hdr.switchid_tags[0].sw_id = sw_id;"
         p "      hdr.switchid_tags[0].type1 = hdr.ethernet.etherType;"
         p "      hdr.ethernet.etherType = TYPE_SWID;"
         p "    }"
         p ""
         p "    table swid_tag {"
         p "            key={"
         p "            hdr.ethernet.etherType          : exact;"
         p "            }"
         p "        actions = {"
         p "            add_switchid_tag;"
         p "            }"
         p "        size = 2;"
         p "        }"
         p "/*----------------------------------------------------------------------*/"
      /// Easier debugging
      if settings.Debug then 
         p "/*Table used to observe some registers' value*/"
         p ""
         p "    table tab_observe_metadata {"
         p "        key = { "
         if ctx.isNS3 then
            p "            standard_metadata.ns3_node_id: ternary;"
         p "            meta.debug_probe_seq_no: ternary;"
         p "            meta.debug_probe_id: ternary;"
         p "            meta.debug_probe_port: ternary;"
         p "            meta.debug_probe_dst_tor: ternary;"
         if (numTagBits > 0) then
            p "            meta.debug_probe_ptag: ternary;"
            p "            meta.debug_probe_new_ptag: ternary;"
            p "            meta.debug_probe_local_ptag: ternary;"
         p "            meta.debug_pkt_ingress_port: ternary;"
         p "            meta.debug_pkt_egress_port: ternary;"
         p "            meta.debug_pkt_data_id: ternary;"
         if (numTagBits > 0) then
            p "            meta.debug_pkt_tag: ternary;"
            p "            meta.debug_pkt_new_tag: ternary;"
            p "            meta.debug_pkt_local_tag: ternary;"
         p "            meta.debug_pkt_fidx: ternary;"
         p "            meta.debug_pkt_flowlet_create: ternary;"
         p "            meta.debug_pkt_flowlet_cached: ternary;"
         p "            meta.debug_pkt_flowlet_thrash: ternary;"
         p "            meta.debug_pkt_flowlet_maybe_loop: ternary;"
         p "            meta.debug_pkt_flowlet_different_nhop: ternary;"
         p "            meta.debug_pkt_flowlet_fvalid: ternary;"
         p "            meta.debug_pkt_flowlet_time_expired: ternary;"
         p "            meta.debug_pkt_flowlet_hop_count: ternary;"
         p "            meta.debug_pkt_flowlet_cport: ternary;"
         p "            meta.debug_pkt_flowlet_fport: ternary;"
         p "            hdr.ipv4.dstAddr: ternary;"
         p "            hdr.ipv4.srcAddr: ternary;"
         p "            hdr.tcp.srcPort: ternary;"
         p "            hdr.tcp.dstPort: ternary;"
         p "            hdr.ipv4.identification : ternary;"

         if hasAttr ctx "util" then 
            p "            meta.debug_probe_util: ternary;"
            p "            meta.debug_pkt_util: ternary;"
            p "            meta.debug_pkt_time: ternary;"
         if hasAttr ctx "queue" then
            p "            meta.debug_probe_queue: ternary;"
         if hasAttr ctx "length" then 
            p "            meta.debug_probe_length: ternary;"
         if hasAttr ctx "latency" then 
            p "            meta.debug_probe_latency: ternary;"
         p "            meta.debug_probe_cidx: ternary;"
         for i = 1 to tupSize do
            p "            meta.debug_probe_decision_f%d: ternary;" i
         for i = 1 to tupSize do
            p "            meta.debug_probe_decision_x%d: ternary;" i
         for i = 1 to tupSize do
            p "            meta.debug_probe_f%d: ternary;" i
         for i = 1 to tupSize do
            p "            meta.debug_probe_x%d: ternary;" i

         p "            meta.debug_probe_choice_update: ternary;"
         p "            meta.debug_probe_decision_update: ternary;"

         p "        }"
         p "        actions = {"
         p "            NoAction;"
         p "        }"
         p "        default_action = NoAction();"
         p "    }"

         p "/*Table used to observe some registers' value*/"
         p ""
         p "    table tab_observe_metadata2 {"
         p "        key = { "
         if ctx.isNS3 then
            p "            standard_metadata.ns3_node_id: ternary;"
         p "            meta.debug_probe_seq_no: ternary;"
         p "            meta.debug_probe_id: ternary;"
         p "            meta.debug_probe_port: ternary;"
         p "            meta.debug_probe_dst_tor: ternary;"
         if (numTagBits > 0) then
            p "            meta.debug_probe_ptag: ternary;"
            p "            meta.debug_probe_new_ptag: ternary;"
            p "            meta.debug_probe_local_ptag: ternary;"
         p "            meta.debug_pkt_ingress_port: ternary;"
         p "            meta.debug_pkt_egress_port: ternary;"
         p "            meta.debug_pkt_data_id: ternary;"
         if (numTagBits > 0) then
            p "            meta.debug_pkt_tag: ternary;"
            p "            meta.debug_pkt_new_tag: ternary;"
            p "            meta.debug_pkt_local_tag: ternary;"
         p "            meta.debug_pkt_fidx: ternary;"
         p "            meta.debug_pkt_flowlet_create: ternary;"
         p "            meta.debug_pkt_flowlet_cached: ternary;"
         p "            meta.debug_pkt_flowlet_thrash: ternary;"
         p "            meta.debug_pkt_flowlet_maybe_loop: ternary;"
         p "            meta.debug_pkt_flowlet_different_nhop: ternary;"
         p "            meta.debug_pkt_flowlet_fvalid: ternary;"
         p "            meta.debug_pkt_flowlet_time_expired: ternary;"
         p "            meta.debug_pkt_flowlet_hop_count: ternary;"
         p "            meta.debug_pkt_flowlet_cport: ternary;"
         p "            meta.debug_pkt_flowlet_fport: ternary;"
         p "            hdr.ipv4.dstAddr: ternary;"
         p "            hdr.ipv4.srcAddr: ternary;"
         p "            hdr.tcp.srcPort: ternary;"
         p "            hdr.tcp.dstPort: ternary;"
         p "            hdr.ipv4.identification : ternary;"

         if hasAttr ctx "util" then
            p "            meta.debug_probe_util: ternary;"
            p "            meta.debug_pkt_util: ternary;"
            p "            meta.debug_pkt_time: ternary;"
         if hasAttr ctx "queue" then
            p "            meta.debug_probe_queue: ternary;"
         if hasAttr ctx "length" then
            p "            meta.debug_probe_length: ternary;"
         if hasAttr ctx "latency" then 
            p "            meta.debug_probe_latency: ternary;"
         p "            meta.debug_probe_cidx: ternary;"
         for i = 1 to tupSize do
            p "            meta.debug_probe_decision_f%d: ternary;" i
         for i = 1 to tupSize do
            p "            meta.debug_probe_decision_x%d: ternary;" i
         for i = 1 to tupSize do
            p "            meta.debug_probe_f%d: ternary;" i
         for i = 1 to tupSize do
            p "            meta.debug_probe_x%d: ternary;" i
         p "            meta.debug_probe_choice_update: ternary;"
         p "            meta.debug_probe_decision_update: ternary;"

         p "        }"
         p "        actions = {"
         p "            NoAction;"
         p "        }"
         p "        default_action = NoAction();"
         p "    }"


      p ""
      p "/*----------------------------------------------------------------------*/"
      p "/*Applying the tables*/"
      p ""
      p "    apply {"
      p ""

      for attr in ctx.Attrs do
         if attr = "length" then
            for (Index(i), _) in ports do
               p "//        local_length.write((bit<32>) %d, 1);" (i-2)
      p ""
      p "        bit<9> hop_count = 255 - (bit<9>) hdr.ipv4.ttl;"
      p ""

      p "        if (hdr.hulapp_data.isValid()"
      p "            || (hdr.tcp.isValid() || hdr.udp.isValid())"
      p "                 && (standard_metadata.ingress_port == 1"
      p "                     || standard_metadata.ingress_port >= %d)          // TCP/UDP data packet from outside" (numPorts+2)
      p "            || hdr.arp_ipv4.isValid()"
      p "                 && (standard_metadata.ingress_port == 1"
      p "                     || standard_metadata.ingress_port >= %d)) {  // arp request/reply from outside" (numPorts+2)

      if settings.Debug then 
         p "            meta.debug_pkt_ingress_port = standard_metadata.ingress_port;"
         if (numTagBits > 0) then
            p "            meta.debug_pkt_tag = hdr.hulapp_data.dtag;"
         p ""

      p "            tab_inject_hula_header.apply();"
      p "            tab_prefix_to_id.apply();"
      p "            bit<32> dst = (bit<32>) meta.dst_switch_id;"
      if (numTagBits > 0) then
         p "            // Get the local tag"
         p "            if (standard_metadata.ingress_port == 1"
         p "                || standard_metadata.ingress_port >= %d) {" (numPorts+2)
         p "                decision_tag.read(meta.local_tag, dst);"
         if (ctx.NumProbes > 1) then
            p "                    decision_pid.read(hdr.hulapp_data.probe_id, dst);"
         p "            } else {"
         p "                tab_local_reverse_state_transition.apply();"
         p "            }"
         if settings.Debug then
            p "            meta.debug_pkt_local_tag = meta.local_tag;"

      p ""
      p "            if (hdr.ipv4.isValid()) {"
      p "                bit<16> srcPort;"
      p "                bit<16> dstPort;"
      p "                if (hdr.tcp.isValid()) {"
      p "                    srcPort = hdr.tcp.srcPort;"
      p "                    dstPort = hdr.tcp.dstPort;"
      p "                } else {"
      p "                    srcPort = hdr.udp.srcPort;"
      p "                    dstPort = hdr.udp.dstPort;"
      p "                }"
      p "                // Compute flowlet hash index"
      p "                bit<32> hash_index;"
      p "                hash(hash_index, "
      p "                     HashAlgorithm.crc32,"
      p "                     (bit<%d>) 0," numFlowletBits
      p "                     { hdr.ipv4.srcAddr,"
      p "                       hdr.ipv4.dstAddr,"
      p "                       hdr.ipv4.protocol,"
      p "                       srcPort,"
      p "                       dstPort },"
      p "                     (bit<32>) %d);" (flowletTableSize-1)
      p ""
      if settings.Debug then 
          p "                //meta.debug_pkt_data_id = hdr.hulapp_data.data_id;"
          p "                //meta.debug_pkt_data_id = meta.debug_pkt_data_id + (bit<32>) hash_index;"
          p ""
          p "                meta.debug_pkt_flowlet_create = false;"
          p "                meta.debug_pkt_flowlet_cached = false;"
          p "                meta.debug_pkt_flowlet_thrash = false;"
      p ""
      p "                // Compute flowlet information"
      let x = (flowletTableSize * numTags)
      let y = flowletTableSize
      let str = sprintf "(bit<32>) hdr.hulapp_data.probe_id * %d + " x
      let probeMath = (if ctx.NumProbes > 1 then str else "")
      if (numTagBits > 0) then
         p "                bit<32> fidx = %s(bit<%d>) meta.local_tag * %d + hash_index;" probeMath numTagBits y
      else
         p "                bit<32> fidx = %shash_index;" probeMath
      p ""

      if settings.Debug then 
          p "                meta.debug_pkt_fidx = fidx;"
          p ""

      p "                bit<48> ftime;"
      p "                bit<%d> fdst;" numDstBits
      if (numTagBits > 0) then
         p "                bit<%d> ftag;" numTagBits
      p "                bit<9>  fport;"
      p "                bit<9>  fcount;"
      p "                bit<1>  fvalid;"
      p "                flowlet_time.read(ftime, fidx);"
      p "                flowlet_dst.read(fdst, fidx);"
      if (numTagBits > 0) then
         p "                flowlet_tag.read(ftag, fidx);"
      p "                flowlet_nhop.read(fport, fidx);"
      p "                flowlet_hopcount.read(fcount, hash_index);"
      p "                flowlet_hopcount_valid.read(fvalid, hash_index);"
      p ""
      p "                // Update min hop count if necessary"
      p "                if (hop_count < fcount) {"
      p "                    flowlet_hopcount_valid.write(hash_index, 1);"
      p "                    flowlet_hopcount.write(hash_index, hop_count);"
      p "                }"
      p ""
      let xc = (numDsts * numTags)
      let yc = numDsts
      let strc = sprintf "(bit<32>) hdr.hulapp_data.probe_id * %d + " xc
      let probeMathC = (if ctx.NumProbes > 1 then strc else "")
      p "                // Compute the best choices entry information"
      if (numTagBits > 0) then
         p "                bit<32> cidx = %s(bit<32>) meta.local_tag * %d + dst;" probeMathC yc
      else
         p "                bit<32> cidx = %sdst;" probeMathC
      p "                bit<9> cport;"
      if (numTagBits > 0) then
         p "                bit<%d> ctag;" numTagBits
      p "                choices_nhop.read(cport, cidx);"
      if (numTagBits > 0) then
         p "                choices_tag.read(ctag, cidx);"
      p ""
      p "                // Check link timeout for link failure detection"
      p "                bit<48> ltime;"
      p "                link_time.read(ltime, (bit<32>) fport);"
      p ""
      p "                // Check various conditions"
      p "                bit<48> flowlet_elapsed = standard_metadata.ingress_global_timestamp - ftime;"
      p "                bool initial_time = (ftime == 0);"
      p "                bool maybe_loop = (fcount + LOOP_THRESHOLD < hop_count) || (standard_metadata.ingress_port == fport);"
      p "                bool different_nhop = (cport != fport);"
      p "                bool needs_flush = (fvalid == 1) && maybe_loop && different_nhop;"
      p "                bool time_expired = initial_time || (flowlet_elapsed > FLOWLET_TIMEOUT);"
      p "                bool link_failed = (standard_metadata.ingress_global_timestamp - ltime > LINK_TIMEOUT);"
      p ""
      if settings.Debug then
          p "                meta.debug_pkt_flowlet_maybe_loop = maybe_loop;"
          p "                meta.debug_pkt_flowlet_different_nhop = different_nhop;"
          p "                meta.debug_pkt_flowlet_fvalid = fvalid;"
          p "                meta.debug_pkt_flowlet_time_expired = time_expired;"
          p "                meta.debug_pkt_flowlet_hop_count = hop_count;"
          p "                meta.debug_pkt_flowlet_cport = cport;"
          p "                meta.debug_pkt_flowlet_fport = fport;"
          p ""
      p "                if (!time_expired && !needs_flush && dst == (bit<32>) fdst && !link_failed) {"
      if settings.Debug then
          p "                    meta.debug_pkt_flowlet_cached = true;"
      p "                    standard_metadata.egress_spec = fport;"
      if (numTagBits > 0) then
          p "                    hdr.hulapp_data.dtag = ftag;"
      p "                    flowlet_time.write(fidx, standard_metadata.ingress_global_timestamp);"
      p ""
      p "                } else {"

      // Lookup next hop
      p "                    // We use the choices table to lookup the next hop"
 
      p "                    standard_metadata.egress_spec = cport;"
      if (numTagBits > 0) then
          p "                    hdr.hulapp_data.dtag = ctag;"
      p ""

      // Possibly update flowlet routing
      p "                    // Update flowlet table if expired"
      p "                    if (time_expired || needs_flush || link_failed) {"
      if settings.Debug then
          p "                        meta.debug_pkt_flowlet_create = true;"
      p "                        flowlet_time.write(fidx, standard_metadata.ingress_global_timestamp);"
      if (numTagBits > 0) then
          p "                        flowlet_tag.write(fidx, hdr.hulapp_data.dtag);"
      p "                        flowlet_dst.write(fidx, (bit<%d>) dst);" numDstBits
      p "                        flowlet_nhop.write(fidx, standard_metadata.egress_spec);"
      p "                        flowlet_hopcount.write(hash_index, hop_count);"
      p "                        flowlet_hopcount_valid.write(hash_index, 1);"
      p "                    }"
      if settings.Debug then
          p "                    else {"
          p "                        meta.debug_pkt_flowlet_thrash = true;"
          p "                    }"
      p "                }"

      if settings.Debug && (numTagBits > 0) then
         p "                meta.debug_pkt_new_tag = hdr.hulapp_data.dtag;"
      p "                // Remember the outbound port for mac translation"
      p "                meta.outbound_port = standard_metadata.egress_spec;"
      p ""
      p "            } else {  // no ip header => arp req/reply => forward without flowlet routing"
      if (numTagBits > 0) then
         p "                bit<32> cidx = meta.local_tag * 10 + dst;"
      else
         p "                bit<32> cidx = dst;"
      p "                choices_nhop.read(standard_metadata.egress_spec, cidx);"
      if (numTagBits > 0) then
         p "                choices_tag.read(hdr.hulapp_data.dtag, cidx);"
      p ""
      p "                // Remember the outbound port for mac translation"
      p "                meta.outbound_port = standard_metadata.egress_spec;"
      p "            }"

      if hasAttr ctx "util" then
         p ""
         p "            // Update the path utilization if necessary"
         p "            if (standard_metadata.egress_spec != 1) {"
         p "                bit<32> tmp_util = 0;"
         p "                bit<48> tmp_time = 0;"
         p "                bit<32> time_diff = 0;"
         p "                local_util.read(tmp_util, (bit<32>) standard_metadata.egress_spec - 2);"
         p "                last_packet_time.read(tmp_time, (bit<32>) standard_metadata.egress_spec - 2);"
         p "                time_diff = (bit<32>)(standard_metadata.ingress_global_timestamp - tmp_time);"
         p "                bit<32> temp = tmp_util*time_diff;"
         p "                tmp_util = time_diff > UTIL_RESET_TIME_THRESHOLD ?"
         p "                           0 : standard_metadata.packet_length + tmp_util - (temp >> TAU_EXPONENT);"
         p "                last_packet_time.write((bit<32>) standard_metadata.egress_spec - 2,"
         p "                                       standard_metadata.ingress_global_timestamp);"
         p "                local_util.write((bit<32>) standard_metadata.egress_spec - 2, tmp_util);"
         p ""
         if settings.Debug then
            p "                meta.debug_pkt_util = tmp_util;"
            p "                meta.debug_pkt_time = standard_metadata.ingress_global_timestamp;"
         p "            }"
         p ""

      p "            //tab_port_to_mac.apply();"
      p "            if (hdr.ipv4.isValid()) {"
      p "                hdr.ipv4.ttl = hdr.ipv4.ttl - 1;"
      if ctx.swidEnabled then
         p "                swid_tag.apply();"
      p "            }"
      if settings.Debug then
         p "            meta.debug_pkt_egress_port = standard_metadata.egress_spec;"
         p "            tab_observe_metadata.apply();"
      p ""
      if ctx.swidEnabled then
         p "            // Remove all tags from packets at last hop"
         p "            if(hdr.ipv4.isValid() && standard_metadata.egress_spec == 1){"
         for i in 1..26 do
            p "                if(hdr.ethernet.etherType==TYPE_SWID){"
            p "                      hdr.ethernet.etherType=hdr.switchid_tags[0].type1;"
            p "                      hdr.switchid_tags.pop_front(1);"
            p "                }"
         p "            }"
         p ""
      p "            tab_remove_hula_header.apply();"
      p ""
      p "            tab_forward_to_end_hosts.apply();"
      p "        } // end of processing hulapp_data"
      p ""
      let x = (numDsts * numTags)
      let str = sprintf "(bit<32>) hdr.hulapp.probe_id * %d + " x
      let probeMath = (if ctx.NumProbes > 1 then str else "")
      p "        else if (hdr.ipv4.isValid()) { // processing probe and background traffic"
      p ""

      p "            if (hdr.ipv4.protocol == HULAPP_BACKGROUND_PROTOCOL && standard_metadata.ingress_port == 1) {"
      p "                standard_metadata.egress_spec = (bit<9>)hdr.hulapp_background.port;"
      p "                hdr.ipv4.ttl = hdr.ipv4.ttl - 1;"
      p ""

      for attr in ctx.Attrs do
         match attr with 
         | "queue" ->
            p ""
         | "length" ->
            p ""
         | "util" -> 
            p ""
            p "                // Update the path utilization if necessary"
            p "                if (standard_metadata.egress_spec != 1) {"
            p "                    bit<32> tmp_util = 0;"
            p "                    bit<48> tmp_time = 0;"
            p "                    bit<32> time_diff = 0;"
            p "                    local_util.read(tmp_util, (bit<32>) standard_metadata.egress_spec - 2);"
            p "                    last_packet_time.read(tmp_time, (bit<32>) standard_metadata.egress_spec - 2);"
            p "                    time_diff = (bit<32>)(standard_metadata.ingress_global_timestamp - tmp_time);"
            p "                    bit<32> temp = tmp_util*time_diff;"
            p "                    tmp_util = time_diff > UTIL_RESET_TIME_THRESHOLD ?"
            p "                               0 : standard_metadata.packet_length + tmp_util - (temp >> TAU_EXPONENT);"
            p "                    last_packet_time.write((bit<32>) standard_metadata.egress_spec - 2,"
            p "                                           standard_metadata.ingress_global_timestamp);"
            p "                    local_util.write((bit<32>) standard_metadata.egress_spec - 2, tmp_util);"
            p ""
            if settings.Debug then
               p "                    meta.debug_pkt_util = tmp_util;"
               p "                    meta.debug_pkt_time = standard_metadata.ingress_global_timestamp;"
            p "                }"
            p ""
         | "latency" ->
            p ""
         | _ -> failwith "Not implemented"
      p "            }"
      p ""
      p "            else if (hdr.ipv4.protocol == HULAPP_PROTOCOL) {"
      p ""

      if settings.Debug then 
         p "                meta.debug_probe_seq_no = hdr.hulapp.seq_no;"
         if ctx.NumProbes > 1 then
            p "                meta.debug_probe_id = hdr.hulapp.probe_id;"
         p "                meta.debug_probe_port = standard_metadata.ingress_port;"
         if (numTagBits > 0) then
            p "                meta.debug_probe_ptag = hdr.hulapp.ptag;"
         p "                meta.debug_probe_dst_tor = hdr.hulapp.dst_tor;"
         p ""

      p "                // Update LinkTable for Link Failure Detection"
      p "                link_time.write((bit<32>) standard_metadata.ingress_port, standard_metadata.ingress_global_timestamp);"
      p ""
      if (numTagBits > 0) then
         p "                meta.original_tag = hdr.hulapp.ptag;"
         p "                tab_state_transition.apply();"
         p "                tab_local_state_transition.apply();"
         p ""    
      if settings.Debug && (numTagBits > 0) then 
         p "                meta.debug_probe_new_ptag = hdr.hulapp.ptag;"
         p "                meta.debug_probe_local_ptag = meta.local_tag;"
         p ""
      for attr in ctx.Attrs do 
         match attr with 
         | "queue" ->
             p "                // Update Queue"
             p "                bit<32> queue = 0;"
             p "                if (standard_metadata.ingress_port != 1) {"
             p "                    local_queue.read(queue, (bit<32>) standard_metadata.ingress_port - 2);"
             p "                }"
             p "                hdr.hulapp.queue = hdr.hulapp.queue + queue;"
             if settings.Debug then
                p "                meta.debug_probe_queue = hdr.hulapp.queue;"
             p ""
         | "length" ->
             p "                // Update Length"
             p "                bit<32> len = 0;"
             p "                if (standard_metadata.ingress_port != 1) {"
             p "                    local_length.read(len, (bit<32>) standard_metadata.ingress_port - 2);"
             p "                }"
             p "                hdr.hulapp.length = hdr.hulapp.length + len;"
             if settings.Debug then
                p "                meta.debug_probe_length = hdr.hulapp.length;"
             p ""
         | "util" ->
             p "                // Update Util"
             p "                bit<32> tmp_util = 0;"
             p "                bit<48> tmp_time;"
             p "                if (standard_metadata.ingress_port != 1) {"
             p "                    local_util.read(tmp_util, (bit<32>) standard_metadata.ingress_port - 2);"
             p "                    last_packet_time.read(tmp_time, (bit<32>) standard_metadata.ingress_port - 2);"
             p "                    if ((bit<32>)(standard_metadata.ingress_global_timestamp - tmp_time) > UTIL_RESET_TIME_THRESHOLD)"
             p "                        tmp_util = 0;"
             p "                }"
             p "                hdr.hulapp.util = hdr.hulapp.util > tmp_util ? hdr.hulapp.util : tmp_util;"
             if settings.Debug then
                p "                meta.debug_probe_util = hdr.hulapp.util;"
             p ""
         | "latency" ->
             p "                // Update Latency"
             p "                bit<32> tmp_latency = 0;"
             p "                if (standard_metadata.ingress_port != 1) {"
             p "                    local_latency.read(tmp_latency, (bit<32>) standard_metadata.ingress_port - 2);"
             p "                }"
             p "                hdr.hulapp.latency = hdr.hulapp.latency + tmp_latency;"
             if settings.Debug then
                p "                meta.debug_probe_latency = hdr.hulapp.latency;"
             p ""
         | _ -> failwith "Not Implemented"

      // Generate locals for when there are multiple probes
      p "                bit<32> dst = (bit<32>) hdr.hulapp.dst_tor;"
      p ""
      p "                // Compute the overall decision function f"

      if (numProbes = 1) then 
         if hasAttr ctx "util" then 
             p "                bit<32> tmp_util0 = hdr.hulapp.util;"
         if hasAttr ctx "queue" then
             p "                bit<32> tmp_queue0 = hdr.hulapp.queue;"
         if hasAttr ctx "length" then 
             p "                bit<32> tmp_length0 = hdr.hulapp.length;"
         if hasAttr ctx "latency" then
             p "                bit<32> tmp_latency0 = hdr.hulapp.latency;"
      else
         p "                bit<32> idx;"
         for pid = 0 to numProbes - 1 do
            if hasAttr ctx "util" then 
               p "                bit<32> tmp_util%d;" pid
         for pid = 0 to numProbes - 1 do
            if hasAttr ctx "queue" then
               p "                bit<32> tmp_queue%d;" pid
         for pid = 0 to numProbes - 1 do
            if hasAttr ctx "length" then
               p "                bit<32> tmp_length%d;" pid
         for pid = 0 to numProbes - 1 do
            if hasAttr ctx "latency" then
               p "                bit<32> tmp_latency%d;" pid
         for pid = 0 to numProbes - 1 do
            p "                if (hdr.hulapp.probe_id == %d) {" pid
            if hasAttr ctx "util" then 
                p "                    tmp_util%d = hdr.hulapp.util;" pid
            if hasAttr ctx "queue" then
                p "                    tmp_queue%d = hdr.hulapp.queue;" pid
            if hasAttr ctx "length" then
                p "                    tmp_length%d = hdr.hulapp.length;" pid
            if hasAttr ctx "latency" then
                p "                    tmp_latency%d = hdr.hulapp.latency;" pid
            p "                } else {"
            if (numTagBits > 0) then
               p "                    idx = %d * %d + (bit<32>) meta.local_tag * %d + dst;" pid x y
            else
               p "                    idx = %d * %d + dst;" pid x
            if hasAttr ctx "util" then 
               p "                    choices_util.read(tmp_util%d, idx);" pid
            if hasAttr ctx "queue" then
               p "                    choices_queue.read(tmp_queue%d, idx);" pid
            if hasAttr ctx "length" then
               p "                    choices_length.read(tmp_length%d, idx);" pid
            if hasAttr ctx "latency" then
               p "                    choices_latency.read(tmp_latency%d, idx);" pid
            p "                }"
      p ""

      for i = 1 to tupSize do 
         p "                bit<32> probe_decision_f%d = %s;" i (generateOptFunction nodes ctx i)

      p ""
      p "                // Compute the probe-local, choices evaluation function"

      for i = 1 to tupSize do 
         p "                bit<32> probe_f%d = 9999999;" i
      if (numProbes > 1) then 
         for pid = 0 to numProbes - 1 do
            let attrs = ctx.ProbeMap.[pid]
            p "                if (hdr.hulapp.probe_id == %d) {" pid
            let mutable i = 0
            for attr in attrs do 
               i <- i + 1
               match attr with 
               | Isotonicity.Constant -> 
                  p "                    probe_f%d = 0;" i
               | Isotonicity.Attribute s -> 
                  p "                    probe_f%d = hdr.hulapp.%s;" i s
            p "                }"
      else 
         let attrs = ctx.ProbeMap.[0]
         let mutable i = 0
         for attr in attrs do 
            i <- i + 1
            match attr with 
            | Isotonicity.Constant -> 
               p "                    probe_f%d = 0;" i
            | Isotonicity.Attribute s -> 
               p "                    probe_f%d = hdr.hulapp.%s;" i s


      p ""
      p "                bit<1> update = 0;"
      p ""
      p "                // Update choices table"
      for i = 1 to tupSize do
         p "                bit<32> x%d;" i
      p "                bit<32> y;"
      if (numTagBits > 0) then
         p "                bit<32> cidx = %s(bit<32>) meta.local_tag * %d + dst;" probeMath numDsts
      else
         p "                bit<32> cidx = %sdst;" probeMath
      p "                current_seq_no.read(y, cidx);"
      for i = 1 to tupSize do 
          p "                choices_f%d.read(x%d, cidx);" i i

      p "                bool eq_seq = (hdr.hulapp.seq_no == y);"
      p "                bool gt_seq = (hdr.hulapp.seq_no > y);"
      p "                bool better_f = %s;" (createComparison tupSize "probe_f" "x")
      p "                if ((eq_seq && better_f) || gt_seq) {"
      p "                    choices_nhop.write(cidx, standard_metadata.ingress_port);"
      if (numTagBits > 0) then
         p "                    choices_tag.write(cidx, meta.original_tag);"
      for i = 1 to tupSize do 
         p "                    choices_f%d.write(cidx, probe_f%d);" i i

      if (numProbes > 1) then 
         if hasAttr ctx "util" then 
            p "                    choices_util.write(cidx, hdr.hulapp.util);"
         if hasAttr ctx "queue" then
            p "                    choices_queue.write(cidx, hdr.hulapp.queue);"
         if hasAttr ctx "length" then 
            p "                    choices_length.write(cidx, hdr.hulapp.length);"
         if hasAttr ctx "latency" then 
            p "                    choices_latency.write(cidx, hdr.hulapp.latency);"

//      if (numProbes > 1) then
//         p "                    bit<32> sidx = (bit<32>) hdr.hulapp.probe_id * %d + dst;" numDsts
//      else
//         p "                    bit<32> sidx = dst;"
      p "                    current_seq_no.write(cidx, hdr.hulapp.seq_no);"
      p "                    update = 1;"
      if settings.Debug then
         p "                    meta.debug_probe_choice_update = 1;"
      p "                }"
      if settings.Debug then
         p "                // Debug"
         p "                meta.debug_probe_cidx = cidx;"
         for i = 1 to tupleSize ctx do
            p "                meta.debug_probe_x%d = x%d;" i i
            p "                meta.debug_probe_f%d = probe_f%d;" i i
         p ""

      p ""
      p "                // Update decision table"
      p "                decision_seq_no.read(y, (bit<32>) hdr.hulapp.dst_tor);"
      p "                eq_seq = (hdr.hulapp.seq_no == y);"
      p "                gt_seq = (hdr.hulapp.seq_no > y);"
      for i = 1 to tupSize do 
         p "                decision_f%d.read(x%d, dst);" i i
      p "                better_f = %s;" (createComparison tupSize "probe_decision_f" "x")
      p "                if ((eq_seq && better_f) || gt_seq) {"
      if (numTagBits > 0) then
         p "                    decision_tag.write(dst, meta.local_tag);"
      if (ctx.NumProbes > 1) then
         p "                    decision_pid.write(dst, hdr.hulapp.probe_id);"
      for i = 1 to tupSize do 
         p "                    decision_f%d.write(dst, probe_decision_f%d);" i i
      p "                    decision_seq_no.write((bit<32>) hdr.hulapp.dst_tor, hdr.hulapp.seq_no);"
      p "                    update = 1;"
      if settings.Debug then
         p "                    meta.debug_probe_decision_update = 1;"
      p "                }"
      p ""
      
      let settings = Args.getSettings()
      if settings.Debug then
         p "                // Debug"
         for i = 1 to tupleSize ctx do
            p "                meta.debug_probe_decision_x%d = x%d;" i i
            p "                meta.debug_probe_decision_f%d = probe_decision_f%d;" i i
         p ""
      if settings.Debug then
         p "                    tab_observe_metadata2.apply();"
      p "                //Multicast the Hula++ probe "
      p "                if (update == 1) {"
      p "                    tab_hulapp_mcast.apply();"
      p "                } else {"
      p "                    mark_to_drop();"
      p "                }"
      p "            }"
      if hasAttr ctx "latency" then
         p "            else if (hdr.hulapp_ping.isValid()) {"
         p "                if (standard_metadata.ingress_port == 1) {   // sender part"
         p "                    hdr.hulapp_ping.timestamp = standard_metadata.ingress_global_timestamp;"
         p "                    standard_metadata.mcast_grp = 1;"
         p "                } else {                                     // receiver part"
         p "                    hdr.hulapp_pong.setValid();"
         p "                    hdr.ipv4.protocol = HULAPP_PONG_PROTOCOL;"
         p "                    hdr.hulapp_pong.latency = (bit<32>)(standard_metadata.ingress_global_timestamp - hdr.hulapp_ping.timestamp);"
         p "                    standard_metadata.egress_port = standard_metadata.ingress_port;"
         p "                    hdr.hulapp_ping.setInvalid();"
         p "                }"
         p "            } else if (hdr.hulapp_pong.isValid()) {"
         p "                // E' = aL + (1-a)E = E + a (L-E), where L is new latency value, E is old estimated latency"
         p "                bit<32> tmp_latency = 0;"
         p "                int<32> latency_diff;"
         p "                local_latency.read(tmp_latency, (bit<32>) standard_metadata.ingress_port - 2);"
         p "                latency_diff = (int<32>) hdr.hulapp_pong.latency - (int<32>) tmp_latency;  // L - E"
         p "                tmp_latency = (bit<32>)((int<32>)tmp_latency + (latency_diff >> ALPHA_EXPONENT)); // E' = E + a(L-E)"
         p "                local_latency.write((bit<32>) standard_metadata.ingress_port - 2, tmp_latency);"
         p "                drop();"
         p "            }"
      p ""
      p "            else {  // not hulapp_data, not hulapp probe, not background, not hula ping/pong, but with ipv4 header => should not happen in our test"
      p "                tab_ipv4_lpm.apply();"
      p "                drop();"
      p "            }"
      p ""
      p "        }"
      p ""
      p "        if (standard_metadata.egress_spec == 0 && standard_metadata.mcast_grp == 0) // avoid loopback"
      p "        {"
      p "            drop();"
      p "        }"
      p "    }"
      p ""
      p "/*----------------------------------------------------------------------*/"
      p ""
      p "}"
      p ""
   )

let egressTxt (ctx : Context) =
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "****************  E G R E S S   P R O C E S S I N G   *******************"
      p "*************************************************************************/"
      p ""
      p "control egress(inout headers hdr, inout metadata meta, inout standard_metadata_t standard_metadata) {"
      p "    apply {"
      if hasAttr ctx "queue" then
         p "       // Update queue depth"
         p "       if (hdr.ipv4.isValid()"
         p "           && ( hdr.ipv4.protocol == HULAPP_DATA_PROTOCOL"
         p "                || hdr.ipv4.protocol == HULAPP_BACKGROUND_PROTOCOL"
         p "                   && standard_metadata.ingress_port == 1)"
         p "           && standard_metadata.egress_spec > 1) {"
         p "           local_queue.write((bit<32>) standard_metadata.egress_spec - 2,"
         p "                             (bit<32>) standard_metadata.deq_qdepth);"
         p "       }"
         p ""
      p "    }"
      p "}"
      p ""
   )

let checksumTxt = 
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "*************   C H E C K S U M    C O M P U T A T I O N   **************"
      p "*************************************************************************/"
      p ""
      p "control computeChecksum("
      p "inout headers  hdr,"
      p "inout metadata meta)"
      p "{"
      p "    apply {"
      p "        update_checksum("
      p "            hdr.ipv4.isValid(),"
      p "            { hdr.ipv4.version,"
      p "              hdr.ipv4.ihl,"
      p "              hdr.ipv4.diffserv,"
      p "              hdr.ipv4.totalLen,"
      p "              hdr.ipv4.identification,"
      p "              hdr.ipv4.flags,"
      p "              hdr.ipv4.fragOffset,"
      p "              hdr.ipv4.ttl,"
      p "              hdr.ipv4.protocol,"
      p "              hdr.ipv4.srcAddr,"
      p "              hdr.ipv4.dstAddr },"
      p "            hdr.ipv4.hdrChecksum,"
      p "            HashAlgorithm.csum16);"
      p "    }"
      p "}"
      p ""
   )

let deparserTxt (ctx : Context) = 
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "***********************  D E P A R S E R  *******************************"
      p "*************************************************************************/"
      p ""
      p "control DeparserImpl(packet_out packet, in headers hdr) {"
      p "    apply {"
      p "        packet.emit(hdr.ethernet);"
      if ctx.swidEnabled then
         p "        packet.emit(hdr.switchid_tags);"
      p "        packet.emit(hdr.ppp);"
      p "        packet.emit(hdr.ipv4);"
      p "        packet.emit(hdr.hulapp_background);"
      p "        packet.emit(hdr.hulapp_data);"
      p "        packet.emit(hdr.hulapp);"
      if hasAttr ctx "latency" then
         p "        packet.emit(hdr.hulapp_ping);"
         p "        packet.emit(hdr.hulapp_pong);"
      p "        packet.emit(hdr.arp);"
      p "        packet.emit(hdr.arp_ipv4);"
      p "        packet.emit(hdr.tcp);"
      p "        packet.emit(hdr.udp);"
      p "    }"
      p "}"
      p ""
   )

let switchTxt = 
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "/*************************************************************************"
      p "***************************  S W I T C H  *******************************"
      p "*************************************************************************/"
      p ""
      p "V1Switch("
      p "ParserImpl(),"
      p "verifyChecksum(),"
      p "ingress(),"
      p "egress(),"
      p "computeChecksum(),"
      p "DeparserImpl()"
      p ") main;"
   )

let rec metrics (policy : Ast.Expr) : Metrics =
   match policy.Node with 
   | Let(_,_,e2) -> metrics e2 
   | If(_,e2,e3) -> Seq.append (metrics e2) (metrics e3)
   | _ -> Seq.singleton policy

let pathAttributes (e : Ast.Expr) : PathAttributes = 
   let attrs = ref Set.empty 
   iter (fun e -> 
      match e.Node with 
      | PathAttribute(s,_) -> attrs := Set.add s.Name !attrs
      | _ -> ()
   ) e
   !attrs
 
let generateAttributeUpdate (attribute : string) = 
   match attribute with 
   | "queue" -> failwith "TODO"
   | "length" -> failwith "TODO"
   | "util" -> failwith "TODO"
   | _ -> failwith "Impossible"

let writeFile subdir fileName content = 
   let settings = Args.getSettings()
   let dir = settings.OutDir
   let sep = Util.File.sep
   let file = dir + sep + subdir + sep + fileName
   Util.File.writeFile file content

let appendFile subdir fileName content = 
   let settings = Args.getSettings()
   let dir = settings.OutDir 
   let sep = Util.File.sep
   let file = dir + sep + subdir + sep + fileName
   Util.File.appendFile file content

let isDestination pg node = 
   let inPeers = neighborsIn pg node 
   match Seq.tryFind (isRealNode >> not) inPeers with 
   | None -> false 
   | Some _ -> true


// TODO: Use sequence numbers in comparison
// TODO: How to deal with lazy splitting of probes?
let generateP4NodePolicy device nodes numTagBits (ctx : Context) =
   let name = device + ".p4"
   let writeFile = writeFile "app"
   let appendFile = appendFile "app"
   writeFile name "/* -*- P4_16 -*- */"          // P4 2016 
   appendFile name (preambleTxt ctx)             // P4 preamble
   appendFile name (headersTxt device numTagBits ctx)       // Header definitions
   appendFile name (parserTxt ctx)               // P4 parser
   appendFile name checksumVerifyTxt             // Check sum
   appendFile name (ingressTxt device nodes numTagBits ctx) // Main ingress policy
   appendFile name (egressTxt ctx)               // Main egress policy
   appendFile name checksumTxt                   // Check sum
   appendFile name (deparserTxt ctx)             // Deparser
   appendFile name switchTxt                     // Main switch impl.

  
let intToHexChar (i : int) = 
   if i < 0 then failwith "impossible" else
   if i < 10 then string i else 
   match i with 
   | 10 -> "a"
   | 11 -> "b"
   | 12 -> "c"
   | 13 -> "d"
   | 14 -> "e"
   | 15 -> "f"
   | _ -> failwith "impossible"

let intToMac (i : int) = 
   let arr = Array.create 12 0 
   let mutable x = i
   for j = 0 to 11 do 
      arr.[j] <- x % 16 
      x <- x / 16
   let arr = Array.map intToHexChar arr
   let mutable str = ""
   for j = 11 downto 0 do 
      if j < 11 && j % 2 = 1 then 
         str <- str + ":"
      str <- str + arr.[j]
   str


let uniqueMac() = 
   let i = ref -1
   let f () = 
      i := !i + 1
      intToMac !i
   f 

let generateTableEntries ctx =
   let settings = Args.getSettings()
   let flowletTableSize = settings.FlowletTableSize
   let getMac = uniqueMac()
   let rand = System.Random()
   for v in Topology.vertices ctx.Graph.Topo do
      let deviceId = ctx.DeviceIdMap.[v.Loc]
      // Create port to mac table
      let name = sprintf "s%d-commands.txt" deviceId
      let sb = StringBuilder()
      let device = v.Loc
      // Map port to ethernet mac address
      let ports = ctx.PortMap.[v.Loc]
      let numPorts = Seq.length ports
      for (Index(i), _) in ports do 
         bprintf sb "table_add tab_port_to_mac update_macs %d => %s\n" i (getMac())
      for (Index(i), _) in ports do 
         bprintf sb "register_write local_length %d %d\n" (i-2) 1
      for (Index(i), _) in ports do 
         bprintf sb "register_write local_queue %d %d\n" (i-2) 0
      // Map prefix to switch id
      let mutable TYPE_IPV4 = "0x0021"
      if ctx.isNS3 then
         TYPE_IPV4 <- "0x0021"
      else
         TYPE_IPV4 <- "0x0800"
      for i in 1 .. 10 do
         bprintf sb "table_add tab_forward_to_end_hosts forward_to_end_hosts %s 6&&&0xFF 1 10.0.0.%d&&&255.255.0.255 => %d 100\n" TYPE_IPV4 (20+i) (numPorts+1+i)
      if ctx.swidEnabled then
         bprintf sb "table_add swid_tag add_switchid_tag 0x8100 => %d\n" deviceId
         bprintf sb "table_add swid_tag add_switchid_tag 0x0800 => %d\n" deviceId

      for v' in Topology.vertices ctx.Graph.Topo do 
         let deviceId' = ctx.DeviceIdMap.[v'.Loc]
         bprintf sb "table_add tab_prefix_to_id update_id 10.0.%d.0/24 => %d\n" deviceId' (deviceId'-1) 
      // Initialize register values
      let numDevices = Topology.vertices ctx.Graph.Topo |> Seq.length
      let numTags = ctx.NodeMap.[device].Length
      for i = 0 to numDevices-1 do
         for j = 1 to tupleSize ctx do 
            bprintf sb "register_write decision_f%d %d %d\n" j i 9999999

      for i = 0 to (numDevices * numTags) - 1 do 
         for j = 1 to tupleSize ctx do 
            bprintf sb "register_write choices_f%d %d %d\n" j i 9999999

      let nodes = ctx.NodeMap.[v.Loc]
      let mutable i = 0
      for node in nodes do
         let initial = i
         let mcastId = ctx.McastIdMap.[node]
         bprintf sb "mc_mgrp_create %d\n" mcastId
         let outPeers = 
            neighbors ctx.Graph node 
            |> Seq.filter isRealNode 
            |> Seq.map (fun n -> n.Node.Loc)
            |> Set.ofSeq
         let allPorts = ctx.PortMap.[node.Node.Loc]
         let ports = Seq.filter (fun (_, peer) -> outPeers.Contains peer) allPorts
         for (Index(j), _) in ports do 
            bprintf sb "mc_node_create %d %d\n" i j
            i <- i + 1
         for j = 0 to (Seq.length ports) - 1 do 
            bprintf sb "mc_node_associate %d %d\n" mcastId (j + initial)
         // bprintf sb "table_add tab_hulapp_mcast set_hulapp_mcast %d => %d\n" node.Id mcastId

      for i = 0 to flowletTableSize - 1 do 
         bprintf sb "register_write flowlet_hopcount %d %d\n" i 9999999

      writeFile "app" name (sb.ToString())

let generateTables (ctx : Context) =
   generateTableEntries ctx
let generateJson (ctx : Context) = 
   let getId s = string ctx.DeviceIdMap.[s]
   let nodes = Topology.vertices ctx.Graph.Topo |> Seq.map (fun v -> getId v.Loc)

   let hostLinks = Seq.map (fun n -> "[\"h" + n + "\", \"s" + n + "\"]") nodes
   let links = 
      Topology.edges ctx.Graph.Topo 
      |> Seq.map (fun (u,v) -> if u.Loc < v.Loc then (u.Loc, v.Loc) else (v.Loc, u.Loc))
      |> Seq.distinctBy id
      |> Seq.map (fun (u,v) -> "[\"s" + (getId u) + "\", \"s" + (getId v) + "\"]")
   let mutable links = Seq.append hostLinks links
   let mutable endHostId = Seq.length nodes
   if not ctx.isNS3 then
      for v in Topology.vertices ctx.Graph.Topo do
         if v.Loc.[0..3] = "edge" then
            let node = ctx.DeviceIdMap.[v.Loc]
            for n in 1 .. ctx.NUM_END_HOSTS do
               endHostId <- endHostId + 1
               links <- Seq.append links (seq [ sprintf "[\"h%i\", \"s%i\"]" endHostId  node ] )
   let start_end_host_node = Seq.length(nodes)+1
   let n_end_hosts = endHostId-start_end_host_node+1

   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "{"
      p "  \"language\": \"p4-16\","
      p "  \"targets\": {"
      p "    \"multiswitch\": {"
      p "      \"auto-control-plane\": true,"
      p "      \"cli\": true,"
      p "      \"pcap_dump\": false,"
      p "      \"bmv2_log\": false,"
      p "      \"NUM_END_HOSTS\": \"%i\"," ctx.NUM_END_HOSTS
      p "      \"links\": [%s]," (Util.String.join links ",")
      p "      \"hosts\": {"

      let mutable serverId = 1
      for v in Topology.vertices ctx.Graph.Topo do
         let node = ctx.DeviceIdMap.[v.Loc]
         if ctx.mininet_server_mapping = LOCAL then
            serverId <- 0
         elif ctx.mininet_server_mapping = ABILENE then
            serverId <- (node-1)/2+1
         elif ctx.mininet_server_mapping = BT then 
            serverId <- (node-1)/2+1
         elif ctx.mininet_server_mapping = FATTREE then 
            if node <= ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*4 then  // aggr and edge
               serverId <- (((node-1)%(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*2))/ctx.NUM_END_HOSTS) + 7
            else                    // core
               serverId <- ((node-1)%(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS)/ctx.NUM_END_HOSTS) + 13
         elif ctx.mininet_server_mapping = SINGLE_POD then 
            if node <= ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*4 then  // aggr and edge
               serverId <- (((node-1)%(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*2))/ctx.NUM_END_HOSTS)
            else                    // core
               serverId <- ((node-1)%(ctx.NUM_END_HOSTS*2))
         else
            failwith "server mapping unsupported"
         p "        \"h%i\": {\"server\": \"%i\"}," node serverId
      for n in 1 .. n_end_hosts do
         let node_id = start_end_host_node+n-1
         if ctx.mininet_server_mapping = LOCAL then
            serverId <- 0
         elif ctx.mininet_server_mapping = ABILENE then
            serverId <- (n-1)/4+7
         elif ctx.mininet_server_mapping = BT then 
            serverId <- (n-1)/20+1
         elif ctx.mininet_server_mapping = FATTREE then 
            serverId <- (n-1)/(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS) + 1
         elif ctx.mininet_server_mapping = SINGLE_POD then 
            serverId <- (n-1)/(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS)
         p "        \"h%i\": {\"server\": \"%i\"}," node_id serverId
      sb.Remove(sb.Length-1, 1) |> ignore

      p "      },"
      p "      \"switches\": {"

      for v in Topology.vertices ctx.Graph.Topo do
         let node = ctx.DeviceIdMap.[v.Loc]
         if ctx.mininet_server_mapping = LOCAL then
            serverId <- 0
         elif ctx.mininet_server_mapping = ABILENE then
            serverId <- (node-1)/2+1
         elif ctx.mininet_server_mapping = BT then 
            serverId <- (node-1)/2+1
         elif ctx.mininet_server_mapping = FATTREE then 
            if node <= ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*4 then  // aggr and edge
               serverId <- (((node-1)%(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*2))/ctx.NUM_END_HOSTS) + 7
            else                    // core
               serverId <- ((node-1)%(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS)/ctx.NUM_END_HOSTS) + 13
         elif ctx.mininet_server_mapping = SINGLE_POD then 
            if node <= ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*4 then  // aggr and edge
               serverId <- (((node-1)%(ctx.NUM_END_HOSTS*ctx.NUM_END_HOSTS*2))/ctx.NUM_END_HOSTS)
            else                    // core
               serverId <- ((node-1)%(ctx.NUM_END_HOSTS*2))
         else
            failwith "server mapping unsupported"
         p "        \"s%i\": {" node
         p "          \"entries\": \"s%i-commands.txt\", " node
         p "          \"server\": \"%i\"" serverId
         p "        },"
      sb.Remove(sb.Length-1, 1) |> ignore

      p "      },"

      p "      \"configs\": {"

      for v in Topology.vertices ctx.Graph.Topo do
         let node = ctx.DeviceIdMap.[v.Loc]
         p "        \"s%d\": \"%s.p4\"," node v.Loc 
      sb.Remove(sb.Length-1, 1) |> ignore
      
      if ctx.isNS3 then
         p "      }"
      else 
         p "      },"
         if ctx.mininet_server_mapping = LOCAL then
            p "      \"servers\": [\"localhost\"],"
         else
            p "      \"servers\": [\"localhost\", \"node-1\", \"node-2\", \"node-3\", \"node-4\", \"node-5\", \"node-6\", \"node-7\", \"node-8\", \"node-9\", \"node-10\", \"node-11\", \"node-12\", \"node-13\", \"node-14\", \"node-15\", \"node-16\", \"node-17\", \"node-18\"],"
         p "      \"before\": {"
         p "        \"cmd\": ["
         for v1 in Topology.vertices ctx.Graph.Topo do
            let node1 = ctx.DeviceIdMap.[v1.Loc]
            for v2 in Topology.vertices ctx.Graph.Topo do
               let node2 = ctx.DeviceIdMap.[v2.Loc]
               p "          [\"h%d\", \"ip route add 10.0.%d.10 dev h%d-eth0\"]," node1 node2 node1
               p "          [\"h%d\", \"arp -s 10.0.%d.10 00:04:00:00:00:%02x\"]," node1 node2 node2
         for n1 in (Seq.length(nodes)+1) .. endHostId do
            if ctx.swidEnabled then
               p "          [\"h%d\", \"ifconfig h%d-eth0 mtu 1400\"]," n1 n1
            for n2 in (Seq.length(nodes)+1) .. endHostId do
               let tor_num = (n2-Seq.length(nodes)-1) / ctx.NUM_END_HOSTS + 1
               let end_host_num = (n2-Seq.length(nodes)-1) % ctx.NUM_END_HOSTS + 21
               p "          [\"h%d\", \"ip route add 10.0.%d.%d dev h%d-eth0\"]," n1 tor_num end_host_num n1
               p "          [\"h%d\", \"arp -s 10.0.%d.%d 00:04:00:00:00:%02x\"]," n1 tor_num end_host_num n2
         for v in Topology.vertices ctx.Graph.Topo do
            let node = ctx.DeviceIdMap.[v.Loc]
            if v.Loc.[0..3] = "edge" then
               p "          [\"h%d\", \"( sleep %f; python ../../../sendProbeLoop.py 0.0.0.0 %d 100000000 %f ) &\"]," node (5.+(float node)*0.12) (node-1) 256.
//         let loads = [10;20;30;40;50;60;70;80;90]
//         let datasets = [1;2;3;4]
//         let distributions = ["DCTCP";"CACHE"]
         let loads = [90]
         let datasets = [1]
         let distributions = ["DCTCP"]
         let workloadIds = ["90-1-DCTCP"]
         let traceLens = [150]
         let workloadDirs = ["../../../DCTCP_Conga_50Mbps_oneDir_Abilene_4_4_newSel_out/DCTCP_CDF_PER_FLOW_UNTIL_10_150.00_4_4_1.dat/"]

// measurement
         let mutable t = 25
         for v in Topology.vertices ctx.Graph.Topo do
            let node = ctx.DeviceIdMap.[v.Loc]
            t <- 25
//            for dist in distributions do
//               for d in datasets do
//                  for l in loads do
//                     p "          [\"s%d\", \"( sleep %f; python ../../../ifconfigstats.py s%d- %d-%d-%s ) &\"]," node (float(t)) node l d dist
//                     t <- t+150
            for i in 0 .. (Seq.length(workloadIds) - 1) do
               p "          [\"s%d\", \"( sleep %f; python ../../../ifconfigstats.py s%d- %s ) &\"]," node (float(t)) node workloadIds.[i]
               t <- t + traceLens.[i]
         if ctx.swidEnabled then   // only for Fattree case
            for v in Topology.vertices ctx.Graph.Topo do
               let node = ctx.DeviceIdMap.[v.Loc]
               if v.Loc.[0..3] = "edge" then
                  for port in 2 .. 4 do
                     p "          [\"s%d\", \"( python ../../../receiveSwid.py s%d-eth%d 10.0.%d. > s%d-eth%d.pktpaths.log 2> s%d-eth%d.pktpaths.err) &\"]," node node port node node port node port

// workload setup
         if ctx.workload = BT_ALL_IPERF then
            let src_nodes = [1;331;2;351;3;101;4;131;5;181;6;191;7;241;8;281;11;352;12;132;21;31;22;151;32;133;41;51;42;61;52;91;53;271;62;71;72;81;73;92;82;93;94;332;95;102;96;111;97;231;98;272;99;282;100;301;103;112;104;121;105;134;106;201;107;221;122;135;136;141;137;182;138;273;142;152;153;202;154;353;155;283;161;171;162;183;163;354;172;355;184;192;185;203;186;242;193;243;204;211;212;222;213;232;233;244;245;333;246;251;247;261;248;302;252;334;253;262;263;274;264;291;265;335;275;336;276;284;285;292;286;303;287;311;293;337;294;341;295;312;305;321;306;338;307;342;308;313;314;322;315;339]
            let dst_nodes = [331;1;351;2;101;3;131;4;181;5;191;6;241;7;281;8;352;11;132;12;31;21;151;22;133;32;51;41;61;42;91;52;271;53;71;62;81;72;92;73;93;82;332;94;102;95;111;96;231;97;272;98;282;99;301;100;112;103;121;104;134;105;201;106;221;107;135;122;141;136;182;137;273;138;152;142;202;153;353;154;283;155;171;161;183;162;354;163;355;172;192;184;203;185;242;186;243;193;211;204;222;212;232;213;244;233;333;245;251;246;261;247;302;248;334;252;262;253;274;263;291;264;335;265;336;275;284;276;292;285;303;286;311;287;337;293;341;294;312;295;321;305;338;306;342;307;313;308;322;314;339;315]
            for n in 1 .. n_end_hosts do
               let node_id = start_end_host_node+n-1
               p "          [\"h%d\", \"( sleep %f; iperf -s > h%d_server.log ) &\"]," node_id 10. node_id
            for i in 0 .. src_nodes.Length-1 do
               let src_node_id = start_end_host_node+src_nodes.[i]-1
               let dst_node_id = start_end_host_node+dst_nodes.[i]-1
               p "          [\"h%d\", \"( sleep %f; iperf -c 10.0.%d.%d -t 30 -i 1 > h%d_to_h%d.log ) &\"]," src_node_id 15. ((dst_nodes.[i]-1)/ctx.NUM_END_HOSTS+1) ((dst_nodes.[i]-1)%ctx.NUM_END_HOSTS+21) src_node_id dst_node_id
         elif ctx.workload = ABILENE_ALL_IPERF then
            let src_nodes = [1;4;2;10;5;7;6;11;8;16;12;13;14;17;15;22;18;19;20;23;21;28;24;25;26;31;29;32]
            let dst_nodes = [4;1;10;2;7;5;11;6;16;8;13;12;17;14;22;15;19;18;23;20;28;21;25;24;31;26;32;29]
            for n in 1 .. n_end_hosts do
               let node_id = start_end_host_node+n-1
               p "          [\"h%d\", \"( sleep %f; iperf -s > h%d_server.log ) &\"]," node_id 10. node_id
            for i in 0 .. src_nodes.Length-1 do
               let src_node_id = start_end_host_node+src_nodes.[i]-1
               let dst_node_id = start_end_host_node+dst_nodes.[i]-1
               p "          [\"h%d\", \"( sleep %f; iperf -c 10.0.%d.%d -t 30 -i 1 > h%d_to_h%d.log ) &\"]," src_node_id 15. ((dst_nodes.[i]-1)/ctx.NUM_END_HOSTS+1) ((dst_nodes.[i]-1)%ctx.NUM_END_HOSTS+21) src_node_id dst_node_id
         elif ctx.workload = ABILENE then
//           let src_nodes = [21;15;1;17]  // simulation setting
//           let dst_nodes = [11;9;3;7]    // simulation setting
            let src_nodes = [7;17;15;1]
            let dst_nodes = [11;19;5;13]
            for receiver in dst_nodes do
               let node_id = start_end_host_node+receiver-1
               p "          [\"h%d\", \"( sleep %f; ../../../DCTCP_Conga_50Mbps_oneDir_Abilene_4_4_newSel_out/DCTCP_CDF_PER_FLOW_UNTIL_10_150.00_4_4_1.dat/h%d.sh > h%d.log 2> h%d.err ) &\"]," node_id 10. node_id node_id node_id
            for sender in src_nodes do
               let node_id = start_end_host_node+sender-1
               t <- 15
               for d in datasets do
                  for l in loads do
                     p "          [\"h%d\", \"( sleep %f; ../../../DCTCP_Conga_50Mbps_oneDir_Abilene_4_4_newSel_out/DCTCP_CDF_PER_FLOW_UNTIL_%d_150.00_4_4_%d.dat/h%d.sh > h%d_%d_%d.log 2> h%d_%d_%d.err ) &\"]," node_id (float(t)) l d node_id node_id d l node_id d l
                     t <- t+400
         elif ctx.workload = FATTREE then
            for n in 1 .. n_end_hosts do
               let node_id = start_end_host_node+n-1
               if n > n_end_hosts/2 then   //receiver
                  p "          [\"h%d\", \"( sleep %f; ../../../DCTCP_Conga_50Mbps_oneDir_out/DCTCP_CDF_PER_FLOW_UNTIL_10_30.00_27_27_1.dat/h%d.sh > h%d.log 2> h%d.err ) &\"]," node_id 10. node_id node_id node_id
               else                      //sender
                  t <- 15
                  for dist in distributions do
                     for d in datasets do
                        for l in loads do
                           p "          [\"h%d\", \"( sleep %f; ../../../%s_Conga_50Mbps_oneDir_out/%s_CDF_PER_FLOW_UNTIL_%d_30.00_27_27_%d.dat/h%d.sh > h%d_%s_%d_%d.log 2> h%d_%s_%d_%d.err ) &\"]," node_id (float(t)) dist dist l d node_id node_id dist d l node_id dist d l
                           t <- t+150
         sb.Remove(sb.Length-1, 1) |> ignore
         p "        ]"
         p "      }"
      p "    }"
      p "  }"
      p "}"
      p ""
   ) |> writeFile "app" "p4app.json"

let generateAttrs (numTagBits : int) (ctx : Context) = 
   template (fun sb -> 
      let p fmt = bprintf sb "\n"; bprintf sb fmt
      p "%d" numTagBits
      for attr in ctx.Attrs do 
        p "%s" attr
   ) |> writeFile "app" "attributes.txt"

let generate (pg : CGraph.T) (ast : Ast.T) (probeMap : Map<int, Isotonicity.AttrType>) (typ : Type) : unit = 
   let metrics = metrics ast.OptFunction 
   let attrs = pathAttributes ast.OptFunction
   let mutable map = Map.empty
   // Map switch to PG nodes
   for v in pg.Graph.Vertices do 
      if isRealNode v then 
         let l = loc v
         map <- Util.Map.adjust l [] (fun x -> v::x) map
   let count = Map.count map
   // Create multicast groups ids for each PG node
   let mutable mcast = Map.empty 
   for kv in map do 
      let nodes = kv.Value
      let mutable i = 1
      for node in nodes do
         mcast <- Map.add node i mcast
         i <- i + 1
   // Collect destination information
   let mutable i = 0 
   let mutable dstMap = Map.empty
   for kv in map do 
      match Seq.tryFind (isDestination pg) kv.Value with
      | None -> () 
      | Some n -> 
         dstMap <- Map.add kv.Key (Tag(n.Id), Index(i)) dstMap
         i <- i+1
   // Compute the port map
   let mutable portMap = Map.empty
   for v in Topology.vertices pg.Topo do 
      let ns = Topology.neighbors pg.Topo v
      let names = Seq.map (fun (n : Topology.Node) -> n.Loc) ns
      let ports = Seq.mapi (fun i name -> (Index(i+2),name)) names
      portMap <- Map.add v.Loc ports portMap
   // Map nodes to ints
   let mutable deviceId = 1
   let mutable deviceIdMap = Map.empty
   for node in Topology.vertices pg.Topo do 
      deviceIdMap <- Map.add node.Loc deviceId deviceIdMap
      deviceId <- deviceId + 1
   let numProbes = numberOfProbes ast.OptFunction
   // Create the context information and generate the p4 programs
   let settings = Args.getSettings()
   let mutable msm = LOCAL
   let mutable w = ABILENE
   if settings.TopoType = "ABILENE" then
      msm <- ABILENE
      w <- ABILENE
   elif settings.TopoType = "FATTREE" then
      msm <- FATTREE
      w <- FATTREE
   else
      printfn "Unknown topo type (only ABILENE and FATTREE are hardcoded)"
      printfn "server mapping \"LOCAL\" and empty workload will be used (if mininet environment)"
      msm <- LOCAL
      w <- EMPTY

   let n = settings.NumEndHosts

   let context = 
     { Graph = pg
       NodeMap = map
       PortMap = portMap
       Ast = ast
       Type = typ
       Metrics = metrics
       Attrs = attrs
       NodeCount = count
       NumProbes = numProbes 
       NumProbesBits = 16
       ProbeMap = probeMap
       DestinationMap = dstMap 
       DeviceIdMap = deviceIdMap
       McastIdMap = mcast
       isNS3 = false
       NUM_END_HOSTS = n
       swidEnabled = false 
       mininet_server_mapping = msm 
       workload = w }
   let mutable numTagBits = 0
   for kv in map do 
      let numTags = kv.Value.Length
      let t = numBits numTags
      if t > numTagBits then
         numTagBits <- t
   for kv in map do
      generateP4NodePolicy kv.Key kv.Value numTagBits context
   generateTables context
   generateJson context
   generateAttrs numTagBits context
   let settings = Args.getSettings()
   if settings.Stats && (Map.count !totalBits > 0) then 
      let max = Map.fold (fun acc _ v -> max acc v) 0 !totalBits
      printfn "--------------------------"
      printfn "Max Total KB:%d" (max / 8000)
      printfn "--------------------------"
