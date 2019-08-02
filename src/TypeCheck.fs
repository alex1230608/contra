module TypeCheck

open Ast

type Type = 
    | Number
    | Boolean
    | Product of Type list 
    override t.ToString() = 
       match t with
       | Number -> "Number"
       | Boolean -> "Boolean"
       | Product ts -> 
          let strs = List.map string ts
          sprintf "(%s)" (Util.String.join strs ",")

type Context = Map<string, Type>

let check (ast : Ast.T) (e : Ast.Expr) (actual : Type) (expected : Type) = 
   if actual <> expected then
      let s1, s2 = string expected, string actual
      let msg = sprintf "expected type %s, but received value of type %s" s1 s2
      error ast msg e.Pos

let unify (ast : Ast.T) (_ : Ast.Expr) (e2 : Ast.Expr) (t1 : Type) (t2 : Type) = 
   if t1 <> t2 then 
      let s1, s2 = string t1, string t2
      let msg = sprintf "expected type %s, but received value of type %s" s1 s2
      error ast msg e2.Pos
   else t1

let rec regexCheck (alphabet : Set<string>) ast (r : Ast.Re) : unit = 
   match r.Value with 
   | Dot -> ()
   | Loc l -> 
      if Set.contains l.Name alphabet then () 
      else 
         let msg = sprintf "invalid topology location %s" l.Name
         error ast msg r.Pos
   | Par(s,t) | Seq(s,t) -> 
      regexCheck alphabet ast s
      regexCheck alphabet ast t
   | Star s -> regexCheck alphabet ast s

let rec typeCheckAux (alphabet : Set<string>) (ctx : Context) (ast : Ast.T) (e : Ast.Expr) : Type =
    match e.Node with 
    | IntLiteral _ -> Number
    | And(e1, e2) | Or(e1,e2) ->
       let t1 = typeCheckAux alphabet ctx ast e1
       let t2 = typeCheckAux alphabet ctx ast e2
       check ast e1 t1 Boolean
       check ast e2 t2 Boolean
       Boolean
    | Not e -> 
       let t = typeCheckAux alphabet ctx ast e 
       check ast e t Boolean 
       Boolean
    | Plus(e1,e2) | Times(e1,e2) ->
       let t1 = typeCheckAux alphabet ctx ast e1 
       let t2 = typeCheckAux alphabet ctx ast e2 
       check ast e1 t1 Number 
       check ast e2 t2 Number
       Number
    | Max es | Min es -> 
       for e in es do 
          let t = typeCheckAux alphabet ctx ast e 
          check ast e t Number 
       Number
    | If(e1,e2,e3) -> 
       let t1 = typeCheckAux alphabet ctx ast e1
       let t2 = typeCheckAux alphabet ctx ast e2
       let t3 = typeCheckAux alphabet ctx ast e3
       check ast e1 t1 Boolean 
       unify ast e2 e3 t2 t3
    | Let(id,e1,e2) -> 
       let t1 = typeCheckAux alphabet ctx ast e1
       let ctx' = Map.add id.Name t1 ctx 
       typeCheckAux alphabet ctx' ast e2
    | PathAttribute(id,_) -> 
        match id.Name with 
        | "capacity" | "queue" | "length" | "util" | "latency" -> Number
        | _ -> 
           let msg = sprintf "invalid path attribute %s" id.Name
           error ast msg e.Pos
    | Matches r | Exists r -> 
        regexCheck alphabet ast r
        Boolean
    | Ident id ->
        match Map.tryFind id.Name ctx with 
        | None ->
           let msg = sprintf "unbound variable %s" id.Name 
           error ast msg e.Pos
        | Some t -> t
    | Gt(e1, e2) | Lt(e1,e2) | Geq(e1,e2) | Leq(e1,e2) -> 
       let t1 = typeCheckAux alphabet ctx ast e1
       let t2 = typeCheckAux alphabet ctx ast e2
       check ast e1 t1 Number
       check ast e2 t2 Number
       Boolean
    | Tuple es -> 
       let ts = List.map (typeCheckAux alphabet ctx ast) es
       let ets = List.zip es ts 
       for (e,t) in ets do 
          check ast e t Number
       Product ts

let run ast = 
   let alphabet = ast.TopoInfo.SelectGraphInfo.InternalNames
   let typ = typeCheckAux alphabet Map.empty ast ast.OptFunction
   match typ with 
   | Number -> Product [Number] 
   | Product _ -> typ
   | Boolean -> 
      let msg = "Top level function must be a number or tuple, but got a boolean"
      error ast msg ast.OptFunction.Pos
