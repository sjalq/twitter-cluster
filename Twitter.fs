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
open Types
open JsonFileProcessing

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


let mutable lastTimeCalled = None
let queryFollowingCallRateLimited (twitterCtx:TwitterContext) paginationToken (userId:UserId) =
    lastTimeCalled <- delay 60000 lastTimeCalled
    let qry = 
        twitterCtx.TwitterUser
            .Where(fun user -> 
                user.Type = UserType.Following
                && user.UserFields = UserField.AllFields
                && user.ID = string userId
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
        queryFollowingCallRateLimited 
            twitterCtx 
            paginationToken 
            userId

    let users = 
        match result with
        | Ok qry -> 
            let users = 
                try 
                    let usersResult = 
                        qry.Users.ToArray()
                        |> Array.map (fun u -> 
                        {
                            Id = u.ID.ToString()
                            Username = u.Username
                            DisplayName = u.Name
                            Bio = u.Description
                            FollowerCount = u.PublicMetrics.FollowersCount
                            FollowingCount = u.PublicMetrics.FollowingCount
                        })

                    printfn "%A" usersResult.Length
                    usersResult |> Ok 
                with
                | ex -> 
                    match ex with
                    | :? NullReferenceException -> Ok [||]
                    | _ -> Error ex.Message
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
            globalUserCache |> serializeJsonFile "data/globalUserCache.json"
            Some following 
        | Error msg -> 
            printfn "Error: %A" msg
            None

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
    |> Array.groupBy (fun u -> u.Id)
    |> Array.map 
        (fun (g,a) -> 
            g.ToLowerInvariant(), 
            { UserId = a.[0].Id
            ; Username = a.[0].Username.ToLowerInvariant()
            ; DisplayName = a.[0].DisplayName
            ; Bio = a.[0].Bio
            ; FollowerCount = a.[0].FollowerCount
            ; FollowingCount = a.[0].FollowingCount
            ; FollowersFromQuery = a.Length
            ; SocialAuthority = 0M
            })

    // next line replaces the userid with the username for ease of use later.
    |> Array.map (fun (_, record) -> record.Username, record) 
    |> Map.ofArray