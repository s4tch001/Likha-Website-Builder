[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = '0.1.0',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $RuntimeIdentifier = 'win-x64',

    [string] $CertificateThumbprint = '',

    [uri] $TimestampServer = 'http://timestamp.digicert.com',

    [string] $InnoCompiler = '',

    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'

function Assert-NativeSuccess([string] $Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Set-VerifiedSignature([string] $FilePath, $Certificate) {
    $signature = Set-AuthenticodeSignature `
        -FilePath $FilePath `
        -Certificate $Certificate `
        -TimestampServer $TimestampServer `
        -HashAlgorithm SHA256
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode signing failed for $FilePath`: $($signature.StatusMessage)"
    }
}
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\release'))
$packageName = "Likha-$Version-$RuntimeIdentifier"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $packageName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$packageName.zip"))
$installerPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$packageName-setup.exe"))
$installerChecksumPath = "$installerPath.sha256"
$artifactsPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $publishDirectory.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to package outside artifacts/release.'
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $installerPath) {
    Remove-Item -LiteralPath $installerPath -Force
}
if (Test-Path -LiteralPath $installerChecksumPath) {
    Remove-Item -LiteralPath $installerChecksumPath -Force
}

Push-Location (Join-Path $repositoryRoot 'src\WebsiteBuilder.Editor')
try {
    npm ci
    Assert-NativeSuccess 'npm ci'
    npm run build
    Assert-NativeSuccess 'Editor production build'
}
finally {
    Pop-Location
}

$dotnet = Join-Path $repositoryRoot '.dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = 'dotnet'
}

& $dotnet publish (Join-Path $repositoryRoot 'src\WebsiteBuilder.App\WebsiteBuilder.App.csproj') `
    --configuration Release `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishDirectory `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
Assert-NativeSuccess '.NET publish'

$executable = Join-Path $publishDirectory 'WebsiteBuilder.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published executable not found: $executable"
}
if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'wwwroot\index.html'))) {
    throw 'Published editor bundle is missing wwwroot/index.html.'
}

$certificate = $null
if ($CertificateThumbprint) {
    $normalizedThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object {
        $_.Thumbprint -eq $normalizedThumbprint -and $_.HasPrivateKey
    } | Select-Object -First 1
    if (-not $certificate) {
        throw 'The requested code-signing certificate was not found in Cert:\CurrentUser\My.'
    }
    Set-VerifiedSignature -FilePath $executable -Certificate $certificate
}

$hashEntries = Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
    Sort-Object FullName |
    ForEach-Object {
        [ordered]@{
            path = [System.IO.Path]::GetRelativePath($publishDirectory, $_.FullName).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            bytes = $_.Length
        }
    }
$manifest = [ordered]@{
    product = 'Likha - Website Builder'
    version = $Version
    runtime = $RuntimeIdentifier
    signed = [bool]$CertificateThumbprint
    files = @($hashEntries)
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $publishDirectory 'release-manifest.json') -Encoding utf8

Compress-Archive -LiteralPath $publishDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Package: $zipPath"
Write-Output "SHA256: $zipHash"

if ($SkipInstaller) {
    Write-Output 'Installer: skipped'
    return
}
if ($RuntimeIdentifier -ne 'win-x64') {
    throw 'The installer currently supports only win-x64. Use -SkipInstaller for other runtime identifiers.'
}

if (-not $InnoCompiler) {
    $candidates = @(
        (Join-Path $repositoryRoot '.tools\inno-7.1.0\ISCC.exe'),
        (Join-Path ${env:ProgramFiles} 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:LOCALAPPDATA} 'Programs\Inno Setup 7\ISCC.exe')
    )
    $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    throw 'Inno Setup 7 compiler not found. Run scripts/install-inno.ps1 or pass -InnoCompiler.'
}

$installerScript = Join-Path $repositoryRoot 'installer\Likha.iss'
& $InnoCompiler `
    "/DAppVersion=$Version" `
    "/DSourceDir=$publishDirectory" `
    "/DOutputDir=$artifactsRoot" `
    $installerScript
Assert-NativeSuccess 'Inno Setup compilation'

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not created at $installerPath."
}
if ($certificate) {
    Set-VerifiedSignature -FilePath $installerPath -Certificate $certificate
}

$installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$installerHash  $([System.IO.Path]::GetFileName($installerPath))" |
    Set-Content -LiteralPath $installerChecksumPath -Encoding ascii
Write-Output "Installer: $installerPath"
Write-Output "Installer SHA256: $installerHash"
