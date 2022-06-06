module Twitter

open System

open LinqToTwitter.Common

open System.IO
open FSharp.Data
open FSharp.Core

open Delay
open Types
open JsonFileProcessing
open System.Net
open System.Net.Http

[<Literal>]
let twitterFollowingSample = "./twitterFollowingSample.json"
type TwitterFollowingData = JsonProvider<twitterFollowingSample>

[<Literal>]
let twitterAccountSample = "./twitterAccountSample.json"
type TwitterAccountData = JsonProvider<twitterAccountSample>


// let getAccounts (twitterCtx:TwitterContext) usernames = 
//     let getAccounts usernames =
//         let usernamesString = usernames |> String.concat ","
//         let userQuery = 
//             twitterCtx.TwitterUser
//                 .Where(fun user -> 
//                     user.Type = UserType.UsernameLookup
//                     && user.Usernames = usernamesString 
//                     && user.UserFields = UserField.AllFields)
//         userQuery.SingleOrDefault().Users.ToArray()

//     usernames 
//     |> Array.chunkBySize 20
//     |> Array.Parallel.map getAccounts
//     |> Array.fold (fun acc i -> acc |> Array.append i) [||]

let rawQueryAccounts bearerToken usernames =
    let api = "https://api.twitter.com/"
    let userFieldsQuery = "&user.fields=" + UserField.AllFields
    let userNamesQuery = "usernames=" + (usernames |> String.concat ",")
    let queryString = String.Format("{0}2/users/by?{1}", api, userNamesQuery, userFieldsQuery)

    printfn "%A" queryString

    try 
        let httpRequest = WebRequest.Create(queryString) :?> HttpWebRequest
        httpRequest.Accept <- "application/json"
        httpRequest.Headers.["Authorization"] <- "Bearer " + bearerToken
        let httpResponse = httpRequest.GetResponse()

        use streamReader = new StreamReader(httpResponse.GetResponseStream())
        streamReader.ReadToEnd() |> Ok
    with
        | ex -> 
            printfn "Exception: %A" ex.Message
            Error ex.Message


let decodeAccounts jsonString = 
    try 
        use stringReader = new StringReader(jsonString) 
        let root = TwitterAccountData.Load(stringReader)
        root.Data
        |> Array.map (fun d -> 
            {|
                Id = int64ToUserId d.Id
                Username = createUsername d.Username
                DisplayName = d.Name
            |})
    with
    | ex -> 
        printfn "Exception: %A" ex.Message
        [||]


let getAccounts bearerToken usernames = 
    usernames 
    |> Array.chunkBySize 20
    |> Array.map (rawQueryAccounts bearerToken)
    |> Array.filter (fun x -> match x with | Ok _ -> true | _ -> false )
    |> Array.map (fun r -> 
        match r with 
        | Ok response -> decodeAccounts response 
        | _ -> [||])
    |> Array.fold (fun acc i -> acc |> Array.append i) [||]


let mutable lastTimeCalled = None
let rawQueryAccountFollowingRateLimited bearerToken paginationToken userId = 
    lastTimeCalled <- delay 60000 lastTimeCalled
    
    let paginationQuery = 
        match paginationToken with
        | Some pt -> "&pagination_token=" + pt
        | None -> ""
    let api = "https://api.twitter.com/"
    let userFieldsQuery = "&user.fields=" + UserField.AllFields
    let userIdQuery = userId
    let queryString = String.Format("{0}2/users/{1}/following?max_results=1000{2}{3}", api, userIdQuery, paginationQuery, userFieldsQuery)

    printfn "%A" queryString

    try 
        let httpRequest = WebRequest.Create(queryString) :?> HttpWebRequest
        httpRequest.Accept <- "application/json"
        httpRequest.Headers.["Authorization"] <- "Bearer " + bearerToken
        let httpResponse = httpRequest.GetResponse()

        use streamReader = new StreamReader(httpResponse.GetResponseStream())
        streamReader.ReadToEnd() |> Ok
    with
        | ex -> 
            printfn "Exception: %A" ex.Message
            Error ex.Message


let decodeUsers jsonString = 
    try 
        use stringReader = new StringReader(jsonString) 
        let root = TwitterFollowingData.Load(stringReader)
        root.Data
        |> Array.map (fun d -> 
            {
                Id = int64ToUserId d.Id
                Username = createUsername d.Username
                DisplayName = 
                    d.Name.String 
                    |> Option.defaultValue ((d.Name.Number |> Option.defaultValue 0).ToString())
                Bio = d.Description |> Option.defaultValue ""
                FollowerCount = d.PublicMetrics.FollowersCount
                FollowingCount = d.PublicMetrics.FollowingCount
            })
    with
    | ex -> 
        printfn "Exception: %A" ex.Message
        [||]


let decodePaginationToken jsonString = 
    try 
        use stringReader = new StringReader(jsonString) 
        let root = TwitterFollowingData.Load(stringReader)
        let nextToken = root.Meta.NextToken
        if nextToken = null then
            None
        else
            Some nextToken
    with
    | ex ->
        printfn "Error decoding pagination token: %A" ex.Message
        None


let rec getAccountFollowing bearerToken paginationToken userId =
    let response  =
        rawQueryAccountFollowingRateLimited 
            bearerToken 
            paginationToken 
            userId

    match response with
    | Ok r ->
        let users = decodeUsers r 
        let nextToken = decodePaginationToken r
        
        match nextToken with 
        | Some t -> 
            users 
            |> Array.append (getAccountFollowing bearerToken (Some t) userId)
        | None -> users
    | Error r ->
        printfn "Error: %A" r
        [||]  


let mutable globalUserCache = Map.empty
let getAccountFollowingCached bearerToken paginationToken userId =  
    let getFollowers userId =
        printfn "Cache miss for : %A" userId 
        let fetchedFollowing = getAccountFollowing bearerToken paginationToken userId
        
        globalUserCache <- Map.add userId fetchedFollowing globalUserCache
        globalUserCache |> serializeJsonFile "data/globalUserCache.json"
        fetchedFollowing 

    match globalUserCache |> (Map.tryFind userId) with
    | None -> 
        getFollowers userId
    | Some following ->
        printfn "Cache hit for : %A" userId 
        following


let getMultipleFollowingsCached bearerToken initialAccountUsernames =
    initialAccountUsernames 
    |> Array.distinct
    |> getAccounts bearerToken 
    |> Array.map (fun a -> getAccountFollowingCached bearerToken None a.Id)
    |> Array.concat
    |> Array.groupBy (fun t -> createUsername t.Username)
    |> Array.map 
        (fun (g,a) -> 
            g, 
            { Id = a.[0].Id
            ; Username = createUsername a.[0].Username
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