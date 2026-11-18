param(
    [string]$Sender,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'

function Get-RecoverySecretPath
{
    $projectPath = Join-Path $PSScriptRoot '..\AtariGo.Server\AtariGo.Server.csproj'
    $project = [xml](Get-Content -LiteralPath $projectPath -Raw)
    $secretId = $project.Project.PropertyGroup.UserSecretsId
    if ([string]::IsNullOrWhiteSpace($secretId))
    {
        throw 'The server project does not define a UserSecretsId.'
    }
    return Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
}

function Protect-RecoverySecretFile
{
    param([string]$Path)

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    if (!(Test-Path -LiteralPath $Path))
    {
        [System.IO.File]::WriteAllText($Path, '{}')
    }
    $security = [System.Security.AccessControl.FileSecurity]::new()
    $security.SetAccessRuleProtection($true, $false)
    $currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    try
    {
        $owner = $currentIdentity.User
    }
    finally
    {
        $currentIdentity.Dispose()
    }
    $system = [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    foreach ($identity in @($owner, $system))
    {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $identity, 'FullControl', 'Allow')
        $security.AddAccessRule($rule)
    }
    [System.IO.FileSystemAclExtensions]::SetAccessControl(
        [System.IO.FileInfo]::new($Path), $security)
}

function Set-RecoveryPassword
{
    param([hashtable]$Settings)

    $password = Read-Host 'Contraseña de aplicación de Gmail (entrada oculta)' -AsSecureString
    $pointer = [IntPtr]::Zero
    try
    {
        $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
        $value = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        $value = $value.Replace(' ', '')
        if ([string]::IsNullOrWhiteSpace($value))
        {
            throw 'The application password cannot be empty.'
        }
        $Settings['Smtp:Password'] = $value
    }
    finally
    {
        if ($pointer -ne [IntPtr]::Zero)
        {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        }
        $password.Dispose()
        $value = $null
    }
}

if (![OperatingSystem]::IsWindows())
{
    throw 'Run this development configuration script with PowerShell 7 on Windows.'
}
if ([string]::IsNullOrWhiteSpace($Sender))
{
    $Sender = Read-Host 'Correo remitente de Gmail'
}
$senderAddress = [System.Net.Mail.MailAddress]::new($Sender).Address
$secretPath = Get-RecoverySecretPath
$settings = @{}
if (Test-Path -LiteralPath $secretPath)
{
    $settings = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json -AsHashtable
}
if ($settings['Smtp:Sender'] -ne $senderAddress)
{
    $settings.Remove('Smtp:Password')
}
$settings['Smtp:Host'] = 'smtp.gmail.com'
$settings['Smtp:Port'] = 465
$settings['Smtp:Sender'] = $senderAddress
$settings['Smtp:UserName'] = $senderAddress
if ([string]::IsNullOrWhiteSpace($settings['PasswordRecovery:HmacKey']))
{
    $randomBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64)
    $settings['PasswordRecovery:HmacKey'] = [Convert]::ToBase64String($randomBytes)
    [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($randomBytes)
}
if (!$PrepareOnly)
{
    Set-RecoveryPassword -Settings $settings
}
Protect-RecoverySecretFile -Path $secretPath
try
{
    $json = ConvertTo-Json -InputObject $settings -Depth 10
    [System.IO.File]::WriteAllText($secretPath, $json)
}
finally
{
    $settings.Clear()
    $json = $null
}
if ($PrepareOnly)
{
    Write-Host 'Remitente y clave HMAC preparados. Falta configurar la contraseña de aplicación.'
}
else
{
    Write-Host 'Configuración local guardada. Reinicia el servidor para aplicar los cambios.'
}
