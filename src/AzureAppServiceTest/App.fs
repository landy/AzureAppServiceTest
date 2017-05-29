open Suave
open Suave.IIS
open System.Net
open Suave.Successful
open Suave.Operators

[<EntryPoint>]
let main argv =

    // use IIS related filter functions
    let path st = Suave.IIS.Filters.path argv st
    let pathScan format = Suave.IIS.Filters.pathScan argv format

    // routes
    let webpart =
        choose [
            path "/test2" >=> OK "Ha! F# web routing run"
            path "/test" >=> OK "Ha! F# web routing run"
            path "/" >=> OK "Ha! F# web right from GitHub with autoredeploy - cool"
        ]

    // start service server
    let config = { defaultConfig with bindings=[HttpBinding.create HTTP IPAddress.Any 8080us]; } |> Suave.IIS.Configuration.withPort argv
    startWebServer config webpart
    0