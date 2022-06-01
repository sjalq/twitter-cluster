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

let writeResults results = 
    let filename = String.Format("data/query {0}.json", DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"))
    File.WriteAllText(filename, results |> Json.serialize)

[<EntryPoint>]
let main argv =
    (* *)
    let mutable auth = new SingleUserAuthorizer()
    auth.CredentialStore <- twitterCredentialStore
    let twitterCtx = new TwitterContext(auth)

    globalUserCache <- File.ReadAllText("data/globalUserCache.json") |> Json.deserialize
    globalSocialAuthCache <- File.ReadAllText("data/globalSocialAuthCache.json") |> Json.deserialize

    let twitterResults = 
        TargetUsers.targetUsers
        |> getMultipleFollowingsCached twitterCtx
        |> Map.toArray
        |> Array.sortBy (fun (_, a) -> a.FollowersFromQuery)
        |> Array.truncate 250 
        |> Map.ofArray 

    let followerWonkResults =
        twitterResults.Keys.ToArray()
        |> getSocialAuth

    let joinedResults =
        twitterResults
        |> innerJoin followerWonkResults
        |> Map.map (fun _ (fw,u) -> { u with SocialAuthority = fw.SocialAuthority })
        |> Map.toSeq
        |> Seq.map 
            (fun (k,v) -> 
                {| Username = v.Username
                ; DisplayName = v.DisplayName
                ; FollowersFromQuery = v.FollowersFromQuery
                ; FollowerCount = v.FollowerCount
                ; FollowingCount = v.FollowingCount
                ; Influence = v.SocialAuthority * (decimal v.FollowersFromQuery) / (decimal v.FollowerCount)
                |})
        |> Seq.sortByDescending (fun u -> u.Influence)

    writeResults joinedResults

    joinedResults
    |> Seq.map (fun u -> u.Username, u.FollowersFromQuery, u.Influence)
    |> printfn "%A" 
    
    1