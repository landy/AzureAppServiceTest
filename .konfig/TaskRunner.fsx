module Konfig.TaskRunner

#I "../packages/FAKE/tools/"
#r "FakeLib.dll"

open Konfig.Domain
open Fake

let runPostBuild rootDir (cfg:ProjectConfig) = function
    | CopyDirectory(src,dest) -> 
        let src = rootDir </> cfg.SourceDirectory </> src
        let dest = rootDir </> cfg.Build.OutputDirectory </> dest
        CopyDir dest src (fun _ -> true)
    | TransformConfig(src,trans) ->
        let src = rootDir </> cfg.SourceDirectory </> src
        let trans = rootDir </> cfg.SourceDirectory </> trans
        let dest = rootDir </> cfg.Build.OutputDirectory </> (Fake.FileHelper.filename src)
        XDTHelper.TransformFile src trans dest

let runDeploy rootDir (cfg:ProjectConfig) = function
    | FileSystemCopy(destination) ->
        let src = rootDir </> cfg.Build.OutputDirectory
        CopyDir destination src (fun _ -> true)