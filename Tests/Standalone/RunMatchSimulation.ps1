param(
    [string]$Player = "Builds/M52/Player/Dragonland.exe",
    [int]$Matches = 3,
    [int]$Seconds = 1800,
    [int]$MatchTimeout = 900,
    [int]$Seed = 52,
    [string]$Output = ""
)
$ErrorActionPreference = "Stop"
$playerPath = (Resolve-Path -LiteralPath $Player).Path
if ($Matches -lt 1 -or $Seconds -lt 5 -or $MatchTimeout -lt 5) { throw "Invalid simulation bounds" }
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = "Builds/M52/Soak-" + [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss") }
$reportPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath (Join-Path $reportPath "events.jsonl")) { throw "Choose a fresh output directory to preserve evidence" }
New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$arguments = @("-batchmode", "-nographics", "-hodrSoak", "-hodrSoakMatches", "$Matches", "-hodrSoakSeconds", "$Seconds", "-hodrSoakTimeout", "$MatchTimeout", "-hodrSoakSeed", "$Seed", "-hodrSoakOutput", ('"' + $reportPath + '"'), "-logFile", ('"' + (Join-Path $reportPath "Player.log") + '"'))
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds($Seconds + 90)
$summaryPath = Join-Path $reportPath "summary.json"
try {
    while (-not $process.HasExited) {
        if (Test-Path -LiteralPath $summaryPath) {
            try { $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json } catch { $summary = $null }
            if ($null -ne $summary -and $summary.message.StartsWith("Stopped:")) { break }
        }
        if ([DateTime]::UtcNow -gt $deadline) { throw "Simulation exceeded shutdown deadline; inspect $reportPath" }
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    }
    if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Player exited without a summary; inspect $reportPath" }
    Get-Content -LiteralPath $summaryPath
    Write-Output "Reports: $reportPath"
} finally {
    # Only this script's newly launched child is stopped, after its flushed report.
    $process.Refresh()
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
