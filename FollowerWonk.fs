module FollowerWonk

open System
open FSharp.Data

open Credentials
open Delay
open System.Security.Cryptography
open System.Text
open System.IO

open ExtraMap

open Types
open JsonFileProcessing

[<Literal>]
let followerWonkSample = "./followerWonkSample.json"
type FollowerWonkData = JsonProvider<followerWonkSample>


let hash_hmac (key:String) (message:String) = 
    let hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key))
    let bytesSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(message))
    let hexSignature = bytesSignature |> Array.fold (fun state x-> state + sprintf "%02X" x) ""
    let base64Signature = Convert.ToBase64String(bytesSignature)
    base64Signature


let authString accessId secretKey unixTimeValidUntil =
    let message = String.Format("{0}\n{1}", accessId, unixTimeValidUntil)
    let base64Signature = hash_hmac secretKey message
    String.Format("AccessID={0};Timestamp={1};Signature={2}", accessId, unixTimeValidUntil, base64Signature) 


let urlString usernames =
    let time = DateTime.UtcNow
    let unixTime = DateTimeOffset(time).ToUnixTimeSeconds() + 500L 
    let auth = 
        authString 
            followerWonkCredentialStore.accessID 
            followerWonkCredentialStore.secretKey 
            unixTime
    let uri = "https://api.followerwonk.com/social-authority"
    let usernamesString = usernames |> Seq.map (fun x -> string x) |> String.concat ","
    printfn "Fetching : %A" usernamesString 
    let url = String.Format("{0}?screen_name={1};{2}", uri, usernamesString, auth)
    log url
    url 


let mutable globalSocialAuthCache = Map.empty


let findUsersNotInCache usernames =
    globalSocialAuthCache |> partitionedFind usernames


let getSocialAuth usernames =
    let mutable lastTimeCalled = None 
    let getSocialAuth usernamesNotInCache =
        let delayPeriod = 200;
        lastTimeCalled <- delay delayPeriod lastTimeCalled

        let httpResult = 
            try
                let result = (Http.RequestString(urlString usernames))
                "Success : " + result |> log
                Ok result
            with
            | ex ->
                printfn "%A" ex.Message 
                "Failure : " + ex.Message |> log
                Error ex.Message

        match httpResult with
            | Ok result ->
                use stringReader = new StringReader(result) 
                let root = FollowerWonkData.Load(stringReader)

                let results = 
                    root.Embedded 
                    |> Array.map (
                        fun u -> 
                            createUsername u.ScreenName, 
                            {
                                Id = int64ToUserId u.UserId
                                Username = createUsername u.ScreenName
                                SocialAuthority = u.SocialAuthority
                            })     

                results

            | ex -> 
                Array.empty


    let _, unfound = 
        usernames |> findUsersNotInCache

    let fetchedResults =
        unfound
        // |> Seq.take 25 // safety line for hardening the interaction with the API
        |> Seq.chunkBySize 25 
        |> Seq.map getSocialAuth 
        |> Seq.concat

    globalSocialAuthCache <- globalSocialAuthCache |> addMultiple fetchedResults
    globalSocialAuthCache |> serializeJsonFile "data/globalSocialAuthCache.json" 
    globalSocialAuthCache |> Map.filter (fun k v -> usernames |> Seq.contains k)