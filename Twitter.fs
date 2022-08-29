module Twitter

open FSharp.Core
open FSharp.Data
open System
open System.IO
open System.Net
open System.Net.Http

open Delay
open JsonFileProcessing
open Types


[<Literal>]
let AllFields = "created_at,description,entities,id,location,name,pinned_tweet_id,profile_image_url,protected,public_metrics,url,username,verified,withheld";

[<Literal>]
let twitterFollowingSample = "./twitterFollowingSample.json"
type TwitterFollowingData = JsonProvider<twitterFollowingSample>

[<Literal>]
let twitterAccountSample = "./twitterAccountSample.json"
type TwitterAccountData = JsonProvider<twitterAccountSample>

let baseApi = "https://api.twitter.com/"
let queryTwitter bearerToken queryString =
    printfn "%s" queryString
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


let rawQueryAccounts bearerToken usernames =
    let userFieldsQuery = "&user.fields=" + AllFields
    let userNamesQuery = "usernames=" + (usernames |> String.concat ",")
    let queryString = String.Format("{0}2/users/by?{1}", baseApi, userNamesQuery, userFieldsQuery)

    queryTwitter bearerToken queryString


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
    let userFieldsQuery = "&user.fields=" + AllFields
    let userIdQuery = userId
    let queryString = String.Format("{0}2/users/{1}/following?max_results=1000{2}{3}", baseApi, userIdQuery, paginationQuery, userFieldsQuery)

    queryTwitter bearerToken queryString


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
        if nextToken = null || nextToken = "" then
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
            ; CombinedRank = 0
            })

    // next line replaces the userid with the username as key for ease of use later.
    |> Array.map (fun (_, record) -> record.Username, record) 
    |> Map.ofArray

// let printAll set fn = 
    