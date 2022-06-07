module Types
open System

let createUsername (username:string) = username.ToLowerInvariant()

let int64ToUserId (userId:int64) = userId.ToString().Trim() 

type TwitterUser =
    {
        Id : string
        Username : string
        DisplayName : string
        Bio : string
        FollowerCount : int
        FollowingCount : int
    }

type FollowerWonkUser = 
    {
        Id : string
        Username : string
        SocialAuthority : decimal
    }

type UserResult =
    {
        Id : string
        Username : string
        DisplayName : string
        Bio : string
        FollowerCount : int
        FollowingCount : int
        FollowersFromQuery : int
        SocialAuthority : decimal
        CombinedRank : float
    }

let nowString () = 
    DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")