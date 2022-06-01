module ExtraMap

let fullOuterJoin left right =
    let leftKeys = left |> Map.keys
    let rightKeys = right |> Map.keys
    let allKeys = leftKeys |> Seq.append rightKeys |> Seq.distinct
    allKeys
    |> Seq.map 
        (fun key -> 
            let leftValue = left |> Map.tryFind key
            let rightValue = right |> Map.tryFind key
            (key, (leftValue, rightValue))
        )
    |> Map.ofSeq

let getLeftOuter joinedMap =
    Map.filter (fun _ (_, r) -> r |> Option.isNone |> not)
    >> Map.map (fun _ (l, r) -> (l, Option.get r))
    <| joinedMap

let getRightOuter joinedMap =
    Map.filter (fun _ (l, _) -> l |> Option.isNone |> not)
    >> Map.map (fun _ (l, r) -> (Option.get l, r))
    <| joinedMap

let getInner joinedMap= 
    (getLeftOuter >> getRightOuter)
    <| joinedMap

let leftOuterJoin left right =
    fullOuterJoin left right |> getLeftOuter

let rightOuterJoin left right =
    fullOuterJoin left right |>  getRightOuter

let innerJoin left right =
    fullOuterJoin left right |> getInner

let findKeysNotInMap keys table =
    keys |> Seq.filter (fun key -> table |> Map.containsKey key |> not)

let addMultiple kvp table =
    kvp 
    |> Seq.fold 
        (fun state (key, value) -> state |> Map.add key value) 
        table