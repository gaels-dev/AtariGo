#Requires -Version 7.0

[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [securestring] $ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot '../../AtariGo.Server/AtariGo.Server.csproj'
$credential = [System.Net.NetworkCredential]::new('', $ConnectionString)
$settings = @{}
try {
    if ([string]::IsNullOrWhiteSpace($credential.Password)) {
        throw [ArgumentException]::new('A non-empty SQL connection string is required.')
    }

    $settings['ConnectionStrings:AtariGoConnection'] = $credential.Password
    $settings | ConvertTo-Json -Compress | & dotnet user-secrets set --project $projectPath
    if ($LASTEXITCODE -ne 0) {
        throw [InvalidOperationException]::new('Unable to save the database configuration.')
    }

    Write-Output 'Database settings saved for Development. Restart the server to apply them.'
}
finally {
    $settings.Clear()
    $credential.Password = ''
}
