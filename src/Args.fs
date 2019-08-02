module Args

open DocoptNet
open System.IO

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

let currentDir = System.Environment.CurrentDirectory
let sep = string Path.DirectorySeparatorChar
let debugDir = ref (currentDir + sep + "debug" + sep)
let settings = ref None
let usage = """
Usage: hula [options]
       hula (--help | --version)

Options:
    -h, --help           Show this message.
    --version            Show the version number.
    --policy FILE        Propane policy file.
    --topo FILE          Network topology file (xml).
    --topotype TOPOTYPE  Network topology type (only ABILENE and FATTREE are supported).
    --numeh=<neh>        Number of end hosts per edge switch (default 2)
    --output DIR         Specify output directory.
    --fsize=<sz>         Flowlet table size (default 1024)
    --ftimeout=<fto>     Flowlet timeout in us (default 50000)
    --ltimeout=<lto>     Link failure detection timeout in us (default 800000)
    --probeperiod=<pp>   Probe period in us (default 262144)
    --lthresh=<lt>       Loop detection threshold (default 3)
    --minimize           Minimize the product graph
    --deoptimize         Do not optimize the p4 program
    --stats              Print compiler statistics
    --debug              Output debugging information.
"""

let checkFile f = 
   let inline adjustFilePath f = 
      if Path.IsPathRooted(f) then f
      else currentDir + sep + f
   if File.Exists f then adjustFilePath f
   else 
      let f' = currentDir + sep + f
      if File.Exists f' then adjustFilePath f'
      else 
         printfn "Invalid file: %s" f
         exit 0

let exitUsage() = 
   printfn "%s" usage
   exit 0

let getFile (vo : ValueObject) = 
   if vo = null then None
   else Some(string vo |> checkFile)

let getTopotype (vo : ValueObject) =
   if vo = null then "EMPTY"
   else (string vo)

let getNumeh (vo : ValueObject) = 
   if vo = null then 2
   else (int (string vo))

let getDir (vo : ValueObject) = 
   if vo = null then None
   else Some(string vo)

let getFsize (vo : ValueObject) = 
   if vo = null then 1024
   else (int (string vo))

let getFtimeout (vo : ValueObject) = 
   if vo = null then 50000
   else (int (string vo))

let getLtimeout (vo : ValueObject) =
   if vo = null then 800000
   else (int (string vo))

let getProbeperiod (vo : ValueObject) =
   if vo = null then 262144
   else (int (string vo))

let getLoopThreshold (vo : ValueObject) = 
   if vo = null then 3
   else (int (string vo))

let parse (argv : string []) : unit = 
   let d = Docopt()
   let vs = d.Apply(usage, argv, version = "Propane version 0.1", exit = true)
   if vs.["--help"].IsTrue then exitUsage()
   let outDir = getDir vs.["--output"]
   
   let outDir = 
      match outDir with
      | None -> currentDir + sep + "output"
      | Some d -> d
   debugDir := outDir + sep + "debug"
   let s = 
      { PolFile = getFile vs.["--policy"]
        TopoFile = getFile vs.["--topo"]
        TopoType = getTopotype vs.["--topotype"]
        NumEndHosts = getNumeh vs.["--numeh"]
        OutDir = outDir
        Debug = vs.["--debug"].IsTrue
        DebugDir = !debugDir
        FlowletTableSize = getFsize vs.["--fsize"]
        FlowletTimeout = getFtimeout vs.["--ftimeout"]
        LinkTimeout = getLtimeout vs.["--ltimeout"]
        ProbePeriod = getProbeperiod vs.["--probeperiod"]
        LoopThreshold = getLoopThreshold vs.["--lthresh"]
        Minimize = vs.["--minimize"].IsTrue
        Optimize = vs.["--deoptimize"].IsFalse
        Stats = vs.["--stats"].IsTrue
        IsAbstract = false
        IsTemplate = false } // these get set from the topology
   settings := Some s

let getSettings() = 
   match !settings with
   | Some s -> s
   | None -> 
      printfn "Error: no settings found"
      exit 0

let changeSettings s = settings := Some s
