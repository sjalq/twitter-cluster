open LinqToTwitter
open LinqToTwitter.OAuth

open System.IO
open FSharp.Json
open FSharp.Core
open System.Text
open FSharp.Data


open Credentials
open FollowerWonk
open Twitter
open TargetUsers
open System.Linq
open ExtraMap

open System
open JsonFileProcessing


let writeResults results = 
    let filename = String.Format("data/query {0}.json", nowString ())
    results |> serializeJsonFile filename


let setupDirectories =
    Directory.CreateDirectory("data") |> ignore


[<EntryPoint>]
let main argv =
    (* *)
    let mutable auth = new SingleUserAuthorizer()
    auth.CredentialStore <- twitterCredentialStore
    let twitterCtx = new TwitterContext(auth)

    globalUserCache <- deserializeJsonFile "data/globalUserCache.json"
    globalSocialAuthCache <- deserializeJsonFile "data/globalSocialAuthCache.json" 

    let twitterResults = 
        TargetUsers.targetUsers
        |> getMultipleFollowingsCached twitterCtx
        |> Map.toArray
        |> Array.sortByDescending (fun (_, a) -> a.FollowersFromQuery)
        // |> Array.truncate 250 
        |> Map.ofArray 

    printfn "%A" (twitterResults.Keys.ToArray())
    printfn "Twitter hits : %A" (twitterResults.Keys.Count)

    let followerWonkResults =
        twitterResults.Keys.ToArray()
        |> getSocialAuth

    let joinedResults =
        twitterResults
        |> innerJoin followerWonkResults
        |> Map.map (fun _ (fw,u) -> { u with SocialAuthority = fw.SocialAuthority })
        |> rankMultipleUnified 
            [|(fun u -> u.SocialAuthority), Descending
            ; (fun u -> decimal u.FollowersFromQuery), Descending
            ; (fun u -> decimal u.FollowerCount), Ascending
            ; (fun u -> decimal u.FollowingCount), Ascending
            |]
            //(fun ranks -> ranks |> Array.fold (+) 1 )
            (fun ranks -> 
                ranks 
                |> Array.map (fun r -> r * r) 
                |> Array.sum 
                |> Math.Sqrt
                )
        //|> Map.toArray

    serializeJsonFileTimestamped 
        "data/results" 
        joinedResults
    
    joinedResults
    |> Array.map (fun (k, (u, r)) -> u.Username, u.SocialAuthority, u.FollowersFromQuery, u.FollowerCount, u.FollowingCount ,r)
    |> printfn "%A" 
    
    1