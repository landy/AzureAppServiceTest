// include Fake lib
#I "packages/FAKE/tools/"

#r "FakeLib.dll"

#load ".konfig/Domain.fsx"
#load ".konfig/Runner.fsx"

open Fake
open Konfig.Domain
open Fake.AssemblyInfoFile

let consoleConfig:ProjectConfig = {
        SourceDirectory = "src/AzureAppServiceTest"
        ReleaseNotes = None
        MetaData = [ Attribute.Copyright "Roman Provaznik" ]
        Watch = { 
                Statics = [ByExtension(".txt")]
                Dynamics = [ByExtension(".fs")]
                NotificationChannel = NotificationChannel.WebSocket(Port.Parse("8183"))
        }
        Build = { 
                SharedTasks = []
                OutputDirectory = "build/AzureAppServiceTest"
                ReleaseTasks = []
                DebugTasks = []
        }
        Deploy = {
                    Tasks = []
        }
    }

let configs = [consoleConfig]

Target "Build" (fun _ ->
    Konfig.Runner.Default.buildRelease configs
)

Target "Watch" (fun _ -> 
    Konfig.Runner.Default.watch configs
    System.Console.ReadKey() |> ignore
)

Target "Deploy" (fun _ ->
    Konfig.Runner.Default.deploy configs
    let deployDir = "deploy/"
    System.IO.Directory.CreateDirectory deployDir |> ignore
    let buildDir = "build/AzureAppServiceTest/"   
    !! (buildDir + "/**/*.*")
        -- "*.zip"
        |> Zip buildDir (deployDir + "AzureAppServiceTest.zip")
)

// start build
RunTargetOrDefault "Build"