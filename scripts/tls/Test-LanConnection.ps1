#Requires -Version 7.0

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [uri] $ServerAddress
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($ServerAddress.Scheme -ne 'https') {
    throw [ArgumentException]::new('The server address must use HTTPS.')
}

$connectionTimeoutSeconds = 10
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds($connectionTimeoutSeconds)
$request = [System.Net.Http.HttpRequestMessage]::new(
    [System.Net.Http.HttpMethod]::Get, $ServerAddress)
$request.Version = [Version]::new(2, 0)
$request.VersionPolicy = [System.Net.Http.HttpVersionPolicy]::RequestVersionExact
$response = $null

try {
    # Standard certificate and hostname validation remain enabled.
    $response = $client.SendAsync($request).GetAwaiter().GetResult()
    $response.EnsureSuccessStatusCode() | Out-Null
    Write-Output ('HTTPS connection validated. HTTP version: ' + $response.Version)
    Write-Output 'This checks transport access, not login, database access, or game services.'
}
finally {
    if ($null -ne $response) {
        $response.Dispose()
    }
    $request.Dispose()
    $client.Dispose()
}
