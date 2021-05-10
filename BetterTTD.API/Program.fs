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

type Enroll =
    { Host : string
      Port : int
      Pass : string
      Name : string
      Tag  : string
      Ver  : string }

let enrollHandler (enroll : Enroll) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let isOk, host = IPAddress.TryParse(enroll.Host)
        if isOk then
            let system = ctx.GetService<ActorSystem>()
            let actorRef = spawn system enroll.Tag (Coordinator.init (host, enroll.Port))
            actorRef <! AuthorizeMsg { Name = enroll.Name; Pass = enroll.Pass; Version = enroll.Ver }
            text (System.Guid.NewGuid().ToString()) next ctx
        else RequestErrors.badRequest (text "Invalid host address") next ctx

let disenrollHandler (guid : System.Guid) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        text (guid.ToString()) next ctx

let webApp =
    choose [
        subRoute "/api"
            (choose [
                POST   >=> route "/enroll" >=> bindJson<Enroll> enrollHandler
                DELETE >=> routef "/disenroll/%O" disenrollHandler
            ])
        RequestErrors.NOT_FOUND "Not Found"
    ]

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    let system = Configuration.defaultConfig() |> System.create "tg"
    services.AddSingleton(system) |> ignore
    services.AddGiraffe() |> ignore
    services.AddMemoryCache() |> ignore

[<EntryPoint>]
let main _ =
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