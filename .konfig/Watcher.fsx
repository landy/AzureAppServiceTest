module Konfig.Watcher

#I "../packages/FAKE/tools/"
#r "FakeLib.dll"

#I "../packages/Suave/lib/net40"
#r "Suave.dll"

#r "System.Xml.Linq.dll"

open Suave
open Suave.Web
open Suave.Http
open Suave.Sockets
open Suave.Sockets.Control
open Suave.Sockets.AsyncSocket
open Suave.WebSocket
open Suave.Utils
open Suave.Files
open System.Net
open System.Diagnostics
open System.Xml.Linq

//#load "Domain.fsx"

open Konfig.Domain
open Fake
open Suave.Operators
open Fake.ChangeWatcher

let private startSocketWeb (refreshEvent:Event<_>) port =
    let socketHandler (webSocket : WebSocket) =
        fun cx -> socket {
            while true do
                let! refreshed =
                    Control.Async.AwaitEvent(refreshEvent.Publish) |> Suave.Sockets.SocketOp.ofAsync 
                do! webSocket.send Text (ByteSegment(ASCII.bytes "refreshed")) true
            }
    let serverConfig = 
            { defaultConfig with
               bindings = [ HttpBinding.create HTTP IPAddress.Any port ]
            }
    let app =
        choose [
            Filters.path "/websocket" >=> handShake socketHandler
            Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
            >=> Writers.setHeader "Pragma" "no-cache"
            >=> Writers.setHeader "Expires" "0"
            >=> browseHome
        ]
    startWebServerAsync serverConfig app |> snd |> Async.Start

let private startNotifications refreshEvent = function
    | Some (NotificationChannel.WebSocket(port)) -> port |> startSocketWeb refreshEvent
    | None -> ()

let private getRunnableName rootDir cfg =
    let source = rootDir </> cfg.SourceDirectory
    let project = seq { for file in !! (source + "/*.fsproj") do yield file } |> Seq.head |> XDocument.Load
    let ns = project.Root.GetDefaultNamespace().NamespaceName
    let output = project.Descendants(XName.Get("OutputType", ns)) |> Seq.head
    match output.Value |> String.toLowerInvariant with
    | "exe" -> 
        let assemblyName = project.Descendants(XName.Get("AssemblyName", ns)) |> Seq.head
        sprintf "%s.exe" assemblyName.Value |> Some
    | _ -> None

let private startApplication rootDir cfg =
    match cfg |> getRunnableName rootDir with
    | Some(filename) ->
        let buildDir = rootDir </> cfg.Build.OutputDirectory
        {
            Program = buildDir </> filename
            WorkingDirectory = buildDir
            CommandLine = ""
            Args = []
        } |> asyncShellExec |> Async.StartAsTask |> ignore
    | None -> ()

let private stopApplication rootDir cfg =
    match cfg |> getRunnableName rootDir with
    | Some(filename) ->
        let processName = filename |> Fake.FileHelper.fileNameWithoutExt
        traceImportant <| sprintf "Stopping process %s" filename
        Process.GetProcessesByName(processName) |> Seq.iter (fun p -> p.Kill())
    | None -> ()

let private foundFor (fileWatches:FileWatch list) (fileChanges:seq<FileChange>) =
    let isFound = function
        | ByExtension(ext) ->
            fileChanges
            |> Seq.toList
            |> List.map (fun x -> x.FullPath)
            |> List.filter (Fake.FileHelper.hasExt ext)
            |> (List.isEmpty >> not)
        
    fileWatches |> List.filter isFound |> List.isEmpty |> not

let private startWatcher staticCallback dynamicCallback rootDir (cfg:ProjectConfig) =
    let appDir = rootDir </> cfg.SourceDirectory |> System.IO.Path.GetFullPath
    let watcher = !! (appDir + "/**/*.*") |> WatchChanges (fun changes ->
        if changes |> foundFor cfg.Watch.Dynamics then
            cfg |> dynamicCallback
        else if changes |> foundFor cfg.Watch.Statics then
            cfg |> staticCallback
        else
            ()
    )
    watcher

let watch rootDir (staticChange:ProjectConfig -> unit) (dynamicChange:ProjectConfig -> unit) (cfg:ProjectConfig) = 
    let refreshEvent = new Event<_>()
    
    let callBack callF cfg =
       cfg |> stopApplication rootDir
       cfg |> callF
       cfg |> startApplication rootDir
       refreshEvent.Trigger()
    
    let staticCallback = callBack staticChange
    let dynamicCallback = callBack dynamicChange

    cfg |> startApplication rootDir
    cfg.Watch.NotificationChannel |> startNotifications refreshEvent
    startWatcher staticCallback dynamicCallback rootDir cfg |> ignore