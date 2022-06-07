module JsonFileProcessing

open System
open System.IO
open FSharp.Json

open Types

let deserializeJsonFile filename = 
    let jsonString = 
        try 
            File.ReadAllText(filename)
        with
            | ex -> 
                match ex with
                | :? FileNotFoundException -> 
                    let result = "{}"
                    File.WriteAllText(filename, result)
                    result
                | _ -> raise ex

    jsonString |> Json.deserialize


let serializeJsonFile filename data =
    File.WriteAllText(filename, data |> Json.serialize)


let serializeJsonFileTimestamped filename data =
    serializeJsonFile 
        (String.Format("{0} {1}.json", filename, nowString() ))
        data


let log data = 
    File.AppendAllLines("data/log.txt", [nowString(); data])