module Delay

open System

let delay milliseconds (lastTimeCalled : DateTime Option) =
    match lastTimeCalled with
    | Some datetime -> 
        let shouldWaitTill = datetime + TimeSpan.FromMilliseconds(milliseconds)

        if shouldWaitTill > DateTime.Now then 
            let diff = shouldWaitTill - DateTime.Now
            printfn "Waiting %A milliseconds" diff.TotalMilliseconds
            System.Threading.Thread.Sleep(diff)
            Some DateTime.Now 
        else 
            Some DateTime.Now
    | None -> 
        Some DateTime.Now