module Program

open System.Net
open Akka.Actor
open Akka.FSharp
open BetterTTD.Actors
open BetterTTD.Actors.Messages
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Hosting

type EnrollCfg =
    { Host : string
      Port : int
      Pass : string
      Name : string
      Tag  : string
      Ver  : string }

type SystemService(system : IActorRefFactory) =
    let mutable actors = Map.empty
    
    member this.EnrollClient (cfg : EnrollCfg) =
        if actors.ContainsKey(cfg.Tag) then
            Error $"Client already added for tag #{cfg.Tag}"
        else
            let coordinatorCfg = (IPAddress.Parse(cfg.Host), cfg.Port, cfg.Tag)
            let ref = spawn system cfg.Tag (Coordinator.init coordinatorCfg)
            ref <! AuthorizeMsg { Name = cfg.Name; Pass = cfg.Pass; Version = cfg.Ver }
            actors <- actors.Add(cfg.Tag, ref)
            Ok ()
            
    member this.DisenrollClient (tag : string) =
        match actors.TryFind tag with
        | Some ref ->
            ref <! PoisonPill.Instance
            actors <- actors.Remove tag
            Ok ()
        | None -> Error $"Client was not found for tag #{tag}"

let enrollHandler (enroll : EnrollCfg) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.GetService<SystemService>().EnrollClient enroll with
        | Ok    _   -> text "Enrolled successfully" next ctx
        | Error err -> RequestErrors.badRequest (text err) next ctx

let disenrollHandler (tag : string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        match ctx.GetService<SystemService>().DisenrollClient tag with
        | Ok    _   -> text "Disenrolled successfully" next ctx
        | Error err -> RequestErrors.badRequest (text err) next ctx

let webApp =
    choose [
        subRoute "/api"
            (choose [
                POST   >=> route "/enroll" >=> bindJson<EnrollCfg> enrollHandler
                DELETE >=> routef "/disenroll/%s" disenrollHandler
            ])
        RequestErrors.NOT_FOUND "Not Found"
    ]

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    let system = Configuration.defaultConfig() |> System.create "tg"
    services.AddSingleton<IActorRefFactory>(system) |> ignore
    services.AddSingleton<SystemService>() |> ignore
    services.AddGiraffe() |> ignore
    services.AddMemoryCache() |> ignore

[<EntryPoint>]
let main _ =
    System.Console.OutputEncoding <- System.Text.Encoding.UTF8
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    |> ignore)
        .Build()
        .Run()
    0