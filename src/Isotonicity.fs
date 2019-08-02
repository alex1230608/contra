module Isotonicity

open Ast
open Util

type Attribute = string

type Id = string

type Context = Map<Id, Set<Attribute>>

let checkEqAttrs ast e a1 a2 = 
   if a1 = a2 then a1 
   else 
      let msg = sprintf "possibly non-isotone function found"
      error ast msg e.Pos

let rec checkIsotone (ast : Ast.T) (ctx : Context) (e : Ast.Expr) : Set<Attribute> = 
    match e.Node with 
    | IntLiteral _ | And _ | Or _ | Not _ | Matches _ 
    | Exists _ | Gt _ | Lt _ | Geq _ | Leq _ -> Set.empty
    | Plus(e1,e2) | Times(e1,e2) -> 
         let x = checkIsotone ast ctx e1
         let y = checkIsotone ast ctx e2
         let cx = Set.count x 
         let cy = Set.count y
         match cx, cy with 
         | 0, _ -> y 
         | _, 0 -> x 
         | 1, 1 ->
            let x = Set.minElement x 
            let y = Set.minElement y
            if x = y then Set.singleton x 
            else 
               let msg = sprintf "possibly non-isotone function found"
               error ast msg e.Pos
         | _, _ -> 
            printfn "%d,%d" cx cy
            let msg = sprintf "possibly non-isotone function found"
            error ast msg e.Pos
    | Max _ | Min _ -> failwith "Not implemented"
    | If(_,e2,e3) ->
         let x = checkIsotone ast ctx e2
         let y = checkIsotone ast ctx e3
         Set.union x y
    | Let(id,e1,e2) ->
         let x = checkIsotone ast ctx e1
         let ctx' = Map.add id.Name x ctx
         checkIsotone ast ctx' e2
    | PathAttribute(id,_) -> Set.singleton (id.Name)
    | Ident id -> Map.find id.Name ctx
    | Tuple es -> 
         List.map (checkIsotone ast ctx) es 
         |> List.fold Set.union Set.empty


let run (ast : Ast.T) = 
    checkIsotone ast Map.empty ast.OptFunction |> ignore


// A function to partition the expression into a combination of subexpressions
// such that each subexpression is isotone
//
// if * then 
//    (1, path.util)
// else 
//    (2, path.length)
//
// should annotate the attribute with a probe number:
//
// if * then 
//    (1, path.util @1)
// else 
//    (2, path.length @2) 
//
// where @1 means we draw from probe #1, and @2 means we draw from probe #2
// We can not use the same probe to compute the best path overall, because
// it is not isotone in this case. A downstream router may choose a best path 
// in a different branch of the if, than what we ultimately want. 
// However, each individually is isotone. By sending out multiple probes, we 
// can find the best choice in each case, and then let the source decide. 
//
// Another example is the following:
//
// if * then
//    (1, path.length, path.util)
// else
//    (2, path.length, path.util)
//
// This prefers certain paths in certain cases, but otherwise uses the same 
// tie-breaking critiera. In this case we should annotate the function as follows:
//
// if * then
//    (1, path.length @1, path.util @1)
// else
//    (2, path.length @1, path.util @1)
//
// Even though there is a branch, the order of comparisons of attributes
// is equal, and a single probe should be enough to ensure isotonicity.
// As a final example, consider the policy:
//
// (1, if * then path.util else path.length)
//
// This should be annotated as:
//
// (1, if * then path.util @1 else path.length @2)
//
// A more complicated example would be the following policy:
//
// if path.util < .8 then
//   if matches(.* M .*) then
//     (1, path.util)
//   else 
//     (2, path.util)
// else 
//   (3, path.len)
//
// We should only need two probes to evaluate this function:
// one for each of the two outer branches. The first probe
// can evaluate the entire inner conditional itself.
//

type Element = 
    | Constant 
    | Attribute of string 

type AttrType = Element list

type Env = Map<Id, Set<AttrType>>

