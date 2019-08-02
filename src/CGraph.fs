module CGraph

open Ast
open QuickGraph
open System.Collections.Generic
open System.Diagnostics
open Util.Debug
open Util.Format


/// Individual node in the Product Graph. Contains:
///
/// .Id     a unique id to speed up hashing/comparisons in graph algorithms
/// .State  a unique representation of all automata states (q1,...,qk)
/// .Accept a 16 bit integer representing the lowest (best) preference
///         the value System.Int16.MaxValue represents a non-accepting node
/// .Node   the underlying topology node, including name + internal/external
[<CustomEquality; CustomComparison>]
type CgState = 
   { Id : int
     mutable State : int
     Accept : Set<int16>
     Node : Topology.Node }
   override this.ToString() = 
      "(id=" + string this.Id + ",state=" + string this.State + ",loc=" + this.Node.Loc + ")"
   
   override x.Equals(other) = 
      match other with
      | :? CgState as y -> (x.Id = y.Id)
      | _ -> false
   
   override x.GetHashCode() = x.Id
   interface System.IComparable with
      member x.CompareTo other = 
         match other with
         | :? CgState as y -> x.Id - y.Id
         | _ -> failwith "cannot compare values of different types"

/// An intermediate representation of Product graph nodes.
/// This is a bit of a temporary hack. It simplifies product graph construction
/// since the default hashing and comparison operators can be used during 
/// construction, before we have assigned a unique id for each state and node.
/// We throw this away after construction since it is less space inefficient.
type CgStateTmp = 
   { TStates : int array
     TAccept : Set<int16>
     TNode : Topology.Node}

/// Type of the Product Graph. Contains:
/// 
/// .Start  A unique start node. Anything routers connected 
///         to the start node can originate traffic
/// .End    A unique end node, which is connected to all accepting
///         nodes. This is included to simplify some algorithms
/// .Topo   The underlying topology object
type T = 
   { Regexes : Map<Re,int16>
     Start : CgState
     End : CgState 
     Topo : Topology.T
     Graph : BidirectionalGraph<CgState, Edge<CgState>> }

/// Direction of search. We often need to search in the reverse graph,
/// yet do not want to make a copy of the graph every time
type Direction = 
   | Up
   | Down


let MAX_VALUE = System.Int16.MaxValue

/// Convert the space-inefficient product graph that keeps an array
/// of automata states in each node, into a space-efficient product
/// graph that represents each unique tuple with a single integer
let index ((regexes, graph, topo, startNode, endNode) : _ * BidirectionalGraph<_, _> * _ * _ * _) = 
   let newCG = BidirectionalGraph()
   let reindex = Util.Reindexer(HashIdentity.Structural)
   
   let nstart = 
      { Id = 0
        Node = startNode.TNode
        State = (reindex.Index startNode.TStates)
        Accept = startNode.TAccept }
   
   let nend = 
      { Id = 1
        Node = endNode.TNode
        State = (reindex.Index endNode.TStates)
        Accept = endNode.TAccept }
   
   ignore (newCG.AddVertex nstart)
   ignore (newCG.AddVertex nend)
   let mutable i = 2
   let idxMap = Dictionary(HashIdentity.Structural)
   idxMap.[(nstart.Node.Loc, nstart.State)] <- nstart
   idxMap.[(nend.Node.Loc, nend.State)] <- nend
   for v in graph.Vertices do
      if Topology.isTopoNode v.TNode then 
         let newv = 
            { Id = i
              Node = v.TNode
              State = (reindex.Index v.TStates)
              Accept = v.TAccept }
         i <- i + 1
         idxMap.Add((v.TNode.Loc, reindex.Index v.TStates), newv)
         newCG.AddVertex newv |> ignore
   for e in graph.Edges do
      let v = e.Source
      let u = e.Target
      let x = idxMap.[(v.TNode.Loc, reindex.Index v.TStates)]
      let y = idxMap.[(u.TNode.Loc, reindex.Index u.TStates)]
      newCG.AddEdge(Edge(x, y)) |> ignore
   { Regexes = regexes
     Start = nstart
     Graph = newCG
     End = nend
     Topo = topo }

