module Konfig.TaskRunner

#I "../packages/FAKE/tools/"
#r "FakeLib.dll"

open Konfig.Domain
open Fake

let private appOfflineName = "App_Offline.htm"

let private copyBuildTo rootDir (cfg:ProjectConfig) destination =
    let src = rootDir </> cfg.Build.OutputDirectory
    CopyDir destination src (fun _ -> true)

let private createAppOffline rootDir src destination = 
    let destFile = destination </> appOfflineName
    rootDir </> src |> Fake.FileHelper.CopyFile destFile

let private removeAppOffline destination = destination </> appOfflineName |> DeleteFile

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
    | Zip(destination) ->
        Konfig.Utils.runWithRepeat 100 (fun _ ->
            let src = rootDir </> cfg.Build.OutputDirectory
            !! (src + "/**/*.*") |> Zip src destination
        )
    | CopyTo(destination) -> Konfig.Utils.runWithRepeat 100 (fun _ -> destination |> copyBuildTo rootDir cfg)
    | CopyToIIS(destination, appOfflineSrc) ->
        try
            Konfig.Utils.runWithRepeat 100 (fun _ -> destination |> createAppOffline rootDir appOfflineSrc)
            Konfig.Utils.runWithRepeat 100 (fun _ -> destination |> copyBuildTo rootDir cfg)
        finally
            Konfig.Utils.runWithRepeat 100 (fun _ -> destination |> removeAppOffline)