let rec getAttrTypes ast (env : Env) (e : Ast.Expr) : Set<AttrType> = 
    match e.Node with 
    | PathAttribute(id,_) -> Set.singleton [Attribute id.Name]
    | IntLiteral _ -> Set.singleton [Constant]
    | Ident x -> Map.find x.Name env
    | If(_,e1,e2) -> 
        let ts1 = getAttrTypes ast env e1 
        let ts2 = getAttrTypes ast env e2
        Set.union ts1 ts2
    | Let(id,e1,e2) ->
        let ts1 = getAttrTypes ast env e1 
        let env' = Map.add id.Name ts1 env 
        getAttrTypes ast env' e2
    | Plus(e1,e2) | Times(e1,e2) ->
        let ts1 = getAttrTypes ast env e1 
        let ts2 = getAttrTypes ast env e2 
        let ret = Set.union ts1 ts2
        if (Set.count ret > 1) then Set.remove [Constant] ret else ret
    | Tuple es -> 
        let ts = List.map (getAttrTypes ast env) es
        let mutable xs = []
        for set in ts do 
            if Set.count set > 1 then 
                let msg = sprintf "if statement inside tuple"
                error ast msg e.Pos
            xs <- (Set.minElement set |> List.head) :: xs
        Set.singleton (List.rev xs)
    | Max _ | Min _ -> failwith "unreachable"
    | And _ | Or _ | Not _ | Geq _ | Gt _ | Leq _ 
    | Lt _ | Matches _ | Exists _ -> failwith "unreachable"

let rec annotate ast (map : Map<AttrType,int>) (env : Env) (o : int option) (e : Ast.Expr) : Ast.Expr = 
    match e.Node with 
    | PathAttribute(id,_) -> 
        let i = 
            match o with 
            | None ->
                let typ = getAttrTypes ast env e |> Set.minElement
                Map.find typ map
            | Some i -> i
        {e with Node = PathAttribute(id,i)}
    | IntLiteral _ | Ident _ -> e
    | If(e1,e2,e3) -> 
        let e2 = annotate ast map env o e2
        let e3 = annotate ast map env o e3
        {e with Node = If(e1,e2,e3)}
    | Let(id,e1,e2) ->
        // update attrType env -- bit of a hack to do this twice
        let ts1 = getAttrTypes ast env e1 
        let env' = Map.add id.Name ts1 env 
        let e1 = annotate ast map env' o e1 
        let e2 = annotate ast map env' o e2
        {e with Node = Let(id,e1,e2)}
    | Plus(e1,e2) ->
        let e1 = annotate ast map env o e1 
        let e2 = annotate ast map env o e2
        {e with Node = Plus(e1,e2)}
    | Times(e1,e2) -> 
        let e1 = annotate ast map env o e1 
        let e2 = annotate ast map env o e2
        {e with Node = Times(e1,e2)}
    | Tuple es -> 
        let typ = getAttrTypes ast env e |> Set.minElement
        let i = Map.find typ map
        let es = List.map (annotate ast map env (Some i)) es
        {e with Node = (Ast.Tuple es)}
    | Max _ | Min _ -> failwith "unreachable"
    | And _ | Or _ | Not _ | Geq _ | Gt _ | Leq _ 
    | Lt _ | Matches _ | Exists _ -> failwith "unreachable"


let filt a = 
    match a with 
    | Constant -> None 
    | Attribute s -> Some s

let filterAttrs (attrs : AttrType) (i : int) = 
    (i, attrs)

let attributeMap (ast : Ast.T) : Map<AttrType,int> = 
    let typs = getAttrTypes ast Map.empty ast.OptFunction
    let mutable map = Map.empty 
    let mutable i = 0
    for set in typs do
        map <- Map.add set i map 
        i <- i + 1
    map

let annotateAst (ast : Ast.T) = 
    let map = attributeMap ast
    let e = annotate ast map Map.empty None ast.OptFunction
    let map' = Map.map filterAttrs map |> Map.toList |> List.map snd |> Map.ofList
    {ast with OptFunction = e}, map'