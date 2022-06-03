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
        |> Array.truncate 250 
        |> Map.ofArray 

    printfn "%A" (twitterResults.Keys.ToArray())

    let followerWonkResults =
        twitterResults.Keys.ToArray()
        |> getSocialAuth

    let joinedResults =
        twitterResults
        |> innerJoin followerWonkResults
        |> Map.map (fun _ (fw,u) -> { u with SocialAuthority = fw.SocialAuthority })
        |> Map.toArray
        |> Array.map 
            (fun (k,v) -> 
                {| Username = v.Username
                ; DisplayName = v.DisplayName
                ; FollowersFromQuery = v.FollowersFromQuery
                ; FollowerCount = v.FollowerCount
                ; FollowingCount = v.FollowingCount
                ; SocialAuthority = v.SocialAuthority
                ; Influence = v.SocialAuthority * (decimal v.FollowersFromQuery) / (decimal v.FollowerCount)
                |})
        |> Array.sortByDescending (fun u -> u.Influence)

    serializeJsonFileTimestamped 
        "data/results" 
        joinedResults
    
    joinedResults
    |> Array.map (fun u -> u.Username, u.FollowersFromQuery, u.SocialAuthority,  u.Influence)
    |> printfn "%A" 
    
    1