$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'This standalone policy check requires the Windows .NET Framework C# compiler.' }
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Force '.utmp/M59Validation' | Out-Null
    & $compiler /nologo /target:exe /out:.utmp\M59Validation\VfxPolicyValidation.exe .\Assets\Game\Presentation\Vfx\VfxBudget.cs .\Tests\Standalone\VfxPolicyValidation.cs
    if ($LASTEXITCODE -ne 0) { throw 'VFX policy compilation failed.' }
    & ./.utmp/M59Validation/VfxPolicyValidation.exe
    if ($LASTEXITCODE -ne 0) { throw 'VFX policy validation failed.' }
} finally { Pop-Location }
