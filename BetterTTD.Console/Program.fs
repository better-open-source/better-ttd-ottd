open System
open System.Net
open Akka.FSharp
open BetterTTD.Actors
open BetterTTD.Actors.Messages

[<EntryPoint>]
let main _ =
    let srvCfg = (IPAddress.Parse("127.0.0.1"), 3977)
    let authMsg =
      AuthorizeMsg
          { Name    = "TG Welcome"
            Pass    = "p7gvv"
            Version = "1.0" }
    
    let system = Configuration.defaultConfig() |> System.create "tg-welcome"
    let coordinatorRef = Coordinator.init srvCfg |> spawn system "coordinator" 
    
    coordinatorRef <! authMsg
    
    Console.Read() |> ignore
    0