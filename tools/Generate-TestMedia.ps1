param(
    [Parameter(Mandatory=$true)][string]$FFmpeg,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.cache\media-suite')
)
$ErrorActionPreference = 'Stop'
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskSubtitle = Join-Path $taskOutput 'external café.srt'
[IO.File]::WriteAllText($taskSubtitle, "1`n00:00:00,000 --> 00:00:03,500`nEffortLess 字幕 test`n", [Text.UTF8Encoding]::new($false))
$taskAss = Join-Path $taskOutput 'external.ass'
[IO.File]::WriteAllText($taskAss, @'
[Script Info]
ScriptType: v4.00+
PlayResX: 320
PlayResY: 180
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Arial,18,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,0,2,10,10,10,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.00,0:00:03.50,Default,,0,0,0,,EffortLess ASS test
'@, [Text.UTF8Encoding]::new($false))
function Invoke-Fixture([string]$Name, [string[]]$Arguments) {
    & $FFmpeg -hide_banner -loglevel error -y @Arguments (Join-Path $taskOutput $Name)
    if ($LASTEXITCODE -ne 0) { throw "FFmpeg failed for $Name ($LASTEXITCODE)" }
}
$taskVideo = @('-f','lavfi','-i','testsrc2=size=320x180:rate=15','-t','4','-threads','2')
Invoke-Fixture 'h264.mp4' ($taskVideo + @('-c:v','libx264','-preset','ultrafast','-pix_fmt','yuv420p'))
Invoke-Fixture 'hevc.mp4' ($taskVideo + @('-c:v','libx265','-preset','ultrafast','-x265-params','pools=1:frame-threads=1:log-level=error'))
Invoke-Fixture 'vp9.webm' ($taskVideo + @('-c:v','libvpx-vp9','-deadline','realtime','-cpu-used','8'))
Invoke-Fixture 'av1.webm' ($taskVideo + @('-c:v','libaom-av1','-cpu-used','8','-row-mt','1'))
Invoke-Fixture 'mpeg4.avi' ($taskVideo + @('-c:v','mpeg4','-q:v','4'))
Invoke-Fixture 'h264.mov' @('-i',(Join-Path $taskOutput 'h264.mp4'),'-c','copy')
Invoke-Fixture 'portrait.mp4' @('-f','lavfi','-i','testsrc2=size=180x320:rate=15','-t','4','-threads','2','-c:v','libx264','-preset','ultrafast')
Invoke-Fixture 'ultrawide.mp4' @('-f','lavfi','-i','testsrc2=size=480x120:rate=15','-t','4','-threads','2','-c:v','libx264','-preset','ultrafast')
# Exact frame-size fixtures, also looped for installed-app metadata observation.
foreach ($taskResolution in @(@('720p','1280x720'), @('1080p','1920x1080'), @('4k','3840x2160'))) {
    $taskResolutionName = 'resolution-' + $taskResolution[0] + '.mp4'
    Invoke-Fixture $taskResolutionName @('-f','lavfi','-i',('testsrc2=size=' + $taskResolution[1] + ':rate=6'),'-t','4','-threads','2','-c:v','libx264','-preset','ultrafast','-pix_fmt','yuv420p')
    Invoke-Fixture ('ui-' + $taskResolutionName) @('-stream_loop','29','-i',(Join-Path $taskOutput $taskResolutionName),'-c','copy')
}
Invoke-Fixture 'audio-only.flac' @('-f','lavfi','-i','sine=frequency=440:duration=4','-c:a','flac')
Invoke-Fixture 'multi-track.mkv' @('-i',(Join-Path $taskOutput 'h264.mp4'),'-f','lavfi','-i','sine=frequency=440:duration=4','-f','lavfi','-i','sine=frequency=880:duration=4','-i',$taskSubtitle,'-map','0:v','-map','1:a','-map','2:a','-map','3:s','-c:v','copy','-c:a','aac','-c:s','srt','-metadata:s:a:0','language=eng','-metadata:s:a:1','language=jpn','-metadata:s:s:0','language=eng','-t','4')
Copy-Item -LiteralPath (Join-Path $taskOutput 'h264.mp4') -Destination (Join-Path $taskOutput 'Unicode café 東京.mp4')
[IO.File]::WriteAllText((Join-Path $taskOutput 'corrupt.mp4'), 'This is deliberately not media.')
# Longer fixtures give manual track/picker checks time before EOF.
Invoke-Fixture 'ui-multi-track.mkv' @('-stream_loop','29','-i',(Join-Path $taskOutput 'multi-track.mkv'),'-map','0','-c','copy')
[IO.File]::WriteAllText((Join-Path $taskOutput 'ui-external.srt'), "1`n00:00:00,000 --> 00:02:00,000`nExternal SRT validation`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $taskOutput 'external.ssa'), @'
[Script Info]
ScriptType: v4.00
PlayResX: 320
PlayResY: 180
[V4 Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding
Style: Default,Arial,18,16777215,16777215,0,0,0,0,1,1,0,2,10,10,10,0,1
[Events]
Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: Marked=0,0:00:00.00,0:00:03.50,Default,,0,0,0,,EffortLess SSA test
'@, [Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $taskOutput -File | Select-Object Name,Length