/// Reduces the number of state tags by reusing tags for  
/// each topology state. If there are two nodes for "A", then
/// we only need to use tags 1,2 even if these are used for other 
/// nodes. The sender can disambiguate.
let compress (cg: T) =
   let toMap list = 
      List.sort list 
      |> List.mapi (fun i x -> (x,i)) 
      |> Map.ofList
   let graph = cg.Graph
   let mutable map = Map.empty 
   for v in graph.Vertices do 
      map <- Util.Map.adjust v.Node.Loc [] (fun x -> v.State::x) map 
   let map2 = Map.map (fun _ v -> toMap v) map
   for v in graph.Vertices do 
      let idxMap = map2.[v.Node.Loc]
      let idx = idxMap.[v.State]
      v.State <- idx

let getTransitions autos = 
   let aux (auto : Regex.Automaton) = 
      let trans = Dictionary(HashIdentity.Structural)
      for kv in auto.trans do
         let (q1, set) = kv.Key
         let q2 = kv.Value
         for s in set do
            trans.[(q1, s)] <- q2
      trans
   Array.map aux autos

let getGarbageStates (auto : Regex.Automaton) = 
   let inline aux (kv : KeyValuePair<_, _>) = 
      let k = kv.Key
      let v = kv.Value
      let c = Set.count v
      if c = 1 && Set.minElement v = k then Some k
      else None
   
   let selfLoops = 
      Map.fold (fun acc (x, _) y -> 
         let existing = Util.Map.getOrDefault x Set.empty acc
         Map.add x (Set.add y existing) acc) Map.empty auto.trans
      |> Seq.choose aux
      |> Set.ofSeq
   
   Set.difference selfLoops auto.F

let inline uniqueNeighbors canOriginate topo (t : Topology.Node) = 
   (if t.Typ = Topology.Start then canOriginate
    else Topology.neighbors topo t)
   |> Set.ofSeq

/// Build the product graph from a topology and collection of 
/// (ordered) regular expressions. Performs a multi-way intersection 
/// of each automata with the topology. As an optimization, we avoid 
/// constructing parts of the product graph prematurely when we are 
/// guaranteed it will never lead to an accepting state.
let buildFromAutomata (topo : Topology.T) (regexes : Map<Re,int16>) (autos : Regex.Automaton array) : T = 
   if autos.Length >= int MAX_VALUE then 
      error (sprintf "Hula does not currently support more than %d regexes" MAX_VALUE)
   if not (Topology.isWellFormed topo) then 
      error (sprintf "Invalid topology. Topology must be connected.")
   let unqTopo = Set.ofSeq (Topology.vertices topo)
   let canOrigin = Seq.filter Topology.canOriginateTraffic unqTopo
   let transitions = getTransitions autos
   let garbage = Array.map getGarbageStates autos
   let graph = BidirectionalGraph<CgStateTmp, Edge<CgStateTmp>>()
   let starting = Array.map (fun (x : Regex.Automaton) -> x.q0) autos
   let newStart = 
      { TStates = starting
        TAccept = Set.empty
        TNode = Topology.Node("start", Topology.Start) }
   ignore (graph.AddVertex newStart)
   let marked = HashSet(HashIdentity.Structural)
   let todo = Queue()
   todo.Enqueue newStart
   while todo.Count > 0 do
      let currState = todo.Dequeue()
      let t = currState.TNode
      let adj = uniqueNeighbors canOrigin topo t
      let adj = 
         match t.Typ with
         | Topology.Unknown _ -> Set.add t adj
         | _ -> adj
      //if t.Typ = Topology.Unknown then Set.add t adj
      //else adj
      for c in adj do
         let dead = ref true
         let nextInfo = 
            Array.init autos.Length (fun i -> 
               let g, v = autos.[i], currState.TStates.[i]
               let newState = transitions.[i].[(v, c.Loc)]
               if not (garbage.[i].Contains newState) then dead := false
               let accept = 
                  if (Topology.canOriginateTraffic c) && (Set.contains newState g.F) then 
                     Some (int16 (i + 1))
                  else None
               newState, accept)
         let nextStates, nextAccept = Array.unzip nextInfo
         let accept = Array.choose id nextAccept |> Set.ofArray
         let state = 
            { TStates = nextStates
              TAccept = accept
              TNode = c }
         if not !dead then 
            if not (marked.Contains state) then 
               ignore (marked.Add state)
               ignore (graph.AddVertex state)
               todo.Enqueue state
            let edge = Edge(currState, state)
            ignore (graph.AddEdge edge)
   let newEnd = 
      { TStates = [||]
        TAccept = Set.empty
        TNode = Topology.Node("end", Topology.End) }
   graph.AddVertex newEnd |> ignore
   for v in graph.Vertices do
      if not (Set.isEmpty v.TAccept) then 
         let e = Edge(v, newEnd)
         ignore (graph.AddEdge(e))
   index (regexes, graph, topo, newStart, newEnd)

