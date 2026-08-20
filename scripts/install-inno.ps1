[CmdletBinding()]
param(
    [string] $Destination = '',
    [string] $DownloadDirectory = ''
)

$ErrorActionPreference = 'Stop'

$version = '7.1.0'
$assetName = "innosetup-$version-x64.exe"
$downloadUrl = "https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/$assetName"
$expectedSha256 = '0362a383ed217d4c4239b5933866dd96d3eb2102737da92f80f6057a4b40df2f'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if (-not $Destination) {
    $Destination = Join-Path $repositoryRoot ".tools\inno-$version"
}
if (-not $DownloadDirectory) {
    $DownloadDirectory = Join-Path $repositoryRoot '.tools\downloads'
}

$Destination = [System.IO.Path]::GetFullPath($Destination)
$DownloadDirectory = [System.IO.Path]::GetFullPath($DownloadDirectory)
$compiler = Join-Path $Destination 'ISCC.exe'

if (Test-Path -LiteralPath $compiler) {
    Write-Output $compiler
    return
}

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
$installer = Join-Path $DownloadDirectory $assetName

if (-not (Test-Path -LiteralPath $installer)) {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $installer
}

$actualSha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSha256 -ne $expectedSha256) {
    throw "Inno Setup checksum mismatch. Expected $expectedSha256 but received $actualSha256."
}

$signature = Get-AuthenticodeSignature -FilePath $installer
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "Inno Setup Authenticode verification failed: $($signature.StatusMessage)"
}

$arguments = @(
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/NORESTART',
    '/PORTABLE=1',
    "/DIR=$Destination"
)
$process = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) {
    throw "Inno Setup portable installation failed with exit code $($process.ExitCode)."
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "Inno Setup compiler was not created at $compiler."
}

Write-Output $compiler
