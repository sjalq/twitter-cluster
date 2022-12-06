open FSharp.Core
open FSharp.Data
open System
open System.IO
open System.Linq

open Credentials
open ExtraMap
open FollowerWonk
open JsonFileProcessing
open Twitter
open Types
open CsvFileProcessing


let setupDirectories =
    Directory.CreateDirectory("data") |> ignore


[<EntryPoint>]
let main argv =
    let inputCsvFile = 
        argv[0]

    let queryCount = 
        try 
            Int32.Parse argv[1]
        with
        | ex -> 20

    let maxFollowingCount = 
        try 
            Int32.Parse argv[2]
        with
        | ex -> 10000

    let thresholdFollowingFromQueryPercentage = 
        try 
            Double.Parse argv[3]
        with
        | ex -> 0.05

    printfn "Querying %A users from Twitter" queryCount

    let usernamesToQuery = 
        match getUsernamesFromCsv inputCsvFile queryCount maxFollowingCount with
        | Ok usernames -> usernames
        | Error msg -> 
            printfn "Error : %A" msg
            [||]

    globalUserCache <- deserializeJsonFile "data/globalUserCache.json"
    globalSocialAuthCache <- deserializeJsonFile "data/globalSocialAuthCache.json" 

    // let elonFollows = 
    //     globalUserCache 
    //     |> Map.find "44196397" 
    //     |> Array.map (fun user -> user.Username)

    // printfn "Elon follows %A accounts"  elonFollows.Length

    let twitterResults = 
        usernamesToQuery
        |> getMultipleFollowingsCached twitterBearerToken
        |> Map.filter (fun k v -> (float v.FollowersFromQuery) / (float usernamesToQuery.Length) >=  thresholdFollowingFromQueryPercentage)

    let followerWonkResults =
        twitterResults
        |> Map.toArray
        |> Array.sortByDescending (fun (_, v) -> v.FollowersFromQuery)
        |> Array.map (fun (k,_) -> k)
        |> getSocialAuthCached

    let joinedResults =
        rightOuterJoin twitterResults followerWonkResults
        |> Map.map (fun _ (u,fw) -> 
            { u with SocialAuthority = 
                        fw 
                            |> Option.map (fun f -> f.SocialAuthority) 
                            |> Option.defaultValue 0M 
            })
        // |> Map.filter (fun _ u -> u.SocialAuthority > 0M)
        |> rankMultipleUnified 
            [|
              (fun u -> u.SocialAuthority), Descending
              //(fun u -> (decimal u.FollowerCount) / ((decimal u.FollowersFromQuery) * (decimal u.FollowersFromQuery))), Ascending
              (fun u -> decimal u.FollowersFromQuery), Descending
              (fun u -> decimal u.FollowerCount), Ascending
              //(fun u -> decimal u.FollowingCount), Ascending
              //(fun u -> ((float u.FollowersFromQuery) ** 2)/(float u.FollowerCount)), Descending
            |] 
            manhattanDistance
            // euclidianDistance
        |> Array.map (fun (_, (ur, rank)) -> { ur with CombinedRank = rank })

    writeResultToTimestampedCsv
        "data/results"
        joinedResults

    serializeJsonFileTimestamped 
        "data/results" 
        joinedResults
    
    joinedResults
    |> Array.length
    |> printfn "%A results" 
    
    1