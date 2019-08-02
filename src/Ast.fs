module Ast

open Util.Format
open System

type Position = 
   { SLine : int
     SCol : int
     ELine : int
     ECol : int }

type Ident = 
   { Pos : Position
     Name : string }
   override i.ToString() = i.Name

type Re =
   { Pos: Position 
     Value: Regex }
   override r.ToString() = string r.Value

and Regex = 
   | Dot
   | Loc of Ident
   | Seq of Re * Re 
   | Par of Re * Re 
   | Star of Re
   override r.ToString() = 
      match r with 
      | Dot -> "."
      | Loc l -> l.Name
      | Seq(s,t) -> sprintf "(%s;%s)" (string s) (string t)
      | Par(s,t) -> sprintf "(%s + %s)" (string s) (string t)
      | Star s -> string s + "*"

type Expr = 
   { Pos : Position
     Node : Node }

   override e.ToString() = string e.Node

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

   override n.ToString() = 
      let rec aux indent node = 
         let ind = indent + "    "
         match node with
         | Let(id,e1,e2) -> 
            sprintf "%slet %s = %s in\n%s" 
               indent (string id) (string e1) (aux indent e2.Node)
         | If(e1,e2,e3) -> 
            sprintf "(if %s then %s else %s)"
               (string e1) (aux ind e2.Node) (aux ind e3.Node)
         | Min es -> 
            let strs = List.map string es 
            sprintf "min(%s)" (Util.String.join strs ",")
         | Max es ->
            let strs = List.map string es 
            sprintf "max(%s)" (Util.String.join strs ",")
         | Times(e1,e2) -> sprintf "%s*%s" (string e1) (string e2)
         | Plus(e1,e2) -> sprintf "(%s + %s)" (string e1) (string e2)
         | And(e1,e2) -> sprintf "%s and %s" (string e1) (string e2)
         | Or(e1,e2) -> sprintf "(%s or %s)" (string e1) (string e2)
         | Not e -> sprintf "not (%s)" (string e) 
         | Matches r -> sprintf "matches(%s)" (string r)
         | Exists r -> sprintf "exists(%s)" (string r)
         | Ident id -> string id
         | IntLiteral i -> string i
         | PathAttribute(id,num) -> sprintf "path.%s[%d]" (string id) num
         | Geq(e1,e2) -> sprintf "(%s >= %s)" (string e1) (string e2)
         | Leq(e1,e2) -> sprintf "(%s <= %s)" (string e1) (string e2)
         | Gt(e1,e2) -> sprintf "(%s > %s)" (string e1) (string e2)
         | Lt(e1,e2) -> sprintf "(%s < %s)" (string e1) (string e2)
         | Tuple es ->
            let strs = List.map string es 
            sprintf "(%s)" (Util.String.join strs ",")
      aux "" n

let rec iter f e = 
   f e
   match e.Node with
   | If(e1,e2,e3) -> iter f e1; iter f e2; iter f e3
   | Min es 
   | Max es
   | Tuple es -> List.iter (iter f) es
   | Let(_,e1,e2) 
   | Times(e1,e2) 
   | Plus(e1,e2) 
   | And(e1,e2) 
   | Or(e1,e2) 
   | Geq(e1,e2) 
   | Leq(e1,e2) 
   | Gt(e1,e2) 
   | Lt(e1,e2) -> iter f e1; iter f e2
   | Not e -> iter f e
   | Matches _ 
   | Exists _
   | Ident _
   | IntLiteral _ 
   | PathAttribute _ -> ()


type T = 
   { Input : string []
     TopoInfo : Topology.TopoInfo
     OptFunction : Expr }



type MessageKind = 
   | Error
   | Warning

let dummyPos = 
   { SLine = -1
     SCol = -1
     ELine = -1
     ECol = -1 }

let msgOffset = 9
let maxLineMsg = 4
let obj = new Object()

let range (x : Position) (y : Position) = 
   { SLine = x.SLine
     SCol = x.SCol
     ELine = y.ELine
     ECol = y.ECol }

let inline count (c : char) (s : string) = s.Split(c).Length - 1
let inline firstNonSpace (s : string) : int = 
   Array.findIndex (fun c -> c <> ' ' && c <> '\t') (s.ToCharArray())

let displayLine (s : string option) (maxLineNo : int) (line : int) = 
   let s = 
      match s with
      | None -> "..."
      | Some x -> x
   
   let sl = string line
   let spaces = String.replicate (maxLineNo - sl.Length + 1) " "
   let sl = sl + spaces + "|"
   let len = sl.Length
   writeColor sl ConsoleColor.DarkGray
   printf "%s%s" (String.replicate (msgOffset - len) " ") s

let displayMultiLine ast p ccolor = 
   let mutable longestLineNo = 0
   let mutable minLineStart = Int32.MaxValue
   for i = p.SLine to p.ELine do
      let str = ast.Input.[i - 1]
      longestLineNo <- max (string i).Length longestLineNo
      minLineStart <- min minLineStart (firstNonSpace str)
   let lines = min maxLineMsg (p.ELine - p.SLine)
   for i = p.SLine to p.SLine + lines do
      let str = ast.Input.[i - 1]
      let str = str.[minLineStart..]
      
      let msg = 
         if i = p.SLine + maxLineMsg then None
         else Some str
      displayLine msg longestLineNo i
      printfn ""
   printfn ""

let displaySingleLine ast p ccolor = 
   let str = ast.Input.[p.SLine - 1]
   let lineStart = firstNonSpace str
   let str = str.Trim()
   let spaces = String.replicate (p.SCol - lineStart + msgOffset) " "
   let s = String.replicate (p.ECol - p.SCol) "~"
   let len = (string p.SLine).Length
   displayLine (Some str) len p.SLine
   printf "\n%s" spaces
   writeColor s ccolor
   printfn ""

let displayFooter msg (color, errorTyp) = 
   writeColor errorTyp color
   printfn "%s" (wrapText msg)
   writeFooter()

let colorInfo (kind : MessageKind) = 
   match kind with
   | Warning -> ConsoleColor.DarkYellow, "Warning: "
   | Error -> ConsoleColor.DarkRed, "Error:   "

let issueAst (ast : T) (msg : string) (p : Position) (kind : MessageKind) = 
   let settings = Args.getSettings()
   let ccolor, errorTyp = colorInfo kind
   writeHeader()
   if p.SLine = p.ELine then displaySingleLine ast p ccolor
   else displayMultiLine ast p ccolor
   displayFooter msg (ccolor, errorTyp)

let validate p = 
   if p = dummyPos then 
      printfn "Internal positioning error"
      exit 0

let error ast msg p = 
   validate p
   lock obj (fun () -> 
      issueAst ast msg p Error
      exit 0)

let warning ast msg p = 
   validate p
   lock obj (fun () -> issueAst ast msg p Warning)