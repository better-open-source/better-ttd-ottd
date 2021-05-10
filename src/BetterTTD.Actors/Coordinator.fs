module BetterTTD.Actors.Coordinator

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
    { Id : byte }

type GameInfo =
    {  ServerName : string
       NetworkRevision : string
       IsDedicated : bool
       MapName : string
       MapSeed : uint32
       Landscape : Landscape
       CurrentDate : uint32
       MapWidth : int
       MapHeight : int }

type State =
    { GameInfo  : GameInfo
      Clients   : Client list
      Companies : Company list }

let init (host : IPAddress, port : int) (mailbox : Actor<Message>) =

    let stream      = connectToStream host port
    let senderRef   = Sender.init   stream |> spawn mailbox "sender"
    let receiverRef = Receiver.init stream |> spawn mailbox "receiver"
    
    let rec errored sender receiver =
        actor {
            return! errored sender receiver
        }
        
    and connected sender receiver =
        actor {
            match! mailbox.Receive () with
            | PacketReceivedMsg pac ->
                match pac with
                | ServerClientInfoMsg msg -> return! connected sender receiver
                | ServerClientInfoMsg msg -> return! connected sender receiver
                | _ -> return! connected sender receiver
                return! connected sender receiver
            | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
        }
        
    and connecting sender receiver =
        actor {
            match! mailbox.Receive () with
            | PacketReceivedMsg pac ->
                match pac with
                | ServerProtocolMsg _ ->
                    defaultPolls @ defaultUpdateFrequencies |> List.iter (fun msg -> sender <! msg)
                    return! connecting sender receiver
                | ServerWelcomeMsg _ ->
                    return! connected sender receiver
                | _ -> failwithf $"INVALID CONNECTING STATE CAPTURED FOR PACKET: %A{pac}"
            | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
        }
    
    and idle sender receiver =
        actor {
            match! mailbox.Receive () with
            | AuthorizeMsg { Pass = pass; Name = name; Version = ver } ->
                sender <! AdminJoinMsg { Password = pass; AdminName = name; AdminVersion = ver }
                return! connecting sender receiver
            | _ -> failwith "INVALID IDLE STATE CAPTURED"
        }
        
    idle senderRef receiverRef