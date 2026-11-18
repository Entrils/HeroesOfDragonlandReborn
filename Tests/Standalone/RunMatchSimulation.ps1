param(
    [string]$Player = "Builds/M52/Player/Dragonland.exe",
    [int]$Matches = 3,
    [int]$Seconds = 1800,
    [int]$MatchTimeout = 900,
    [int]$StuckSeconds = 30,
    [int]$Seed = 52,
    [string]$Output = ""
)
$ErrorActionPreference = "Stop"
$playerPath = (Resolve-Path -LiteralPath $Player).Path
if ($Matches -lt 1 -or $Matches -gt 100000 -or $Seconds -lt 5 -or $MatchTimeout -lt 5 -or $StuckSeconds -lt 5) { throw "Invalid simulation bounds" }
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = "Builds/M52/Soak-" + [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss") }
$reportPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath (Join-Path $reportPath "events.jsonl")) { throw "Choose a fresh output directory to preserve evidence" }
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$arguments = @("-batchmode", "-nographics", "-hodrSoak", "-hodrSoakMatches", "$Matches", "-hodrSoakSeconds", "$Seconds", "-hodrSoakTimeout", "$MatchTimeout", "-hodrSoakStuck", "$StuckSeconds", "-hodrSoakSeed", "$Seed", "-hodrSoakOutput", ('"' + $reportPath + '"'), "-logFile", ('"' + (Join-Path $reportPath "Player.log") + '"'))
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds($Seconds + 90)
$summaryPath = Join-Path $reportPath "summary.json"
try {
    while (-not $process.HasExited) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Simulation exceeded shutdown deadline; inspect $reportPath" }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Player exited without a summary; inspect $reportPath" }
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    Get-Content -LiteralPath $summaryPath
    Write-Output "Reports: $reportPath"
    if ($process.ExitCode -ne 0 -or -not $summary.message.StartsWith("Stopped:")) {
        throw "Simulation did not pass (player exit $($process.ExitCode)); inspect $reportPath"
    }
} finally {
    # Terminate only this script's child if it missed its shutdown deadline.
    $process.Refresh()
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
    $process.Dispose()
}
