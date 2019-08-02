module Regex

open Hashcons

/// Extended regular expressions with negation, 
/// intersection, and character classes
[<NoComparison; CustomEquality>]
type Re = 
   | Empty
   | Epsilon
   | Locs of Set<string>
   | Out of Set<string>
   | Concat of T list
   | Inter of T list
   | Union of T list
   | Negate of T
   | Star of T

and [<CustomComparison; CustomEquality>] T = 
   | Regex of HashCons<Re>
   interface System.IComparable

/// Reverse a regular expression
val rev : T -> T
/// Smart constructors for regular expression
val empty : T
val epsilon : T
val loc : string -> T
val locs : Set<string> -> T
val star : T -> T
val negate : Set<string> -> T -> T
val concat : T -> T -> T
val concatAll : T list -> T
val inter : T -> T -> T
val interAll : T list -> T
val union : T -> T -> T
val unionAll : T list -> T
/// Special out variable only used for regex construction from Product Graph
val out : Set<string> -> T

/// Build a DFA for a regular expression directly using regular 
/// expression derivatives. Works well with complement,
/// intersection, and character classes. Produces near-minimal DFAs
type Automaton = 
   { q0 : int
     Q : Set<int>
     F : Set<int>
     trans : Map<int * Set<string>, int> }

/// Create a DFA from a regex
val makeDFA : Set<string> -> T -> Automaton

/// Find the starting locations (initial transitions) for an automaton 
val startingLocs : Automaton -> Set<string>

/// Check if an automaton denotes the empty set, and if not, return an example sequence
val emptiness : Automaton -> string list option