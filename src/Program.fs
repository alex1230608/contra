module Program

open Util.Format


[<EntryPoint>]
let main argv = 
   ignore (Args.parse argv)
   let settings = Args.getSettings()
   // Grab topology
   let (topoInfo, settings), _ = 
      match settings.TopoFile with
      | None -> errorLine "No topology file specified, use --help to see options"
      | Some f -> Util.Profile.time Topology.readTopology f
   // Grab policy
   let polFile = 
      match settings.PolFile with
      | None -> errorLine "No policy file specified, use --help to see options"
      | Some polFile -> polFile 
   // Parse into an AST
   let ast = Input.readFromFile topoInfo polFile
   // Type check the policy
   let typ = TypeCheck.run ast 
   // Check for the isotonicity property
   Isotonicity.run ast
   // Break a non-isotonic policy into multiple isotonic policies
   printfn "Ast: %s" (string ast.OptFunction)
   let ast, probeMap = Isotonicity.annotateAst ast
   printfn "Annot: %s" (string ast.OptFunction)
   // Generate the combined graph
   let graph = CGraph.run ast
   // Minimize the combined graph
   // let graph = 
   //   if settings.Minimize 
   //   then CGraph.Minimize.minimize graph 
   //   else graph
   // Create a minimal number of "local" tags for each device
   CGraph.compress graph
   // Generate the P4 programs
   let baseDir = System.AppDomain.CurrentDomain.BaseDirectory
   let baseDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir,"../../../"))
   let env = baseDir + "environment"
   Util.File.createDir settings.OutDir
   Util.File.createDir settings.DebugDir
   Util.File.directoryCopy env settings.OutDir true
   if settings.Debug then
      let file = settings.DebugDir + Util.File.sep + "product-graph"
      CGraph.generatePNG graph file
   CodeGen.generate graph ast probeMap typ
   0