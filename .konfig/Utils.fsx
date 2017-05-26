module Konfig.Utils

// include Fake lib
#I "../packages/FAKE/tools/"

#r "FakeLib.dll"

open Fake

let runWithRepeat times fn = 
    let rec repeat timesLeft =
        if timesLeft > 0 then
            try fn() with _ -> repeat (timesLeft - 1)
        else
            traceImportant <| sprintf "Could not finish task within %i tries. Making last try now..." times
            fn()
    repeat times

module Default =
    let runWithRepeat fn = runWithRepeat 100 fn