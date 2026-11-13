#Requires -Version 7.0

[CmdletBinding()]
param (
    [string[]] $DnsNames = @([System.Net.Dns]::GetHostName(), 'localhost'),

    [Parameter(Mandatory)]
    [string[]] $IpAddresses,

    [string] $OutputDirectory = (Join-Path $env:LOCALAPPDATA 'AtariGo/certificates')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw [PlatformNotSupportedException]::new('Windows certificate stores are required.')
}

$DnsNames = @($DnsNames | Sort-Object -Unique)
foreach ($dnsName in $DnsNames) {
    if ([Uri]::CheckHostName($dnsName) -ne [UriHostNameType]::Dns) {
        throw [ArgumentException]::new('Each DNS name must be a valid hostname.')
    }
}

$IpAddresses = @($IpAddresses + @('127.0.0.1', '::1') | ForEach-Object {
    $address = [System.Net.IPAddress]::Parse($_)
    if ($address.Equals([System.Net.IPAddress]::Any) -or
        $address.Equals([System.Net.IPAddress]::IPv6Any)) {
        throw [ArgumentException]::new('Wildcard listening addresses cannot identify the server.')
    }
    $address.ToString()
} | Sort-Object -Unique)

$repositoryPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
if ($outputPath.Equals($repositoryPath, [StringComparison]::OrdinalIgnoreCase) -or
    $outputPath.StartsWith($repositoryPath + [System.IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw [ArgumentException]::new('Certificate output must remain outside the repository.')
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$statePath = Join-Path $outputPath 'server.json'
$keyLengthBits = 3072
$authorityLifetimeYears = 3
$serverLifetimeYears = 1
$state = @{}
if (Test-Path -LiteralPath $statePath) {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json -AsHashtable
}

$rootCertificate = $null
if ($state.ContainsKey('RootThumbprint')) {
    $rootCertificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $state.RootThumbprint)
    if (-not $rootCertificate.HasPrivateKey -or $rootCertificate.NotAfter -le (Get-Date)) {
        throw [InvalidOperationException]::new('The existing CA must be restored or replaced.')
    }
}
else {
    $authorityId = [Guid]::NewGuid().ToString('N')
    $rootOptions = @{
        Type = 'Custom'
        Subject = 'CN=AtariGo LAN Authority ' + $authorityId
        FriendlyName = 'AtariGo LAN certificate authority'
        CertStoreLocation = 'Cert:\CurrentUser\My'
        KeyAlgorithm = 'RSA'
        KeyLength = $keyLengthBits
        HashAlgorithm = 'SHA256'
        KeyExportPolicy = 'NonExportable'
        KeyUsage = @('CertSign', 'CRLSign')
        NotAfter = (Get-Date).AddYears($authorityLifetimeYears)
        TextExtension = @(
            '2.5.29.19={critical}{text}ca=1&pathlength=0',
            '2.5.29.37={text}1.3.6.1.5.5.7.3.1'
        )
    }
    $rootCertificate = New-SelfSignedCertificate @rootOptions
    $state.RootThumbprint = $rootCertificate.Thumbprint
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
}

$rootPublicPath = Join-Path $outputPath 'AtariGo-LAN-Root.cer'
Export-Certificate -Cert $rootCertificate -FilePath $rootPublicPath -Force | Out-Null
$rootSha256 = (Get-FileHash -LiteralPath $rootPublicPath -Algorithm SHA256).Hash
& (Join-Path $PSScriptRoot 'Install-LanTrust.ps1') -CertificatePath $rootPublicPath `
    -ExpectedSha256 $rootSha256

$serverCertificate = $null
$renewalWindowDays = 30
if ($state.ContainsKey('ServerThumbprint')) {
    $existingCertificate = Get-Item -LiteralPath (
        'Cert:\CurrentUser\My\' + $state.ServerThumbprint) -ErrorAction SilentlyContinue
    $namesMatch = ($state.DnsNames -join ',') -eq ($DnsNames -join ',') -and
        ($state.IpAddresses -join ',') -eq ($IpAddresses -join ',')
    if ($null -ne $existingCertificate -and $namesMatch -and
        $existingCertificate.HasPrivateKey -and
        $existingCertificate.NotAfter -gt (Get-Date).AddDays($renewalWindowDays)) {
        $serverCertificate = $existingCertificate
    }
}

if ($null -eq $serverCertificate) {
    $subjectEntries = @($DnsNames | ForEach-Object { 'DNS=' + $_ }) +
        @($IpAddresses | ForEach-Object { 'IPAddress=' + $_ })
    $serverId = [Guid]::NewGuid().ToString('N')
    $leafExpiry = (Get-Date).AddYears($serverLifetimeYears)
    if ($leafExpiry -gt $rootCertificate.NotAfter) {
        $leafExpiry = $rootCertificate.NotAfter
    }
    $serverOptions = @{
        Type = 'Custom'
        Subject = 'CN=AtariGo Server ' + $serverId
        FriendlyName = 'AtariGo HTTPS server'
        CertStoreLocation = 'Cert:\CurrentUser\My'
        Signer = $rootCertificate
        KeyAlgorithm = 'RSA'
        KeyLength = $keyLengthBits
        HashAlgorithm = 'SHA256'
        KeyExportPolicy = 'NonExportable'
        KeyUsage = @('DigitalSignature', 'KeyEncipherment')
        NotAfter = $leafExpiry
        TextExtension = @(
            '2.5.29.19={critical}{text}ca=0',
            '2.5.29.37={text}1.3.6.1.5.5.7.3.1',
            ('2.5.29.17={text}' + ($subjectEntries -join '&'))
        )
    }
    $serverCertificate = New-SelfSignedCertificate @serverOptions
}

$state.ServerThumbprint = $serverCertificate.Thumbprint
$state.DnsNames = $DnsNames
$state.IpAddresses = $IpAddresses
$state.RootSha256 = $rootSha256
$state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
Export-Certificate -Cert $serverCertificate `
    -FilePath (Join-Path $outputPath 'AtariGo-Server.cer') -Force | Out-Null

# The certificate is selected from Windows; no private key is written to a file.
$projectPath = Join-Path $repositoryPath 'AtariGo.Server/AtariGo.Server.csproj'
$certificateSettings = @{
    'Subject' = $serverCertificate.Subject.Substring('CN='.Length)
    'Store' = 'My'
    'Location' = 'CurrentUser'
    'AllowInvalid' = 'false'
}
foreach ($setting in $certificateSettings.GetEnumerator()) {
    & dotnet user-secrets set ('Kestrel:Certificates:Default:' + $setting.Key) `
        $setting.Value --project $projectPath
    if ($LASTEXITCODE -ne 0) {
        throw [InvalidOperationException]::new('Unable to configure the server certificate.')
    }
}

Write-Output ('Public CA certificate: ' + $rootPublicPath)
Write-Output ('Verify this SHA-256 on each client: ' + $rootSha256)
Write-Output ('Server certificate expires: ' + $serverCertificate.NotAfter.ToString('O'))
