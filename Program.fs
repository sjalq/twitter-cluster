open FSharp.Core
open System
open System.IO
open System.Linq

open Credentials
open ExtraMap
open FollowerWonk
open JsonFileProcessing
open Twitter
open Types


let writeResults results = 
    let filename = String.Format("data/query {0}.json", nowString ())
    results |> serializeJsonFile filename


let setupDirectories =
    Directory.CreateDirectory("data") |> ignore

[<EntryPoint>]
let main argv =
    globalUserCache <- deserializeJsonFile "data/globalUserCache.json"
    globalSocialAuthCache <- deserializeJsonFile "data/globalSocialAuthCache.json" 

    let twitterResults = 
        TargetUsers.ecommerceUsers
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

    serializeJsonFileTimestamped 
        "data/results" 
        joinedResults
    
    joinedResults
    |> Array.map (fun (k, (u, r)) -> u.Username, u.SocialAuthority, u.FollowersFromQuery, u.FollowerCount, u.FollowingCount ,r)
    |> printfn "%A" 
    
    1