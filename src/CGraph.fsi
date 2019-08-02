module CGraph

open QuickGraph
open System.Collections.Generic

/// Individual node in the Product Graph. Contains:
///
/// .Id     a unique id to speed up hashing/comparisons in graph algorithms
/// .State  a unique representation of all automata states (q1,...,qk)
/// .Accept a 16 bit integer representing the lowest (best) preference
///         the value System.Int16.MaxValue represents a non-accepting node
/// .Node   the underlying topology node, including name + internal/external
[<CustomEquality; CustomComparison>]
type CgState = 
   { Id     : int
     mutable State : int
     Accept : Set<int16>
     Node   : Topology.Node }
   interface System.IComparable

/// Type of the Product Graph. Contains:
/// 
/// .Start  A unique start node. Anything routers connected 
///         to the start node can originate traffic
/// .End    A unique end node, which is connected to all accepting
///         nodes. This is included to simplify some algorithms
/// .Topo   The underlying topology object
type T = 
   { Regexes : Map<Ast.Re, int16>
     Start : CgState
     End   : CgState
     Topo  : Topology.T
     Graph : BidirectionalGraph<CgState, Edge<CgState>> }

/// Direction of search. We often need to search in the reverse graph,
/// yet do not want to make a copy of the graph every time
type Direction = 
   | Up
   | Down

/// Get the location for the state
val loc : CgState -> string
/// Determine if two nodes shadow each other
val shadows : CgState -> CgState -> bool
/// Returns the (outgoing) neighbors of a state in the graph
val neighbors : T -> CgState -> seq<CgState>
/// Returns the (incoming) neighbors of a state in the graph
val neighborsIn : T -> CgState -> seq<CgState>
/// Returns true if a node is not the special start or end node
val isRealNode : CgState -> bool
/// Returns true if a node is internal to the AS
val isInside : CgState -> bool
/// Returns true if the graph contains only the start and end nodes
val isEmpty : T -> bool
/// Generate a png file for the constraint graph (requires graphviz dot utility)
val generatePNG : T -> string -> unit

module Reachable = 
   /// Find all destinations reachable from src
   val dfs : T -> CgState -> Direction -> HashSet<CgState>

module Minimize = 
   /// Remove nodes and edges not relevant to the BGP decision process
   val minimize : T -> T

/// Compress the state tags to reduce rule space
val compress : T -> unit

/// Create the product graph from an Ast expression
val run : Ast.T -> T