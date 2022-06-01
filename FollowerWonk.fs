module FollowerWonk

open System
open FSharp.Data
open FSharp.Json

open Credentials
open Delay
open System.Security.Cryptography
open System.Text
open System.IO

open ExtraMap

open Types

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
 
let mutable globalSocialAuthCache = Map.empty

let findUsersNotInCache usernames =
    let result = globalSocialAuthCache |> findKeysNotInMap usernames
    result 
    |> Set.ofSeq
    |> Set.difference (usernames |> Set.ofSeq)
    |> String.concat ","
    |> printfn "Cache hits for '%A' " 
    result

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
                            u.ScreenName.ToLowerInvariant(), 
                            {
                                Id = u.UserId.ToString()
                                Username = u.ScreenName.ToLowerInvariant()
                                SocialAuthority = u.SocialAuthority
                            })

                globalSocialAuthCache <- globalSocialAuthCache |> addMultiple results
                File.WriteAllText("data/globalSocialAuthCache.json",globalSocialAuthCache |> Json.serialize)

                globalSocialAuthCache 
                |> Map.filter (fun key _ -> usernames |> Array.contains key)

            | _ -> Map.empty

    usernames 
    |> Seq.take 25
    |> Seq.chunkBySize 25 
    |> Seq.map getSocialAuth 
    |> Seq.map Map.toSeq
    |> Seq.concat
    |> Map.ofSeq