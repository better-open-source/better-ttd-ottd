module BetterTTD.Actors.Receiver

open System
open System.IO
open System.Timers
open Akka.FSharp
open BetterTTD.Network.Packet
open BetterTTD.PacketTransformer
open FSharpx.Collections

let private read (stream : Stream) (size : int) =
    let buf = Array.zeroCreate<byte> size
    
    let rec tRead (tStream : Stream) (tSize : int) =
        if tSize < size then
            let res = tStream.Read (buf, tSize, size - tSize)
            tRead tStream (tSize + res)
        else tSize
        
    tRead stream 0 |> ignore
    buf

let private createPacket (sizeBuf : byte array) (content : byte array) =
    let buf = Array.zeroCreate<byte> (2 + content.Length)
    buf.[0] <- sizeBuf.[0]
    buf.[1] <- sizeBuf.[1]
    for i in 0 .. (content.Length - 1) do
        buf.[i + 2] <- content.[i]
    { createPacket with Buffer = buf }

let private waitForPacket (stream : Stream) =
    let sizeBuf = read stream 2
    let size = BitConverter.ToUInt16 (sizeBuf, 0)
    let content = read stream (int size - 2)
    createPacket sizeBuf content

let private initTimedEvent (interval : float) handler =
    let timer = new Timer(interval)
    timer.AutoReset <- true
    timer.Elapsed.Add handler
    timer.Start()

let private handlePacket (stream : Stream) (mailbox : Actor<_>) =
    let pac = waitForPacket stream
    let msg = packetToMsg pac
    mailbox.Context.Parent <! Messages.PacketReceivedMsg msg

let init (stream : Stream) (mailbox : Actor<_>) =
    fun _ ->
        let pac = waitForPacket stream
        let msg = packetToMsg pac
        printfn $"%A{msg}"
        mailbox.Context.Parent <! Messages.PacketReceivedMsg msg
    |> initTimedEvent (TimeSpan.FromSeconds(1.0).TotalMilliseconds)
    
    let rec loop () =
        actor {
            match! mailbox.Receive () with
            | _ -> return! loop () 
        }
        
    loop ()
