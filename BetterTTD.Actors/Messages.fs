module BetterTTD.Actors.Messages

open BetterTTD.PacketTransformer

type Authorize =
    { Name    : string
      Pass    : string
      Version : string }

type Message =
    | PacketReceivedMsg of PacketMessage
    | AuthorizeMsg of Authorize