module Twitter

open System
open LinqToTwitter
open System.Linq

open LinqToTwitter.OAuth
open LinqToTwitter.Common

open System.IO
open FSharp.Json
open FSharp.Core
open System.Text

open Delay

type User =
    {
        Id : string
        Username : string
        DisplayName : string
        Bio : string
        FollowerCount : int
        FollowingCount : int
    }

type RelevantFollowingUser =
    {
        User : User
        FollowersWithinQuerySet : int
    }

let getAccounts (twitterCtx:TwitterContext) usernames = 
    let getAccounts usernames =
        let usernamesString = usernames |> String.concat ","
        let userQuery = 
            twitterCtx.TwitterUser
                .Where(fun user -> 
                    user.Type = UserType.UsernameLookup
                    && user.Usernames = usernamesString 
                    && user.UserFields = UserField.AllFields)
        userQuery.SingleOrDefault().Users.ToArray()

    usernames 
    |> Array.chunkBySize 20
    |> Array.Parallel.map getAccounts
    |> Array.fold (fun acc i -> acc |> Array.append i) [||]


//type TwitterResult = Result<TwitterContext, Exception>

let mutable lastTimeCalled = None
let queryFollowingCallRateLimited (twitterCtx:TwitterContext) paginationToken userId =
    lastTimeCalled <- delay 60000 lastTimeCalled
    let qry = 
        twitterCtx.TwitterUser
            .Where(fun user -> 
                user.Type = UserType.Following
                && user.UserFields = UserField.AllFields
                && user.ID = userId
                && user.MaxResults = 1000)

    try 
        match paginationToken with
        | None -> qry.SingleOrDefault()
        | Some token -> qry.Where(fun user -> user.PaginationToken = token).SingleOrDefault()
        |> Ok
    with 
        | ex -> 
            printfn "Exception: %A" ex.Message
            Error ex.Message

// let rawQuery (twitterCtx:TwitterContext) paginationToken userId = 
//     let 

let rec getAccountFollowing (twitterCtx:TwitterContext) paginationToken userId = 
    let result = 
        queryFollowingCallRateLimited twitterCtx paginationToken userId

    let users = 
        match result with
        | Ok qry -> 
            let users = 
                try 
                    let usersResult = 
                        qry.Users.ToArray()
                        |> Array.map (fun u -> 
                        {
                            Id = u.ID
                            Username = u.Username
                            DisplayName = u.Name
                            Bio = u.Description
                            FollowerCount = u.PublicMetrics.FollowersCount
                            FollowingCount = u.PublicMetrics.FollowingCount
                        })

                    printfn "%A" usersResult.Length
                    usersResult |> Ok 
                with
                | ex -> Error ex.Message
            if qry.PaginationToken = null then
                users
            else
                let newUsers = getAccountFollowing twitterCtx (Some qry.PaginationToken) userId
                match newUsers, users with
                | Ok newUsers, Ok users -> 
                    newUsers |> Array.append users |> Ok
                | Ok _, Error err -> 
                    Error err
                | Error err, Ok _ -> 
                    Error err
                | Error err1, Error err2 -> 
                    Error (err1 + ", " + err2)
        | Error msg ->
            printfn "Error: %A" msg
            Error msg

    users


let mutable globalUserCache = Map.empty
let getAccountFollowingCached (twitterCtx:TwitterContext) paginationToken userId =  
    let getFollowers userId =
        printfn "Cache miss for : %A" userId 
        let fetchedFollowing = getAccountFollowing twitterCtx paginationToken userId
        
        match fetchedFollowing with
        | Ok following -> 
            globalUserCache <- Map.add userId following globalUserCache
            File.WriteAllText("data/globalUserCache.json",globalUserCache |> Json.serialize)
            Some following 
        | Error msg -> None

    match globalUserCache |> (Map.tryFind userId) with
    | None -> 
        getFollowers userId
    | Some following ->
        printfn "Cache hit for : %A" userId 
        Some following


let getMultipleFollowingsCached (twitterCtx:TwitterContext) initialAccountUsernames =
    initialAccountUsernames 
    |> Array.distinct
    |> getAccounts twitterCtx 
    |> Array.map (fun a -> getAccountFollowingCached twitterCtx None a.ID)
    |> Array.fold (fun acc a ->
        match a with
        | Some following -> acc |> Array.append following
        | None -> acc
        ) [||] 
    |> Array.groupBy (fun i -> i.Id)
    |> Array.map (fun (g,a) -> g, { User = a.[0]; FollowersWithinQuerySet = a.Length })
    // |> Array.sortByDescending (fun (_,v) -> v.FollowersWithinQuerySet)
    |> Map.ofArray