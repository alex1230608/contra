module Regex

open Hashcons
open QuickGraph
open System.Collections.Generic
open Topology
open Util
open Util.Format

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
   
   override x.Equals(other) = 
      let rec equalAux xs ys = 
         match xs, ys with
         | [], [] -> true
         | x :: xtl, y :: ytl -> (x = y) && (equalAux xtl ytl)
         | _, _ -> false
      match other with
      | :? Re as y -> 
         match x, y with
         | Empty, Empty -> true
         | Epsilon, Epsilon -> true
         | Locs S, Locs T -> S = T
         | Out S, Out T -> S = T
         | Concat xs, Concat ys | Inter xs, Inter ys | Union xs, Union ys -> equalAux xs ys
         | Negate x, Negate y | Star x, Star y -> x = y
         | _, _ -> false
      | _ -> false
   
   override x.GetHashCode() = 
      let rec hashAux acc xs = 
         match xs with
         | [] -> acc
         | x :: xtl -> hashAux (19 * acc + hash x) xtl
      match x with
      | Empty -> 1
      | Epsilon -> 2
      | Locs S -> 3 + hash S
      | Out S -> 5 + hash S
      | Concat xs -> hashAux 0 xs + 7
      | Inter xs -> hashAux 0 xs + 11
      | Union xs -> hashAux 0 xs + 13
      | Negate x -> 19 * hash x + 17
      | Star x -> 19 * hash x + 19
   
   override this.ToString() = 
      let addParens s = "(" + s + ")"
      match this with
      | Empty -> "{}"
      | Epsilon -> "\"\""
      | Locs S -> Util.Set.toString S
      | Out S -> sprintf "out-%s" (Util.Set.toString S)
      | Concat rs -> 
         List.map (fun r -> r.ToString()) rs
         |> List.joinBy ";"
         |> addParens
      | Inter rs -> 
         List.map (fun r -> r.ToString()) rs
         |> List.joinBy " and "
         |> addParens
      | Union rs -> 
         List.map (fun r -> r.ToString()) rs
         |> List.joinBy " or "
         |> addParens
      | Negate r -> "!(" + r.ToString() + ")"
      | Star r -> (r.ToString() |> addParens) + "*"

and [<CustomComparison; CustomEquality>] T = 
   | Regex of HashCons<Re>
   
   override this.ToString() = 
      match this with
      | Regex(re) -> string re.Node
   
   interface System.IComparable with
      member x.CompareTo other = 
         match other with
         | :? T as y -> 
            let (Regex(x)) = x
            let (Regex(y)) = y
            x.Id - y.Id
         | _ -> failwith "cannot compare values of different types"
   
   override x.Equals(other) = 
      match other with
      | :? T as y -> 
         let (Regex(x)) = x
         let (Regex(y)) = y
         x.Id = y.Id
      | _ -> false
   
   override x.GetHashCode() = 
      let (Regex(x)) = x
      x.Hash

type Automaton = 
   { q0 : int
     Q : Set<int>
     F : Set<int>
     trans : Map<int * Set<string>, int> }
   override this.ToString() = 
      let header = "=======================\n"
      let states = sprintf "States: %s\n" (Util.Set.toString this.Q)
      let init = sprintf "Initial: %d\n" this.q0
      let final = sprintf "Final: %s\n" (Util.Set.toString this.F)
      
      let trans = 
         Map.fold (fun acc (q, S) v -> 
            let t = sprintf "  State: %d, chars: %s ---> %d\n" q (Util.Set.toString S) v
            acc + t) "" this.trans
      
      let trans = sprintf "Transitions:\n%s" trans
      sprintf "%s%s%s%s%s%s" header states init final trans header

let builder = HashConsBuilder()
let mkNode x = Regex(builder.Hashcons x)

