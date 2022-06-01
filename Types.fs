module Types

open System

type Username = String
type UserId = String

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
        UserId : UserId
        Username : Username
        DisplayName : string
        Bio : string
        FollowerCount : int
        FollowingCount : int
        FollowersFromQuery : int
        SocialAuthority : decimal
    }