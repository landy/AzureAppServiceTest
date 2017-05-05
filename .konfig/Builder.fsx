module Konfig.Builder

#I "../packages/FAKE/tools/"
#r "FakeLib.dll"

#load "Domain.fsx"

open Konfig.Domain
open Fake.MSBuildHelper
open Fake

let private safeBuild buildF rootDir cfg = 
    let dir = rootDir </> cfg.Build.OutputDirectory
    let source = rootDir </> cfg.SourceDirectory

    try
        traceImportant <| sprintf "Starting build in %s" source
        for file in !! (source + "/*.fsproj") do
            buildF dir "Build" [file] |> Log "Build-Output:"
    with e -> 
        traceError <| sprintf "Build for application in directory %s failed." dir
        traceError <| sprintf "Message: %s" e.Message
    ()

let buildDebug rootDir (cfg:ProjectConfig) = safeBuild MSBuildDebug rootDir cfg

let buildRelease rootDir (cfg:ProjectConfig) = safeBuild MSBuildRelease rootDir cfg

let clean rootDir (cfg:ProjectConfig) = rootDir </> cfg.Build.OutputDirectory |> CleanDir

module Default =
    let private path = __SOURCE_DIRECTORY__ </> "../"
    let buildDebug = buildDebug path    
    let buildRelease = buildRelease path
    let clean = clean path