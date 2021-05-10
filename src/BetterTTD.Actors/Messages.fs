module BetterTTD.Actors.Messages

open BetterTTD.Network.PacketTransformer

type Authorize =
    { Name    : string
      Pass    : string
      Version : string }

type Message =
    | PacketReceivedMsg of PacketMessage
    | AuthorizeMsg of Authorize