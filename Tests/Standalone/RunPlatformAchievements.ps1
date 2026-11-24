param([Parameter(Mandatory = $true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$player = (Resolve-Path -LiteralPath $Executable).Path
$profile = [Guid]::NewGuid().ToString('N')
$directory = New-Item -ItemType Directory -Path "Builds/AchievementAcceptance-$($profile.Substring(0,8))"
foreach ($case in @('Seed', 'Verify')) {
    $log = Join-Path $directory.FullName "$case.log"
    $process = Start-Process -FilePath $player -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-nographics', '-hodrProfileTest', $profile, "-hodrAchievement$case", '-logFile', ('"' + $log + '"'))
    try {
        if (!$process.WaitForExit(60000)) { throw "Achievement fixture timed out: $log" }
        if ($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern '\[AchievementCheck\] PASS' -Quiet)) {
            throw "Achievement fixture failed: $log"
        }
    } finally {
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        $process.Dispose()
    }
}
Write-Output "PASS: all six achievement IDs persisted and duplicate match receipts were ignored. Logs: $($directory.FullName)"
