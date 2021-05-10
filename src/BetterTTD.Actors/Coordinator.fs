module BetterTTD.Actors.Coordinator

open System
open System.Net
open System.Net.Sockets
open Akka.FSharp
open BetterTTD.Actors.Messages
open BetterTTD.MessageTransformer
open BetterTTD.Network.Enums
open BetterTTD.PacketTransformer

let private defaultPolls =
    [ { UpdateType = AdminUpdateType.ADMIN_UPDATE_CLIENT_INFO
        Data       = uint32 0xFFFFFFFF }
      { UpdateType = AdminUpdateType.ADMIN_UPDATE_COMPANY_INFO
        Data       = uint32 0xFFFFFFFF } ]
    |> List.map AdminPollMsg

let private defaultUpdateFrequencies =
    [ { UpdateType = AdminUpdateType.ADMIN_UPDATE_CHAT
        Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC }
      { UpdateType = AdminUpdateType.ADMIN_UPDATE_CLIENT_INFO
        Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC }
      { UpdateType = AdminUpdateType.ADMIN_UPDATE_COMPANY_INFO
        Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC } ]
    |> List.map AdminUpdateFreqMsg
    
let schedule (mailbox : Actor<_>) ref interval msg =
    mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(
        TimeSpan.FromMilliseconds 0.,
        TimeSpan.FromMilliseconds interval,
        ref, msg)
    
let private connectToStream (ipAddress : IPAddress) (port : int) =
    let tcpClient = new TcpClient ()
    tcpClient.Connect (ipAddress, port)
    tcpClient.GetStream ()

type Client =
    { Id        : uint32
      CompanyId : byte
      Name      : string
      Host      : string
      Language  : NetworkLanguage }

type Company =
    { Id          : byte
      Name        : string
      ManagerName : string
      Color       : Color
      HasPassword : bool }

type GameInfo =
    { ServerName : string
      NetworkRevision : string
      IsDedicated : bool
      Landscape : Landscape
      MapWidth : int
      MapHeight : int }

type State =
    { GameInfo  : GameInfo option
      Clients   : Client list
      Companies : Company list }
    static member Default = { GameInfo = None; Clients = []; Companies = [] }

let dispatch (state : State) (msg : PacketMessage) =
    match msg with
    | ServerProtocolMsg _ -> state

    | ServerWelcomeMsg msg ->
        let gameInfo =
            { ServerName      = msg.ServerName
              NetworkRevision = msg.NetworkRevision
              IsDedicated     = msg.IsDedicated
              Landscape       = msg.Landscape
              MapWidth        = msg.MapWidth
              MapHeight       = msg.MapHeight }
        { state with GameInfo = Some gameInfo} 

    | ServerClientJoinMsg _ -> state

    | ServerClientInfoMsg msg ->
        let client =
            { Id        = msg.ClientId
              CompanyId = msg.CompanyId
              Name      = msg.Name
              Host      = msg.Address
              Language  = msg.Language }
        let clients = state.Clients |> List.filter (fun cli -> cli.Id <> client.Id)
        { state with Clients = clients @ [ client ] }

    | ServerClientUpdateMsg msg ->
        match state.Clients |> List.tryFind (fun cli -> cli.Id = msg.ClientId) with
        | Some client ->
            let client  = { client with Name = client.Name; CompanyId = client.CompanyId }
            let clients = state.Clients |> List.filter (fun cli -> cli.Id <> client.Id)
            { state with Clients = clients @ [ client ] }
        | None -> state

    | ServerClientQuitMsg msg ->
        let clients = state.Clients |> List.filter (fun cli -> cli.Id <> msg.ClientId)
        { state with Clients = clients }

    | ServerClientErrorMsg msg ->
        let clients = state.Clients |> List.filter (fun cli -> cli.Id <> msg.ClientId)
        { state with Clients = clients }
    
    | ServerCompanyNewMsg _ -> state
    
    | ServerCompanyInfoMsg msg ->
        let company =
            { Id = msg.CompanyId
              Name = msg.CompanyName
              ManagerName = msg.ManagerName
              Color = msg.Color
              HasPassword = msg.HasPassword }
        let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> company.Id)
        { state with Companies = companies @ [ company ] }
    
    | ServerCompanyUpdateMsg msg ->
        match state.Companies |> List.tryFind (fun cmp -> cmp.Id = msg.CompanyId) with
        | Some company ->
            let company =
                { company with Name = msg.CompanyName
                               ManagerName = msg.CompanyName
                               Color = msg.Color
                               HasPassword = msg.HasPassword }
            let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> company.Id)
            { state with Companies = companies @ [ company ] }
        | None -> state
        
    | ServerCompanyRemoveMsg msg ->
        let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> msg.CompanyId)
        { state with Companies = companies }

    | _ -> failwith "Invalid update message for state"

let init (host : IPAddress, port : int) (mailbox : Actor<Message>) =

    let state       = State.Default
    let stream      = connectToStream host port
    let senderRef   = Sender.init   stream |> spawn mailbox "sender"
    let receiverRef = Receiver.init stream |> spawn mailbox "receiver"
    
    let rec errored sender receiver state =
        actor {
            return! errored sender receiver state
        }
        
    and connected sender receiver state =
        actor {
            match! mailbox.Receive () with
            | PacketReceivedMsg msg ->
                let state = dispatch state msg
                return! connected sender receiver state
            | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
        }
        
    and connecting sender receiver state =
        actor {
            match! mailbox.Receive () with
            | PacketReceivedMsg msg ->
                let state = dispatch state msg
                match msg with
                | ServerProtocolMsg _ ->
                    defaultPolls @ defaultUpdateFrequencies |> List.iter (fun msg -> sender <! msg)
                    return! connecting sender receiver state
                | ServerWelcomeMsg _ ->
                    return! connected sender receiver state
                | _ -> failwithf $"INVALID CONNECTING STATE CAPTURED FOR PACKET: %A{msg}"
            | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
        }
    
    and idle sender receiver state =
        actor {
            match! mailbox.Receive () with
            | AuthorizeMsg { Pass = pass; Name = name; Version = ver } ->
                sender <! AdminJoinMsg { Password = pass; AdminName = name; AdminVersion = ver }
                schedule mailbox receiver 1.0 "start receiving"
                return! connecting sender receiver state
            | _ -> failwith "INVALID IDLE STATE CAPTURED"
        }
        
    idle senderRef receiverRef state