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

type Direction =
    | Ascending
    | Descending

let rank rankFn direction table =
    let directionFn = 
        match direction with
        | Ascending -> Array.sortBy
        | Descending -> Array.sortByDescending
    
    table 
    |> Map.toArray
    //|> Array.sortBy (fun (_,v) -> rankFn v)
    |> directionFn (fun (_,v) -> rankFn v)
    |> Array.mapi (fun i (key, _) -> (key, i))
    |> Map.ofArray

let rankMultiple rankFns table =
    rankFns
    |> Array.map (fun (rankFn, dir) ->  table |> rank rankFn dir |> Map.toArray)
    |> Array.transpose 
    |> Array.concat
    |> Array.groupBy (fun (k, _) -> k)
    |> Array.map (fun (k, v) -> k, v |> Array.map snd)
    |> Map.ofArray
    |> innerJoin table

let rankMultipleUnified rankFns rankCombinerFn table =
    rankMultiple rankFns table
    |> Map.map (fun k (v,r) -> v, rankCombinerFn r)
    |> Map.toArray
    |> Array.sortBy (fun (_,(_,r)) -> r)