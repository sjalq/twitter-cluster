module ExtraMap

open System

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
    //table |> Map.toSeq |> Seq.append kvp |> Map.ofSeq

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

let euclidianDistance v =
    v
    |> Array.map (fun x -> (float x) * (float x))
    |> Array.fold (+) 0.0
    |> Math.Sqrt

let manhattanDistance v  =
    v |> Array.fold (+) 0 |> float

let partitionedFind keys map =
    keys 
    |> Array.partition (fun key -> map |> Map.containsKey key)
    |> (fun (found, unfound) -> 
        (found 
            |> Array.map (fun key -> (key, map |> Map.find key) ) 
            |> Map.ofArray,
        unfound)
    )

let isOk result =
    match result with
    | Ok _ -> true
    | _ -> false

let getValue result = 
    match result with 
    | Ok v -> v

let getError result =
    match result with
    | Error e -> e
    