let rec rev (re : T) : T = 
   let (Regex(r)) = re
   match r.Node with
   | Empty | Epsilon | Locs _ | Out _ -> re
   | Concat rs -> mkNode (Concat(List.rev rs |> List.map rev))
   | Inter rs -> mkNode (Inter(List.map rev rs))
   | Union rs -> mkNode (Union(List.map rev rs))
   | Negate r -> mkNode (Negate(rev r))
   | Star r -> mkNode (Star(rev r))

let rec insertOrdered rs r = 
   match rs with
   | [] -> [ r ]
   | rhd :: rtl -> 
      let cmp = compare r rhd
      if cmp < 0 then r :: rs
      elif cmp = 0 then rs
      else rhd :: (insertOrdered rtl r)

let rec insertOrderedAll dups rs1 rs2 = 
   match rs2 with
   | [] -> rs1
   | hd :: tl -> insertOrderedAll dups (insertOrdered rs1 hd) tl

let empty = mkNode (Empty)
let epsilon = mkNode (Epsilon)
let locs s = mkNode (Locs s)
let loc s = mkNode (Locs(Set.singleton s))
let out S = mkNode (Out S)

let star (re : T) = 
   let (Regex(r)) = re
   match r.Node with
   | Star _ -> re
   | Epsilon -> epsilon
   | Empty -> epsilon
   | _ -> mkNode (Star re)

let negate alphabet (re : T) = 
   let (Regex(r)) = re
   match r.Node with
   | Negate _ -> re
   | Locs s -> mkNode (Locs(Set.difference alphabet s))
   | _ -> mkNode (Negate re)

let rec concat (r1 : T) (r2 : T) = 
   let (Regex(re1)) = r1
   let (Regex(re2)) = r2
   match re1.Node, re2.Node with
   | _, Empty -> empty
   | Empty, _ -> empty
   | _, Epsilon -> r1
   | Epsilon, _ -> r2
   | Concat rs1, Concat rs2 -> mkNode (Concat(List.append rs1 rs2))
   | Concat rs, _ -> mkNode (Concat(List.append rs [ r2 ]))
   | _, Concat rs -> mkNode (Concat(r1 :: rs))
   | _, _ -> concat (mkNode (Concat [ r1 ])) r2

let concatAll (res : T list) = 
   match res with
   | [] -> empty
   | _ -> Util.List.fold1 concat res

(* TODO: negate empty == alphabet check for locs *)
let rec inter (r1 : T) (r2 : T) = 
   if r1 = r2 then r1
   else 
      let (Regex(re1)) = r1
      let (Regex(re2)) = r2
      match re1.Node, re2.Node with
      | _, Empty -> empty
      | Empty, _ -> empty
      | _, Negate(Regex({ Node = Empty })) -> r1
      | Negate(Regex({ Node = Empty })), _ -> r2
      | Inter rs1, Inter rs2 -> mkNode (Inter(insertOrderedAll false rs1 rs2))
      | Inter rs, _ -> mkNode (Inter(insertOrdered rs r2))
      | _, Inter rs -> mkNode (Inter(insertOrdered rs r1))
      | _, _ -> inter (mkNode (Inter [ r1 ])) r2

let interAll (res : T list) = 
   match res with
   | [] -> empty
   | _ -> Util.List.fold1 inter res

