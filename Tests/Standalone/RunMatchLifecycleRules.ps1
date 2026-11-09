param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $UnityData 'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
& (Join-Path $UnityData 'DotNetSdk/dotnet.exe') $compiler /nologo /langversion:latest /nostdlib `
    /reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll `
    /reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll `
    /reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll `
    /out:Builds/LifecycleRulesValidation.exe Assets/Game/Core/MatchPhase.cs `
    Assets/Game/Core/NetworkTeamRoster.cs Assets/Game/Core/TeamId.cs Tests/Standalone/MatchLifecycleRulesValidation.cs
if ($LASTEXITCODE -ne 0) { throw 'Lifecycle rules compilation failed.' }
& ./Builds/LifecycleRulesValidation.exe
if ($LASTEXITCODE -ne 0) { throw 'Lifecycle rules validation failed.' }
