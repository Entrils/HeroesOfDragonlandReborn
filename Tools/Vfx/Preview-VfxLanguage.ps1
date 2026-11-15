$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'ArtSource/Vfx/VfxLanguage.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$shapes = @{
    spark='-.45,-.15 -.1,0 -.35,.4 0,.1 .35,.4 .1,0 .45,-.15'
    shield='-.4,.4 .4,.4 .32,-.18 0,-.45 -.32,-.18 -.4,.4'
    star='0,.5 .1,.1 .5,0 .1,-.1 0,-.5 -.1,-.1 -.5,0 -.1,.1 0,.5'
    break='-.4,.4 -.12,.4 .04,.15 -.12,-.06 .1,-.2 0,-.45 .35,-.15 .4,.4 .15,.4'
    slash='-.5,-.3 -.1,-.1 .3,.2 .5,.45'
    ash='-.35,-.3 -.2,0 -.32,.3 0,.1 .22,.4 .15,-.1 .35,-.3'
    bolt='-.45,-.1 -.1,.05 -.2,.28 .4,0 0,-.06 .05,-.28 -.45,-.1'
    cross='-.4,.1 -.1,.1 -.1,.4 .1,.4 .1,.1 .4,.1 .4,-.1 .1,-.1 .1,-.4 -.1,-.4 -.1,-.1 -.4,-.1 -.4,.1'
    ward='-.45,.3 -.35,.5 .35,.5 .45,.3 .38,-.25 0,-.5 -.38,-.25 -.45,.3'
    rune='0,.45 .35,0 0,-.45 -.35,0 0,.45 0,-.45'
    rise='-.4,-.2 0,.2 .4,-.2 0,.2 0,.5 -.2,.3 0,.5 .2,.3'
    diamond='0,.45 .3,0 0,-.45 -.3,0 0,.45'
    lid='-.4,-.3 .4,-.3 .4,.05 -.4,.05 -.25,.4 .45,.4 .4,.05 -.4,.05 -.4,-.3'
    notches='-.4,-.3 -.4,.3 -.2,.3 -.2,-.3 0,-.3 0,.3 .2,.3 .2,-.3 .4,-.3 .4,.3'
    crown='-.45,.35 -.35,-.3 .35,-.3 .45,.35 .15,.05 0,.4 -.15,.05 -.45,.35'
    brokenCrown='-.45,.35 -.35,-.3 -.08,-.3 .08,-.05 -.05,.15 .12,.4 .3,.05 .45,.35 .35,-.3 .15,-.3'
    laurel='-.4,.4 -.5,0 -.3,-.4 0,-.5 .3,-.4 .5,0 .4,.4'
}
if ($manifest.effects.Count -ne 17 -or @($manifest.effects.kind | Sort-Object -Unique).Count -ne 17) { throw 'Expected 17 unique effect kinds.' }
$svg = [System.Text.StringBuilder]::new()
[void]$svg.AppendLine('<svg xmlns="http://www.w3.org/2000/svg" width="1260" height="1050" viewBox="0 0 1260 1050">')
[void]$svg.AppendLine('<rect width="1260" height="1050" fill="#161d22"/><g font-family="Segoe UI, sans-serif" fill="#e6dfcf">')
[void]$svg.AppendLine('<text x="30" y="42" font-size="26">Dragonland - Combat and Gameplay VFX</text>')
[void]$svg.AppendLine('<text x="30" y="74" font-size="15" fill="#b5bdc2">Design sheet: compact glyphs + sparse bursts. Not a Unity render or timing validation.</text>')
[void]$svg.AppendLine('<path d="M34 112 Q48 91 62 112" stroke="#8cd1cc" stroke-width="3" fill="none"/><text x="74" y="113" font-size="14">Friendly: smooth/open</text>')
[void]$svg.AppendLine('<path d="M345 113 L352 96 L359 110 L366 92 L373 110 L380 96 L387 113" stroke="#ff913d" stroke-width="3" fill="none"/><text x="403" y="113" font-size="14">Hostile: outward teeth</text>')
[void]$svg.AppendLine('<path d="M728 93 L740 104 L728 115 L716 104 Z" stroke="#c8b994" stroke-width="3" fill="none"/><text x="753" y="113" font-size="14">Neutral: diamond</text>')
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$index = 0
foreach ($effect in $manifest.effects) {
    if (-not $shapes.ContainsKey($effect.shape) -or $effect.particles -gt 16 -or $effect.maximumConcurrent -gt 8) { throw 'Unknown glyph or invalid VFX budget.' }
    $x = 30 + ($index % 4) * 305
    $y = 145 + [math]::Floor($index / 4) * 163
    $points = foreach ($pair in $shapes[$effect.shape].Split(' ')) {
        $xy = $pair.Split(',')
        $px = 145 + [double]::Parse($xy[0], $culture) * 66
        $py = 58 - [double]::Parse($xy[1], $culture) * 66
        $px.ToString('0.##', $culture) + ',' + $py.ToString('0.##', $culture)
    }
    $pointText = $points -join ' '
    [void]$svg.AppendLine("<g transform=`"translate($x,$y)`"><rect width=`"290`" height=`"148`" rx=`"8`" fill=`"#252e34`"/>")
    [void]$svg.AppendLine("<polyline points=`"$pointText`" stroke=`"#$($effect.color)`" stroke-width=`"3`" fill=`"none`" stroke-linejoin=`"round`"/>")
    [void]$svg.AppendLine("<text x=`"14`" y=`"111`" font-size=`"17`">$($effect.label)</text>")
    $duration = ([double]$effect.duration).ToString('0.##', $culture)
    [void]$svg.AppendLine("<text x=`"14`" y=`"132`" font-size=`"12`" fill=`"#adb7bc`">${duration}s | $($effect.particles) particles | cap $($effect.maximumConcurrent) | priority $($effect.priority)</text></g>")
    $index++
}
[void]$svg.AppendLine('<text x="30" y="1005" font-size="15">32 active effects max. Existing attack telegraphs remain outside this pool.</text></g></svg>')
$output = Join-Path $projectRoot 'Documentation/VfxLanguage.svg'
[System.IO.File]::WriteAllText($output, $svg.ToString(), [System.Text.UTF8Encoding]::new($false))
$null = [xml](Get-Content -LiteralPath $output -Raw -Encoding UTF8)
Write-Output 'PASS: 17 glyphs and audience legend written to Documentation/VfxLanguage.svg; valid SVG/XML.'
