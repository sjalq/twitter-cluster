module Types

open System

type Username = Username of String
type UserId = UserId of String with
    static member op_Implicit(str:String) = UserId str
    //static member op_Implicit(id:UserId) = id.ToString() |> UserId
    static member op_Explicit(src:UserId) = match src with UserId str -> str
    override this.ToString() = match this with UserId str -> str 

let createUsername (username:string) = username.ToLowerInvariant() |> Username

let stringToUserId userId = userId |> UserId 

let int64ToUserId (userId:int64) = userId.ToString() |> UserId

let userIdToString userId = match userId with UserId s -> s

type TwitterUser =
    {
        Id : UserId
        Username : Username
        DisplayName : string
        Bio : string
        FollowerCount : int
        FollowingCount : int
    }

type FollowerWonkUser = 
    {
        Id : UserId
        Username : Username
        SocialAuthority : decimal
    }

type UserResult =
    {
        Id : UserId
        Username : Username
        DisplayName : string
        Bio : string
        FollowerCount : int
        FollowingCount : int
        FollowersFromQuery : int
        SocialAuthority : decimal
    }