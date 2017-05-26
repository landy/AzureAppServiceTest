module Konfig.Runner

#I "../packages/FAKE/tools/"
#r "FakeLib.dll"

#load "Domain.fsx"
#load "Builder.fsx"
#load "TaskRunner.fsx"
#load "Watcher.fsx"

open Konfig.Domain
open Fake.MSBuildHelper
open Fake
open Fake.ReleaseNotesHelper
open Fake.AssemblyInfoFile
open System.IO

let private changeAssemblyInfo srcFolder attributes =
    for file in !! (srcFolder + "/AssemblyInfo*.fs") do
        CreateFSharpAssemblyInfo file attributes

let private getReleaseNotesAttributes srcFolder (cfg:ProjectConfig) =
    match cfg.ReleaseNotes with
    | Some notes ->
        let notes = srcFolder </> notes
        File.ReadLines notes 
        |> ReleaseNotesHelper.parseReleaseNotes 
        |> (fun notes -> [ Attribute.Version notes.AssemblyVersion; Attribute.FileVersion notes.AssemblyVersion ])
    | None -> []
    
let private setAssemblyInfo rootDir (cfg:ProjectConfig) =
    let srcFolder = rootDir </> cfg.SourceDirectory
    cfg.MetaData @ (getReleaseNotesAttributes srcFolder cfg) 
    |> (fun attrs -> if attrs |> List.length > 0 then changeAssemblyInfo srcFolder attrs)

let private runReleaseTasks rootDir cfg =
    cfg.Build.SharedTasks @ cfg.Build.ReleaseTasks 
    |> List.distinct 
    |> List.iter (TaskRunner.runPostBuild rootDir cfg)

let private runDebugTasks rootDir cfg =
    cfg.Build.SharedTasks @ cfg.Build.DebugTasks 
    |> List.distinct 
    |> List.iter (TaskRunner.runPostBuild rootDir cfg)

let clean = Builder.clean

let private build rootDir configs buildF tasksF =
    let run cfg = async {
        cfg |> Builder.clean rootDir
        cfg |> setAssemblyInfo rootDir
        cfg |> buildF rootDir
        cfg |> tasksF rootDir
    }
    let par, seq = configs |> List.partition (fun x -> x.Build.SupportsParallelBuild)
    seq |> List.iter (run >> Async.RunSynchronously)
    par |> List.map run |> Async.Parallel |> Async.RunSynchronously |> ignore

let buildRelease rootDir configs = build rootDir configs Builder.buildRelease runReleaseTasks 
    
let buildDebug rootDir configs = build rootDir configs Builder.buildDebug runDebugTasks

let watch rootDir configs =
    configs |> buildDebug rootDir
    let staticF = runDebugTasks rootDir
    let dynamicF = Builder.buildDebug rootDir
    let asyncWatch cfg = async { 
        Watcher.watch rootDir staticF dynamicF cfg
    }
    configs |> List.map asyncWatch |> Async.Parallel |> Async.RunSynchronously |> ignore

let deploy rootDir configs = 
    let asyncDeploy cfg = async {
        cfg.Deploy.Tasks |> List.iter (TaskRunner.runDeploy rootDir cfg)
    }
    configs |> buildRelease rootDir
    configs |> List.map asyncDeploy |> Async.Parallel |> Async.RunSynchronously |> ignore

module Default =
    let private path = __SOURCE_DIRECTORY__ </> "../"
    let buildDebug = buildDebug path    
    let buildRelease = buildRelease path
    let clean = clean path    
    let watch = watch path
    let deploy = deploy path