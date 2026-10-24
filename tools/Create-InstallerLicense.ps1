param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
# Windows PowerShell's inbox RichEdit parser preserves the rendered SDK RTF text.
# This is a build-time tool only; the installed app has no dependency on it.
Add-Type -AssemblyName System.Windows.Forms
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskSdk = New-Object System.Windows.Forms.RichTextBox
try {
    $taskSdk.Rtf = [IO.File]::ReadAllText((Join-Path $taskRoot 'docs\third-party\Windows-SDK-license.rtf'))
    $taskSdkText = $taskSdk.Text
    if ($taskSdkText -notmatch 'Distributable Code' -or $taskSdkText.Length -lt 10000) {
        throw 'SDK RTF did not parse completely.'
    }
}
finally { $taskSdk.Dispose() }
$taskSections = @(
    "EFFORTLESS LICENSES AND THIRD-PARTY RUNTIME TERMS`n`nEffortLess original code is free software under GNU GPL version 3 or later, with the Windows runtime additional permission below. You do not need to accept the GPL merely to receive or run GPL-covered code. Your GPL rights are preserved. Microsoft component terms below apply only to their respective components; by installing those components you agree to their applicable terms. They do not restrict rights granted for EffortLess's GPL-covered code. Third-party notices and full original license documents are installed with the program. Native LGPL libraries may be replaced with compatible modified versions; corresponding source and build materials accompany the release."
    "GNU GPL VERSION 3`n" + [IO.File]::ReadAllText((Join-Path $taskRoot 'LICENSE'))
    "WINDOWS RUNTIME ADDITIONAL PERMISSION`n" + [IO.File]::ReadAllText((Join-Path $taskRoot 'ADDITIONAL-PERMISSION.md'))
    "MICROSOFT WINDOWS APP SDK TERMS`n" + [IO.File]::ReadAllText((Join-Path $taskRoot 'docs\third-party\nuget\microsoft.windowsappsdk.runtime\1.8.260921001\license.txt'))
    "MICROSOFT WINDOWS ML TERMS`n" + [IO.File]::ReadAllText((Join-Path $taskRoot 'docs\third-party\nuget\microsoft.windowsappsdk.ml\1.8.2223\license.txt'))
    "MICROSOFT WINDOWS SDK TERMS`n" + $taskSdkText
)
$taskText = $taskSections -join "`n`n------------------------------------------------------------`n`n"
$taskRtf = New-Object Text.StringBuilder
[void]$taskRtf.Append('{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs18 ')
foreach ($taskCharacter in $taskText.ToCharArray()) {
    $taskCode = [int]$taskCharacter
    if ($taskCharacter -in @('\','{','}')) { [void]$taskRtf.Append('\').Append($taskCharacter) }
    elseif ($taskCode -eq 10) { [void]$taskRtf.Append('\par ' + "`r`n") }
    elseif ($taskCode -eq 13) { continue }
    elseif ($taskCode -gt 127) {
        if ($taskCode -gt 32767) { $taskCode -= 65536 }
        [void]$taskRtf.Append('\u').Append($taskCode).Append('?')
    }
    else { [void]$taskRtf.Append($taskCharacter) }
}
[void]$taskRtf.Append('}')
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Output), $taskRtf.ToString(), [Text.Encoding]::ASCII)
Write-Output ('Installer license presentation generated; original license texts remain unchanged.')
