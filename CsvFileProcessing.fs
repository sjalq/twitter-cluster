module CsvFileProcessing

open FSharp.Data
open System
open System.IO

open Types


type InputCsv = CsvProvider<"inputSample.csv", HasHeaders=true>

let getUsernamesFromCsv (filename:string) =
    try
        let csvText = File.ReadAllText(filename)
        let csv = InputCsv.Parse(csvText).Rows |> Seq.take 100
        csv 
        |> Seq.map (fun r -> r.``Screen name`` |> createUsername)
        |> Seq.toArray
        |> Ok
    with
        | ex -> 
            printfn "Error : %A" ex.Message
            Error ex.Message

type OutputCsv = 
    CsvProvider<
        ("User ID, Username, Display Name, Follower Count, Following Count, Followers From Query, Social Authority, Combined Rank")
        , Schema = "User ID (string), Username (string), Display Name (string), Follower Count (int), Following Count (int), Followers From Query (int), Social Authority (decimal), Combined Rank (float)"
        , HasHeaders = true>

let writeResultToCsv results filename =
    let buildRowFromObject obj = OutputCsv.Row(
            obj.Id
            , obj.Username
            , obj.DisplayName
            , obj.FollowerCount
            , obj.FollowingCount
            , obj.FollowersFromQuery
            , obj.SocialAuthority
            , obj.CombinedRank)

    let buildTableFromObjects = (Seq.map buildRowFromObject) >> Seq.toList >> OutputCsv

    let csv = results |> buildTableFromObjects
    let csvText = csv.SaveToString(',', '"')
    File.WriteAllText(filename, csvText)


let writeResultToTimestampedCsv filename results=
    let filename = String.Format("{0} {1}.csv", filename, nowString())
    writeResultToCsv results filename