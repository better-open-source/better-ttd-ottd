open System
open System.Net
open System.Text
open Akka.FSharp
open BetterTTD.Actors
open BetterTTD.Actors.Messages

[<EntryPoint>]
let main _ =
    Console.OutputEncoding <- Encoding.UTF8
    
    let host = IPAddress.Parse("194.87.232.129")
    let system = Configuration.defaultConfig() |> System.create "tg"
    
    let welcomeRef =
        Coordinator.init (host, 3980)
        |> spawn system "welcome-coordinator" 
    
    let vanillaRef =
        Coordinator.init (host, 3983)
        |> spawn system "vanilla-coordinator"
        
    welcomeRef <! AuthorizeMsg { Name = "TG Welcome"; Pass = ""; Version = "1.0" }
    vanillaRef <! AuthorizeMsg { Name = "TG Vanilla"; Pass = ""; Version = "1.0" }
    
    Console.Read() |> ignore
    0