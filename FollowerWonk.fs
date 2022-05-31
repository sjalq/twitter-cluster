module FollowerWonk

open System
open FSharp.Data
open FSharp.Json

open Credentials
open Delay
open System.Security.Cryptography
open System.Text
open System.IO

[<Literal>]
let followerWonkSample = "./followerWonkSample.json"
type FollowerWonkData = JsonProvider<followerWonkSample>

type FollowerWonkItem = 
    {
        Id : string
        Username : string
        SocialAuthority : decimal
    }

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
 
let mutable globalSocialAuthCache = Map.empty

let findKeysNotInMap keys table =
    keys |> Array.filter (fun key -> table |> Map.containsKey key |> not)

let addMultiple kvp table =
    kvp 
    |> Array.fold 
        (fun state (key, value) -> state |> Map.add key value) 
        table

let findUsersNotInCache usernames =
    globalSocialAuthCache |> findKeysNotInMap usernames

let getSocialAuth usernames =
    let mutable lastTimeCalled = None 
    let getSocialAuth usernames =
        let delayPeriod = 200;
        lastTimeCalled <- delay delayPeriod lastTimeCalled

        let time = DateTime.UtcNow
        let unixTime = DateTimeOffset(time).ToUnixTimeSeconds() + 500L 
        let auth = 
            authString 
                followerWonkCredentialStore.accessID 
                followerWonkCredentialStore.secretKey 
                unixTime
        let uri = "https://api.followerwonk.com/social-authority"
        let usernamesString = 
            usernames 
            |> findUsersNotInCache 
            |> String.concat ","
        let url = String.Format("{0}?screen_name={1};{2}", uri, usernamesString, auth)
        
        let httpResult = 
            try
                Ok (Http.RequestString(url))
            with
            | ex ->
                printfn "%A" ex.Message 
                Error ex.Message

        match httpResult with
            | Ok result ->
                use stringReader = new StringReader(result) 
                let root = FollowerWonkData.Load(stringReader)
                let results = 
                    root.Embedded 
                    |> Array.map (
                        fun u -> 
                            u.ScreenName, 
                            {
                                Id = u.UserId.ToString() 
                                Username = u.ScreenName 
                                SocialAuthority = u.SocialAuthority
                            })

                globalSocialAuthCache <- globalSocialAuthCache |> addMultiple results
                File.WriteAllText("data/globalUserCache.json",globalSocialAuthCache |> Json.serialize)

                globalSocialAuthCache 
                |> Map.filter (fun key _ -> usernames |> Array.contains key)

            | _ -> Map.empty

    usernames 
    |> Array.take 2
    |> Array.chunkBySize 25 
    |> Array.map getSocialAuth 
    |> Array.map Map.toArray
    |> Array.concat
    |> Map.ofArray