let loc x = x.Node.Loc

let shadows x y = (loc x = loc y) && (x <> y)

let isRealNode (state : CgState) = Topology.isTopoNode state.Node

let neighbors (cg : T) (state : CgState) = 
   seq { 
      for e in cg.Graph.OutEdges state do
         yield e.Target
   }

let neighborsIn (cg : T) (state : CgState) = 
   seq { 
      for e in cg.Graph.InEdges state do
         yield e.Source
   }

let repeatedOuts (cg : T) = 
   seq { 
      for v in cg.Graph.Vertices do
         match v.Node.Typ with
         | Topology.Unknown vs -> 
            if (Set.isEmpty vs && Seq.contains v (neighbors cg v)) then yield v
         | _ -> ()
   }
   |> Set.ofSeq

(* let isRepeatedOut (cg : T) (state : CgState) = 
   match state.Node.Typ, Seq.contains state (neighbors cg state) with
   | Topology.Unknown vs, true -> Set.isEmpty vs
   | _, _ -> false *)

//(state.Node.Typ = Topology.Unknown) && (Seq.contains state (neighbors cg state))
let isInside x = Topology.isInside x.Node
let isOutside x = Topology.isOutside x.Node
let isEmpty (cg : T) = cg.Graph.VertexCount = 2

let toDot (cg : T) : string = 
   let onFormatEdge (_ : Graphviz.FormatEdgeEventArgs<CgState, Edge<CgState>>) = ()

   let onFormatVertex (v : Graphviz.FormatVertexEventArgs<CgState>) = 
      let state = string v.Vertex.State
      
      let location = 
         match v.Vertex.Node.Typ with
         | Topology.Unknown excls ->
            let s = 
               if Set.isEmpty excls then "{}"
               else sprintf "{%s}" (Util.Set.joinBy "," excls)
            sprintf "out-%s" s
         | _ -> v.Vertex.Node.Loc
      match v.Vertex.Node.Typ with
      | Topology.Start -> v.VertexFormatter.Label <- "0,Start"
      | Topology.End -> v.VertexFormatter.Label <- "1,End"
      | _ -> 
         if Set.isEmpty v.Vertex.Accept then 
            let label = sprintf "%s, %s" state location
            v.VertexFormatter.Label <- label
         else 
            let str = Util.Set.toString v.Vertex.Accept
            let label = sprintf "%s, %s, Accept=%s" state location str
            v.VertexFormatter.Label <- label
            v.VertexFormatter.Shape <- Graphviz.Dot.GraphvizVertexShape.DoubleCircle
            v.VertexFormatter.Style <- Graphviz.Dot.GraphvizVertexStyle.Filled
            v.VertexFormatter.FillColor <- Graphviz.Dot.GraphvizColor.LightYellow
   
   let graphviz = Graphviz.GraphvizAlgorithm<CgState, Edge<CgState>>(cg.Graph)
   graphviz.FormatEdge.Add(onFormatEdge)
   graphviz.FormatVertex.Add(onFormatVertex)
   graphviz.Generate()

