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
    let inputCsvFile = argv[0]
    let usernamesToQuery = 
        match getUsernamesFromCsv inputCsvFile with
        | Ok usernames -> usernames
        | Error msg -> 
            printfn "Error : %A" msg
            [||]

    globalUserCache <- deserializeJsonFile "data/globalUserCache.json"
    globalSocialAuthCache <- deserializeJsonFile "data/globalSocialAuthCache.json" 

    let twitterResults = 
        usernamesToQuery
        |> getMultipleFollowingsCached twitterBearerToken

    printfn "%A" (twitterResults.Keys.ToArray())
    printfn "Twitter hits : %A" (twitterResults.Keys.Count)

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
        |> Map.filter (fun _ u -> u.SocialAuthority > 0M)
        |> rankMultipleUnified 
            [|(fun u -> u.SocialAuthority), Descending
            //; (fun u -> (decimal u.FollowerCount) / (decimal u.FollowersFromQuery)), Ascending
            ; (fun u -> decimal u.FollowersFromQuery), Descending
            ; (fun u -> decimal u.FollowerCount), Ascending
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
    |> printfn "%A" 
    
    1