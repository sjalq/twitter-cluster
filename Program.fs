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

[<EntryPoint>]
let main argv =
    let mutable auth = new SingleUserAuthorizer()
    auth.CredentialStore <- twitterCredentialStore
    let twitterCtx = new TwitterContext(auth)

    globalUserCache <- File.ReadAllText("data/globalUserCache.json") |> Json.deserialize
    globalSocialAuthCache <- File.ReadAllText("data/globalSocialAuthCache.json") |> Json.deserialize

    let checks = getSocialAuth targetUsers

    let twitterResults = 
        TargetUsers.targetUsers
        |> getMultipleFollowingsCached twitterCtx
        |> Map.toArray
        |> Array.sortBy (fun (_, a) -> a.FollowersWithinQuerySet)
    let twitterResults = 
        twitterResults 
        |> Array.take (max 250 twitterResults.Length)
        |> Map.ofArray 

    let socialAuthResults = 
        twitterResults.Keys.ToArray()
        |> getSocialAuth
    
    let joinedResults = 
        innerJoin 
            twitterResults 
            socialAuthResults
        
    joinedResults 
    |> Map.toArray
    |> Array.sortByDescending (fun (userId,(a,b)) -> (decimal a.FollowersWithinQuerySet) * b.SocialAuthority / (decimal a.User.FollowerCount))
    |> printfn "%A"

    // joinedResults
    //     |> Map.toArray 
    //     |> Array.map (
    //         fun (name, (a,s)) -> 
    //             match a with
    //             | Some q ->
    //                 q.User.Username, 
    //                 q.User.DisplayName, 
    //                 q.FollowersWithinQuerySet, 
    //                 q.User.FollowerCount,
    //                 (float q.User.FollowerCount)**0.5 / (float q.FollowersWithinQuerySet),
    //                 (float q.User.FollowingCount)**0.5 / (float q.FollowersWithinQuerySet),
    //             | _ -> (name, "0", 0, 0, 0, 0)
    //         )
    //     |> Array.sortBy (fun (_,_,_,_,relativeInfluence,_) -> relativeInfluence)
    //     |> printfn "%A"

    File.WriteAllText("data/detailsChris.json", joinedResults |> Json.serialize)

    1