/// To help with debugging, we generate a graphical representation
/// of the product graph in the graphviz dot format.
/// A png can be generated if the 'dot' command line tool
/// is installed and in the system path.
let generatePNG (cg : T) (file : string) : unit = 
   System.IO.File.WriteAllText(file + ".dot", toDot cg)
   let p = new Process()
   p.StartInfo.FileName <- "dot"
   p.StartInfo.UseShellExecute <- false
   p.StartInfo.Arguments <- "-Tpng " + file + ".dot -o " + file + ".png"
   p.StartInfo.CreateNoWindow <- true
   p.Start() |> ignore
   p.WaitForExit()

/// Helper functions for common reachability queries.
/// We avoid using the default QuickGraph algorithms since 
/// (1)  We want to abstract over direction
/// (2)  We want to take advantage of the unique node id
module Reachable = 
   let postOrder (cg : T) (source : CgState) direction : List<CgState> = 
      let f = 
         if direction = Up then neighborsIn
         else neighbors
      
      let marked = HashSet()
      let ret = ResizeArray()
      let s = Stack()
      s.Push(source)
      while s.Count > 0 do
         let v = s.Pop()
         if not (marked.Contains v) then 
            ignore (marked.Add v)
            ret.Add(v)
            for w in f cg v do
               s.Push(w)
      ret
   
   let dfs (cg : T) (source : CgState) direction : HashSet<CgState> = 
      let f = 
         if direction = Up then neighborsIn
         else neighbors
      
      let marked = HashSet()
      let s = Stack()
      s.Push(source)
      while s.Count > 0 do
         let v = s.Pop()
         if not (marked.Contains v) then 
            ignore (marked.Add v)
            for w in f cg v do
               s.Push(w)
      marked
   
   let path (cg : T) (source : CgState) (target : CgState) : List<Edge<CgState>> option = 
      let marked = HashSet()
      let edgeTo = Dictionary()
      let s = Queue()
      ignore (marked.Add(source))
      s.Enqueue(source)
      let mutable search = true
      while search && s.Count > 0 do
         let v = s.Dequeue()
         if v = target then search <- false
         for e in cg.Graph.OutEdges v do
            let w = e.Target
            if not (marked.Contains w) then 
               ignore (marked.Add w)
               edgeTo.[w] <- e
               s.Enqueue(w)
      if search then None
      else 
         let path = List()
         let mutable x = target
         while x <> source do
            let e = edgeTo.[x]
            path.Add(e)
            x <- e.Source
         Some(path)
   

module Domination = 
   type DomTreeMapping = Dictionary<CgState, CgState option>
   
   [<Struct>]
   type DominationTree(tree : DomTreeMapping) = 
      
      member this.IsDominatedBy(x, y) = 
         match tree.[x] with
         | None -> false
         | Some v -> 
            let mutable runner = x
            let mutable current = v
            let mutable found = false
            while not found && runner <> current do
               if runner = y then found <- true
               runner <- current
               current <- tree.[runner].Value
            found
      
      member this.IsDominatedByFun(x, f) = 
         match tree.[x] with
         | None -> false
         | Some v -> 
            let mutable runner = x
            let mutable current = v
            let mutable found = false
            while not found && runner <> current do
               if f runner then found <- true
               runner <- current
               current <- tree.[runner].Value
            found
      
      member this.TryIsDominatedBy(x, f) = 
         match tree.[x] with
         | None -> None
         | Some v -> 
            let mutable runner = x
            let mutable current = v
            let mutable found = None
            while Option.isNone found && runner <> current do
               if f runner then found <- Some runner
               runner <- current
               current <- tree.[runner].Value
            found
   
   let inter (po : Dictionary<CgState, int>) (dom : DomTreeMapping) b1 b2 = 
      let mutable finger1 = b1
      let mutable finger2 = b2
      let mutable x = po.[finger1]
      let mutable y = po.[finger2]
      while x <> y do
         while x > y do
            finger1 <- Option.get dom.[finger1]
            x <- po.[finger1]
         while y > x do
            finger2 <- Option.get dom.[finger2]
            y <- po.[finger2]
      finger1
   
   let dominators (cg : T) root direction : DominationTree = 
      let adj = 
         if direction = Up then neighbors cg
         else neighborsIn cg
      
      let dom = Dictionary()
      let reach = Reachable.postOrder cg root direction
      let postorder = Seq.mapi (fun i n -> (n, i)) reach
      let postorderMap = Dictionary()
      for (n, i) in postorder do
         postorderMap.[n] <- i
      let inline findBefore i preds = 
         let aux x = 
            let (b, v) = postorderMap.TryGetValue x
            b && (v < i)
         Seq.find aux preds
      for b in cg.Graph.Vertices do
         dom.[b] <- None
      dom.[root] <- Some root
      let mutable changed = true
      while changed do
         changed <- false
         for b, i in postorder do
            if b <> root then 
               let preds = adj b
               let mutable newIDom = findBefore i preds
               for p in preds do
                  if p <> newIDom then 
                     if dom.[p] <> None then newIDom <- inter postorderMap dom p newIDom
               let x = dom.[b]
               if Option.isNone x || x.Value <> newIDom then 
                  dom.[b] <- Some newIDom
                  changed <- true
      DominationTree(dom)

