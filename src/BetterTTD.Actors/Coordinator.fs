module BetterTTD.Actors.Coordinator

open System.Net
open System.Net.Sockets
open Akka.FSharp
open BetterTTD.Actors.Messages
open BetterTTD.MessageTransformer
open BetterTTD.Network.Enums
open BetterTTD.PacketTransformer

let defaultUpdateFrequencies =
    [ { UpdateType = AdminUpdateType.ADMIN_UPDATE_CHAT
        Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC }
      { UpdateType = AdminUpdateType.ADMIN_UPDATE_CLIENT_INFO
        Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC } ]
    |> List.map AdminUpdateFreqMsg
    
let private connectToStream (ipAddress : IPAddress) (port : int) =
    let tcpClient = new TcpClient ()
    tcpClient.Connect (ipAddress, port)
    tcpClient.GetStream ()
    
let init (host : IPAddress) (port : int) (mailbox : Actor<Message>) =

    let stream      = connectToStream host port
    let senderRef   = Sender.init   stream |> spawn mailbox "sender"
    let receiverRef = Receiver.init stream |> spawn mailbox "receiver"
    
    let rec connected sender receiver =
        actor {
            return! connected sender receiver
        }
        
    and connecting sender receiver =
        actor {
            match! mailbox.Receive () with
            | PacketReceivedMsg pac ->
                match pac with
                | ServerProtocolMsg _ ->
                    defaultUpdateFrequencies |> List.iter (fun msg -> sender <! msg)
                    return! connecting sender receiver
                | ServerWelcomeMsg welcome -> return! connected sender receiver
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