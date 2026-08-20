[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = '0.1.0',

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $RuntimeIdentifier = 'win-x64',

    [string] $CertificateThumbprint = '',

    [uri] $TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

function Assert-NativeSuccess([string] $Step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\release'))
$packageName = "Likha-$Version-$RuntimeIdentifier"
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $packageName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$packageName.zip"))
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

if ($CertificateThumbprint) {
    $normalizedThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object {
        $_.Thumbprint -eq $normalizedThumbprint -and $_.HasPrivateKey
    } | Select-Object -First 1
    if (-not $certificate) {
        throw 'The requested code-signing certificate was not found in Cert:\CurrentUser\My.'
    }
    $signature = Set-AuthenticodeSignature `
        -FilePath $executable `
        -Certificate $certificate `
        -TimestampServer $TimestampServer `
        -HashAlgorithm SHA256
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode signing failed: $($signature.StatusMessage)"
    }
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
