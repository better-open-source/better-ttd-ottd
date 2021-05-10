module BetterTTD.Actors.Coordinator

open System
open System.Net
open System.Net.Sockets
open Akka.FSharp
open BetterTTD.Actors.Messages
open BetterTTD.Network.MessageTransformer
open BetterTTD.Network.Enums
open BetterTTD.Network.PacketTransformer

let private schedule (mailbox : Actor<_>) ref interval msg =
    mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(
        TimeSpan.FromMilliseconds 0.,
        TimeSpan.FromMilliseconds interval,
        ref, msg)
    
let private connectToStream (ipAddress : IPAddress) (port : int) =
    let tcpClient = new TcpClient ()
    tcpClient.Connect (ipAddress, port)
    tcpClient.GetStream ()

let private defaultPolls =
    [ { UpdateType = AdminUpdateType.ADMIN_UPDATE_COMPANY_INFO
        Data       = uint32 0xFFFFFFFF }
      { UpdateType = AdminUpdateType.ADMIN_UPDATE_CLIENT_INFO
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

let init (host : IPAddress, port : int) (mailbox : Actor<Message>) =

    let state       = State.init
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
                let state = State.dispatch state msg
                match msg with
                | ServerChatMsg _ ->
                    match state.ChatHistory |> List.tryLast with
                    | Some chatAction -> printfn $"%A{chatAction}"
                    | None -> ()
                | _ -> ()
                return! connected sender receiver state
            | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
        }
        
    and connecting sender receiver state =
        actor {
            match! mailbox.Receive () with
            | PacketReceivedMsg msg ->
                let state = State.dispatch state msg
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