/// We perform multiple minimization passes over the product graph
/// after construction for the following reasons:
/// (1)  Can speed up compilation by reducing the size of the graph
/// (2)  Can make the compiled configs smaller by pruning irrelevant cases
/// (3)  Can improve the failure analysis, by ruling out certain cases
module Minimize = 
   type Edge = 
      struct
         val X : int
         val Y : int
         new(x, y) = 
            { X = x
              Y = y }
      end
   
   let edgeSet (cg : T) = 
      let acc = HashSet()
      for e in cg.Graph.Edges do
         let e = Edge(e.Source.Id, e.Target.Id)
         ignore (acc.Add e)
      acc
   
   /// Remove nodes/edges that are never on any non-loop path.
   /// There are 4 symmetric cases.
   let removeDominated (cg : T) = 
      let routs = repeatedOuts cg
      let dom = Domination.dominators cg cg.Start Down
      let domRev = Domination.dominators cg cg.End Up
      cg.Graph.RemoveVertexIf
         (fun v -> 
         (not (routs.Contains v)) 
         && (dom.IsDominatedByFun(v, shadows v) || domRev.IsDominatedByFun(v, shadows v))) |> ignore
      let edges = edgeSet cg
      cg.Graph.RemoveEdgeIf
         (fun e -> 
         let x = e.Source
         let y = e.Target
         let e = Edge(y.Id, x.Id)
         (edges.Contains e) && (not (routs.Contains x || routs.Contains y)) 
         && (dom.IsDominatedBy(x, y) || domRev.IsDominatedBy(y, x)) && (x <> y))
      |> ignore
      cg.Graph.RemoveEdgeIf
         (fun e -> 
         let x = e.Source
         let y = e.Target
         (not (routs.Contains e.Source || routs.Contains e.Target)) 
         && (domRev.IsDominatedByFun(y, shadows x)))
      |> ignore
   
   /// Combines a node out-{N} with a node N into a new state: out
   let mergeNodes (cg : T) out state (merged, _) = 
      let nsOut = neighbors cg out |> Set.ofSeq
      let nsIn = neighborsIn cg out |> Set.ofSeq
      // remove old node
      cg.Graph.RemoveVertex out |> ignore
      // remove all nodes to be merged
      for n in merged do
         cg.Graph.RemoveVertex n |> ignore
      // add new state
      cg.Graph.AddVertex state |> ignore
      // add back edges
      for neigh in nsOut do
         if not (Set.contains neigh merged) then 
            let edge = 
               if neigh = out then Edge<CgState>(state, state)
               else Edge<CgState>(state, neigh)
            cg.Graph.AddEdge edge |> ignore
      for neigh in nsIn do
         if not (Set.contains neigh merged) then 
            if neigh <> out then 
               let edge = Edge<CgState>(neigh, state)
               cg.Graph.AddEdge edge |> ignore
   
   let getCandidates out = 
      match out.Node.Typ with
      | Topology.Unknown vs -> vs
      | _ -> Set.empty
   
   /// Combines nodes out-{X,Y} and Y into out-{X} 
   /// when they share all neighbors and have edges to each other
   let combineAsOut (cg : T) = 
      let outs = Seq.filter (fun v -> Topology.isUnknown v.Node) cg.Graph.Vertices
      let mutable toMerge = Seq.empty
      for out in outs do
         let mutable candidates = getCandidates out
         if candidates.Count > 0 then 
            let nsOut = neighbors cg out |> Set.ofSeq
            let nsIn = neighborsIn cg out |> Set.ofSeq
            for n in nsOut do
               if n.Accept = out.Accept && candidates.Contains(loc n) then 
                  let nsOut' = neighbors cg n |> Set.ofSeq
                  let nsIn' = neighborsIn cg n |> Set.ofSeq
                  if (nsOut' = Set.remove n nsOut) && (nsIn' = Set.remove n nsIn) then 
                     candidates <- Set.remove n.Node.Loc candidates
                     let x = Seq.singleton (out, n)
                     toMerge <- Seq.append x toMerge
      let groups = Seq.groupBy fst toMerge
      for (out, gs) in groups do
         let candidates = getCandidates out
         let mutable merged = Set.empty
         let mutable notMerged = candidates
         for (_, n) in gs do
            merged <- Set.add n merged
            notMerged <- Set.remove n.Node.Loc notMerged
         let node = Topology.Node(out.Node.Loc, Topology.Unknown notMerged)
         
         let newState = 
            { Accept = out.Accept
              Id = out.Id
              State = out.State
              Node = node }
         mergeNodes cg out newState (merged, candidates)
   
   let removeNodesThatCantReachEnd (cg : T) = 
      let canReach = Reachable.dfs cg cg.End Up
      cg.Graph.RemoveVertexIf(fun v -> Topology.isTopoNode v.Node && not (canReach.Contains v)) 
      |> ignore
   
   let removeNodesThatStartCantReach (cg : T) = 
      let canReach = Reachable.dfs cg cg.Start Down
      cg.Graph.RemoveVertexIf(fun v -> Topology.isTopoNode v.Node && not (canReach.Contains v)) 
      |> ignore
   
   let inline allConnected cg outStar scc = 
      Set.forall (fun x -> 
         let nOut = Set.ofSeq (neighbors cg x)
         let nIn = Set.ofSeq (neighborsIn cg x)
         x = outStar || (nOut.Contains outStar && nIn.Contains outStar)) scc
   
   let pickBorders (e : Edge<CgState>) = 
      if isInside e.Source && isOutside e.Target then Some e.Source
      else None

   let removeConnectionsToOutStar (cg : T) = 
      let routs = repeatedOuts cg
      cg.Graph.RemoveEdgeIf(fun e -> 
         let x = e.Source
         let y = e.Target
         let realNodes = isRealNode x && isRealNode y
         let eqRanks = x.Accept = y.Accept
         if realNodes && eqRanks && x <> y then 
            if routs.Contains x then not (Set.isEmpty x.Accept)
            else if routs.Contains y then 
               Seq.exists isInside (neighbors cg x) 
               && (Seq.exists ((=) cg.Start) (neighborsIn cg y))
            else false
         else false)
      |> ignore
   
   let removeRedundantExternalNodes (cg : T) = 
      let toDelNodes = HashSet(HashIdentity.Structural)
      let routs = repeatedOuts cg
      for os in routs do
         let nos = Set.ofSeq (neighborsIn cg os)
         for n in Set.remove os nos do
            if cg.Graph.OutDegree n = 1 && isOutside n then 
               let nin = Set.ofSeq (neighborsIn cg n)
               if Set.isSuperset nos nin then ignore (toDelNodes.Add n)
      for os in routs do
         let nos = Set.ofSeq (neighbors cg os)
         for n in Set.remove os nos do
            if cg.Graph.InDegree n = 1 && isOutside n then 
               let nin = Set.ofSeq (neighbors cg n)
               if Set.isSuperset nos nin then ignore (toDelNodes.Add n)
      cg.Graph.RemoveVertexIf(fun v -> toDelNodes.Contains v) |> ignore
    
   let minimize (cg : T) =
      let settings = Args.getSettings()
      if not settings.Minimize then cg
      else 
         let isConcrete = not settings.IsAbstract
         log (sprintf "Node count: %d" cg.Graph.VertexCount)
         let inline count cg = cg.Graph.VertexCount + cg.Graph.EdgeCount
         
         let inline prune() = 
            removeNodesThatCantReachEnd cg
            log (sprintf "Node count (cant reach end): %d" cg.Graph.VertexCount)
            combineAsOut cg
            log (sprintf "Node count (combine external nodes): %d" cg.Graph.VertexCount)
            removeRedundantExternalNodes cg
            log (sprintf "Node count (redundant external nodes): %d" cg.Graph.VertexCount)
            removeConnectionsToOutStar cg
            log (sprintf "Node count (connections to out*): %d" cg.Graph.VertexCount)
            // combineExportNeighborsAsOut ti cg
            // logInfo (idx, sprintf "Node count (merge export neighbors): %d" cg.Graph.VertexCount)
            if isConcrete then 
               removeDominated cg
               log (sprintf "Node count (remove dominated): %d" cg.Graph.VertexCount)
            removeNodesThatStartCantReach cg
            log (sprintf "Node count (start cant reach): %d" cg.Graph.VertexCount)
         
         let mutable sum = count cg
         prune()
         while count cg <> sum do
            sum <- count cg
            prune()
         log (sprintf "Node count - after O3: %d" cg.Graph.VertexCount)
         cg


