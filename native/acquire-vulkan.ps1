param([string]$ArchivePath)
$ErrorActionPreference = 'Stop'
$taskRepository = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskVersion = '1.4.341.0'
$taskArchiveName = "VulkanRT-X64-$taskVersion-Components.zip"
$taskArchiveHash = '20571dcc8292b80a47d68bbbfa4094d87044041f50f4e2e1f326a01e3732b226'
$taskDllHash = '98bc6e33b384f83e8cc36174ce1ba81357611d0cbe03462e1ca58d0663034039'
$taskUrl = "https://sdk.lunarg.com/sdk/download/$taskVersion/windows/$taskArchiveName"
if (-not $ArchivePath) {
    $ArchivePath = Join-Path $taskRepository ".cache\$taskArchiveName"
    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ArchivePath)) | Out-Null
        & curl.exe -fL $taskUrl -o $ArchivePath
        if ($LASTEXITCODE -ne 0) { throw 'Official Vulkan runtime download failed.' }
    }
}
if ((Get-FileHash -LiteralPath $ArchivePath).Hash -ne $taskArchiveHash) { throw 'Vulkan archive SHA-256 mismatch.' }
$taskExtract = Join-Path $taskRepository ('.cache\vulkan-acquire-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $taskExtract
$taskComponents = Join-Path $taskExtract "VulkanRT-X64-$taskVersion-Components"
$taskDll = Join-Path $taskComponents 'x64\vulkan-1.dll'
if ((Get-FileHash -LiteralPath $taskDll).Hash -ne $taskDllHash) { throw 'Vulkan x64 DLL SHA-256 mismatch.' }
$taskSignature = Get-AuthenticodeSignature -LiteralPath $taskDll
if ($taskSignature.Status -ne 'Valid') { throw 'Official Vulkan DLL signature is not valid.' }
$taskTarget = Join-Path $PSScriptRoot 'win-x64'
Copy-Item -LiteralPath $taskDll -Destination (Join-Path $taskTarget 'vulkan-1.dll')
Copy-Item -LiteralPath (Join-Path $taskComponents 'VulkanRT-License.txt') -Destination (Join-Path $taskTarget 'VulkanRT-License.txt')
[ordered]@{
    purpose = 'PRIVATE TEST BUILD / LOCAL DEVELOPMENT DISTRIBUTION'
    sourceUrl = $taskUrl; metadataUrl = 'https://vulkan.lunarg.com/sdk/files.json'
    version = $taskVersion; archiveSha256 = $taskArchiveHash; dllSha256 = $taskDllHash
    architecture = 'x64'; signer = $taskSignature.SignerCertificate.Subject
    license = 'VulkanRT-License.txt (upstream MIT / Apache-2.0 notices)'
    system32Copy = $false
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskTarget 'vulkan-manifest.json') -Encoding utf8
Write-Output 'Pinned official x64 Vulkan loader and archive license acquired; no SDK or system installation performed.'
