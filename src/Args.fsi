module Args

type T = 
   { PolFile : string option
     TopoFile : string option
     OutDir : string
     DebugDir : string
     Debug : bool
     FlowletTableSize : int
     FlowletTimeout : int
     LinkTimeout : int
     ProbePeriod : int
     LoopThreshold : int
     Minimize : bool
     Optimize : bool
     Stats : bool
     IsAbstract : bool 
     IsTemplate : bool
     TopoType : string
     NumEndHosts : int }

/// Get the command-line settings
val getSettings : unit -> T 
/// Update the settings
val changeSettings : T -> unit
/// Parse command line arguments and return the compiler settings
val parse : string [] -> unit