/// Idea is to rearrange the term so matches on regexes come first
/// followed by some regex free expressions. So for example the program:
///
/// let x = 1 in 
/// let y = 2 in 
/// if path.util < 10 then
///    if matches(.* X .*) then 
///       path.util
///    else 
///       2 * path.util
/// else 
///    path.len
///
/// The first step would be to adjust the order of if statements:
///
/// let x = 1 in 
/// let y = 2 in 
/// if matches(.* X .*) then 
///    (if path.util < 10 then
///       path.util
///     else 
///       path.len)
/// else 
///    (if path.util < 10 then
///      2 * path.util
///     else 
///      path.len)
///
/// The second step would be to create a single product graph, that combines every
/// regex that appears in the policy together. The nodes are annotated with the Ast
/// expression that appears under the sequence of ifs

let rec toRegex (alphabet : Set<string>) (re : Ast.Re) : Regex.T = 
   match re.Value with 
   | Dot -> Regex.locs alphabet
   | Loc l -> Regex.loc l.Name 
   | Par(s,t) -> Regex.union (toRegex alphabet s) (toRegex alphabet t)
   | Seq(s,t) -> Regex.concat (toRegex alphabet s) (toRegex alphabet t)
   | Star s -> Regex.star (toRegex alphabet s)

let rec allRegexes (e : Ast.Expr) : Set<Ast.Re> = 
   match e.Node with 
   | Exists _ -> failwith "don't support exists yet"
   | Matches r -> Set.singleton r
   | Not e -> allRegexes e 
   | If(e1,e2,e3) -> 
      Set.union (allRegexes e1) (allRegexes e2)
      |> Set.union (allRegexes e3)
   | Let(_,e1,e2) | Plus(e1,e2) | Times(e1,e2) | And(e1, e2) 
   | Or(e1,e2) | Geq(e1,e2) | Gt(e1,e2) | Lt(e1,e2) | Leq(e1,e2) -> 
      Set.union (allRegexes e1) (allRegexes e2)
   | Tuple es | Max es | Min es -> List.map allRegexes es |> List.fold Set.union Set.empty
   | Ident _ | IntLiteral _ | PathAttribute _ -> Set.empty

// For now, we assume only top level if statements, and no let bindings
let run (x : Ast.T) =
   let alphabet = x.TopoInfo.SelectGraphInfo.InternalNames
   let topo = x.TopoInfo.SelectGraphInfo.Graph
   let res = allRegexes x.OptFunction |> Set.toArray
   let resi = (Array.mapi (fun i v -> (int16 (i+1),v)) res)
   let reMap = Array.fold (fun acc (i,r) -> Map.add r i acc) Map.empty resi
   let regexes = Array.map (toRegex alphabet) res
   let regexes = Array.append regexes [|Regex.star (Regex.locs alphabet)|]
   let regexes = Array.map Regex.rev regexes
   let dfas = Array.map (Regex.makeDFA alphabet) regexes
   buildFromAutomata topo reMap dfas 