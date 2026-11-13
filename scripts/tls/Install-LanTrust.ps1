#Requires -Version 7.0

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [string] $CertificatePath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string] $ExpectedSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw [PlatformNotSupportedException]::new('Windows certificate stores are required.')
}

$certificateFile = Get-Item -LiteralPath $CertificatePath
$actualSha256 = (Get-FileHash -LiteralPath $certificateFile.FullName -Algorithm SHA256).Hash
if ($actualSha256 -ne $ExpectedSha256) {
    throw [System.Security.SecurityException]::new('The certificate SHA-256 does not match.')
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $certificateFile.FullName)
try {
    $constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
    if ($certificate.HasPrivateKey -or $null -eq $constraints -or
        -not $constraints.CertificateAuthority) {
        throw [System.Security.SecurityException]::new('A public CA certificate is required.')
    }

    if ($certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
        throw [System.Security.SecurityException]::new(
            'The CA certificate is outside its validity.')
    }

    $trustedCertificatePath = 'Cert:\CurrentUser\Root\' + $certificate.Thumbprint
    if (-not (Test-Path -LiteralPath $trustedCertificatePath)) {
        Import-Certificate -FilePath $certificateFile.FullName `
            -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
    }

    Write-Output 'The AtariGo certificate authority is trusted for the current Windows user.'
}
finally {
    $certificate.Dispose()
}
