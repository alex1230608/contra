module Ast

type Position = 
   { SLine : int
     SCol : int
     ELine : int
     ECol : int }

type Ident = 
   { Pos : Position
     Name : string }

type Re =
   { Pos: Position 
     Value: Regex }

and Regex = 
   | Dot
   | Loc of Ident
   | Seq of Re * Re 
   | Par of Re * Re 
   | Star of Re

type Expr = 
   { Pos : Position
     Node : Node }

and Node = 
   | Let of Ident * Expr * Expr
   | If of Expr * Expr * Expr 
   | Min of Expr list
   | Max of Expr list
   | Times of Expr * Expr
   | Plus of Expr * Expr
   | And of Expr * Expr 
   | Or of Expr * Expr 
   | Not of Expr 
   | Matches of Re
   | Exists of Re
   | Ident of Ident
   | IntLiteral of int
   | PathAttribute of Ident * int
   | Geq of Expr * Expr 
   | Leq of Expr * Expr 
   | Gt of Expr * Expr
   | Lt of Expr * Expr
   | Tuple of Expr list

type T = 
   { Input : string []
     TopoInfo : Topology.TopoInfo
     OptFunction : Expr }

val iter : (Expr -> unit) -> Expr -> unit

val error : T -> string -> Position -> 'a

val warning : T -> string -> Position -> unit