let rec union (r1 : T) (r2 : T) = 
   if r1 = r2 then r1
   else 
      let (Regex(re1)) = r1
      let (Regex(re2)) = r2
      match re1.Node, re2.Node with
      (* rewrite x;y + x;z = x;(y+z) *)
      | x, Concat(Regex(hd') as hd :: tl) when x = hd'.Node -> 
         concat hd (union epsilon (concatAll tl))
      | Concat(Regex(hd') as hd :: tl), x when x = hd'.Node -> 
         concat hd (union epsilon (concatAll tl))
      | Concat(hd1 :: tl1), Concat(hd2 :: tl2) when hd1 = hd2 -> 
         concat hd1 (union (concatAll tl1) (concatAll tl2))
      (* rewrite variants of 1 + y;y* = y* *)
      | Epsilon, Concat [ y1; Regex({ Node = Star y2 }) ] when y1 = y2 -> star y2
      | Epsilon, Concat [ Regex({ Node = Star y1 }); y2 ] when y1 = y2 -> star y2
      | Concat [ y1; Regex({ Node = Star y2 }) ], Epsilon when y1 = y2 -> star y2
      | Concat [ y1; Regex({ Node = Star y2 }) ], Epsilon when y1 = y2 -> star y2
      (* smart constructors *)
      | _, Empty -> r1
      | Empty, _ -> r2
      | _, Negate(Regex({ Node = Empty })) -> r2
      | Negate(Regex({ Node = Empty })), _ -> r1
      | Locs r, Locs s -> mkNode (Locs(Set.union r s))
      | Union rs1, Union rs2 -> mkNode (Union(insertOrderedAll false rs1 rs2))
      | Union rs, _ -> mkNode (Union(insertOrdered rs r2))
      | _, Union rs -> mkNode (Union(insertOrdered rs r1))
      | _, _ -> union (mkNode (Union [ r1 ])) r2

let unionAll (res : T list) = 
   match res with
   | [] -> empty
   | _ -> Util.List.fold1 union res

let rec nullable (r : T) = 
   let (Regex(re)) = r
   match re.Node with
   | Epsilon -> epsilon
   | Locs _ -> empty
   | Empty -> empty
   | Concat rs | Inter rs -> Util.List.fold (fun acc r -> inter acc (nullable r)) epsilon rs
   | Union rs -> Util.List.fold (fun acc r -> union acc (nullable r)) empty rs
   | Star r -> epsilon
   | Negate r -> 
      match nullable r with
      | Regex({ Node = Empty }) -> epsilon
      | _ -> empty
   | Out _ -> unreachable()

let conserv r s = 
   seq { 
      for x in r do
         for y in s do
            yield Set.intersect x y
   }
   |> Set.ofSeq

let rec dclasses alphabet (r : T) = 
   let (Regex(re)) = r
   match re.Node with
   | Empty | Epsilon -> Set.singleton alphabet
   | Locs s -> 
      assert not (Set.isEmpty s)
      Set.ofList [ s
                   Set.difference alphabet s ]
   | Concat rs -> 
      match rs with
      | [] -> failwith "impossible"
      | [ r ] -> dclasses alphabet r
      | r :: tl -> 
         if nullable r = empty then dclasses alphabet r
         else conserv (dclasses alphabet r) (dclasses alphabet (mkNode (Concat tl)))
   | Inter rs | Union rs -> 
      List.fold (fun acc r -> conserv acc (dclasses alphabet r)) (Set.singleton alphabet) rs
   | Star r -> dclasses alphabet r
   | Negate r -> dclasses alphabet r
   | Out _ -> unreachable()

let rec derivative alphabet a (r : T) = 
   let (Regex(re)) = r
   match re.Node with
   | Epsilon | Empty -> empty
   | Locs s -> 
      if s.Contains(a) then epsilon
      else empty
   | Concat rs -> 
      match rs with
      | [] | [ _ ] -> failwith "impossible"
      | x :: y :: tl -> 
         let y = 
            if List.isEmpty tl then y
            else mkNode (Concat(y :: tl))
         union (concat (derivative alphabet a x) y) (concat (nullable x) (derivative alphabet a y))
   | Inter rs -> 
      rs
      |> List.map (fun r -> derivative alphabet a r)
      |> List.fold1 inter
   | Union rs -> List.fold (fun acc r -> union acc (derivative alphabet a r)) empty rs
   | Negate r' -> negate alphabet (derivative alphabet a r')
   | Star r' -> concat (derivative alphabet a r') r
   | Out _ -> unreachable()

let rec goto alphabet q (Q, trans) S = 
   let c = Set.minElement S
   let qc = derivative alphabet c q
   if Set.exists ((=) qc) Q then (Q, Map.add (q, S) qc trans)
   else 
      let Q' = Set.add qc Q
      let trans' = Map.add (q, S) qc trans
      explore alphabet Q' trans' qc

and explore alphabet Q trans q = 
   let charClasses = Set.remove Set.empty (dclasses alphabet q)
   Set.fold (goto alphabet q) (Q, trans) charClasses

let indexStates (q0, Q, F, trans) = 
   let aQ = Set.toArray Q
   let mutable Q' = Set.empty
   let idxMap = Dictionary()
   for i = 0 to Array.length aQ - 1 do
      Q' <- Set.add i Q'
      idxMap.[aQ.[i]] <- i
   let q0' = idxMap.[q0]
   let F' = Set.map (fun q -> idxMap.[q]) F
   let trans' = 
      Map.fold (fun acc (re, c) v -> Map.add (idxMap.[re], c) idxMap.[v] acc) Map.empty trans
   (q0', Q', F', trans')

let makeDFA alphabet r = 
   let q0 = r
   let (Q, trans) = explore alphabet (Set.singleton q0) Map.empty q0
   let F = Set.filter (fun q -> nullable q = epsilon) Q
   let (q0', Q', F', trans') = indexStates (q0, Q, F, trans)
   { q0 = q0'
     Q = Q'
     F = F'
     trans = trans' }

let getAlphabet (topo : Topology.T) = 
   let (inStates, outStates) = Topology.alphabet topo
   let inside = Set.map (fun (s : Topology.Node) -> s.Loc) inStates
   let outside = Set.map (fun (s : Topology.Node) -> s.Loc) outStates
   let alphabet = Set.union inside outside
   (inside, outside, alphabet)

let startingLocs (dfa : Automaton) : Set<string> = 
   let transitions = 
      dfa.trans
      |> Map.toSeq
      |> Seq.map (fun ((x, _), z) -> (x, z))
      |> Seq.groupBy fst
      |> Seq.map (fun (k, ss) -> (k, Set.ofSeq (Seq.map snd ss)))
      |> Map.ofSeq
   let s = System.Collections.Generic.Stack()
   let mutable marked = Set.singleton dfa.q0
   let mutable edgeTo = Map.add dfa.q0 dfa.q0 Map.empty
   s.Push dfa.q0
   while s.Count > 0 do
      let v = s.Pop()
      for w in Map.find v transitions do
         if not (marked.Contains w) then 
            edgeTo <- Map.add w v edgeTo
            marked <- Set.add w marked
            s.Push w
   let marked = marked
   Map.fold (fun acc (i, ss) j -> 
      if dfa.F.Contains j && marked.Contains i then Set.union acc ss
      else acc) Set.empty dfa.trans

let emptinessAux dfa start = 
   let transitions = 
      dfa.trans
      |> Map.toSeq
      |> Seq.map (fun ((x, _), z) -> (x, z))
      |> Seq.groupBy fst
      |> Seq.map (fun (k, ss) -> (k, Set.ofSeq (Seq.map snd ss)))
      |> Map.ofSeq
   
   let s = System.Collections.Generic.Stack()
   let mutable marked = Set.singleton start
   let mutable edgeTo = Map.add start start Map.empty
   s.Push start
   while s.Count > 0 do
      let v = s.Pop()
      for w in Map.find v transitions do
         if not (marked.Contains w) then 
            edgeTo <- Map.add w v edgeTo
            marked <- Set.add w marked
            s.Push w
   let reachableFinal = Set.filter dfa.F.Contains marked
   if reachableFinal.Count > 0 then 
      let mutable path = []
      let x = ref (Seq.head (Set.toSeq reachableFinal))
      while !x <> start do
         let y = !x
         x := Map.find !x edgeTo
         let (_, ss) = Map.findKey (fun (i, ss) j -> i = !x && j = y) dfa.trans
         path <- (Set.minElement ss) :: path
      Some path
   else None

let emptiness dfa = emptinessAux dfa dfa.q0