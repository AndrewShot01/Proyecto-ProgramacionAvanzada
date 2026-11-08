param(
    [Parameter(Mandatory=$true)][string]$Servidor,
    [Parameter(Mandatory=$true)][string]$Usuario,
    [string]$BaseDatos = 'advanced_programming',
    [int]$Puerto = 5432
)
$ErrorActionPreference = 'Stop'
$proyecto = Join-Path $PSScriptRoot '../src/MiPulperia.Web/MiPulperia.Web.csproj'
$clave = Read-Host 'Contraseña de PostgreSQL (no se mostrará)' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($clave)
$encodingAnterior = $OutputEncoding
try {
    # DbConnectionStringBuilder escapa comillas y punto y coma en los valores.
    $cadena = New-Object System.Data.Common.DbConnectionStringBuilder
    $cadena['Host'] = $Servidor
    $cadena['Port'] = $Puerto
    $cadena['Database'] = $BaseDatos
    $cadena['Username'] = $Usuario
    $cadena['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    $cadena['SSL Mode'] = 'VerifyFull'
    $cadena['Search Path'] = 'user_07'
    $cadena['Timeout'] = '15'
    # stdin evita exponer la contraseña en argumentos del proceso y en historial.
    $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    @{ 'ConnectionStrings:Pulperia' = $cadena.ConnectionString } | ConvertTo-Json -Compress | dotnet user-secrets set --project $proyecto
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la conexión.' }
    Write-Host 'Conexión guardada fuera del repositorio. Reinicia la aplicación.'
} finally {
    $OutputEncoding = $encodingAnterior
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    $cadena = $null
}
