module TypeCheck

type Type = 
    | Number
    | Boolean
    | Product of Type list 

val run : Ast.T -> Type