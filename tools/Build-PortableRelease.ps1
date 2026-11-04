#Requires -Version 5.1
<#
.SYNOPSIS
  Empaqueta AuditaPC como carpeta portable y genera un checksum SHA-256.
.DESCRIPTION
  Copia solo el ejecutable publicado y las instrucciones de uso.
  No admite auditorías, capturas, fotografías, configuraciones ni informes.
#>
param(
    [string]$OutputDirectory = '',
    [string]$PublishDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'artifacts\releases'
}
if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    $PublishDirectory = Join-Path $root 'artifacts\publish\win-x64'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$PublishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)
$exe = Join-Path $PublishDirectory 'AuditaPC.exe'
$leeme = Join-Path $root 'packaging\LEEME.txt'
$license = Join-Path $root 'LICENSE'

if (-not (Test-Path $exe -PathType Leaf)) {
    throw "No se encontró el EXE publicado en $exe. Primero ejecuta dotnet publish."
}
if (-not (Test-Path $leeme -PathType Leaf)) {
    throw "Falta packaging/LEEME.txt."
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('AuditaPC-release-' + [guid]::NewGuid().ToString('N'))
$app = Join-Path $work 'AuditaPC'
$zip = Join-Path $OutputDirectory 'AuditaPC-win-x64-portable.zip'
$checksumPath = "$zip.sha256"

try {
    New-Item -ItemType Directory -Force -Path $app | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $app 'Auditorias') | Out-Null
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

    Copy-Item -LiteralPath $exe -Destination (Join-Path $app 'AuditaPC.exe')
    Copy-Item -LiteralPath $leeme -Destination (Join-Path $app 'LEEME.txt')
    if (Test-Path $license -PathType Leaf) {
        Copy-Item -LiteralPath $license -Destination (Join-Path $app 'LICENSE')
    }

    if (Test-Path $zip) {
        Remove-Item -LiteralPath $zip -Force
    }

    Compress-Archive -LiteralPath $app -DestinationPath $zip -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $files = @($entries | Where-Object { -not $_.EndsWith('/') })
        $expectedFiles = @('AuditaPC/AuditaPC.exe', 'AuditaPC/LEEME.txt')
        if (Test-Path $license -PathType Leaf) {
            $expectedFiles += 'AuditaPC/LICENSE'
        }
        if ($files.Count -ne $expectedFiles.Count) {
            throw "Paquete inseguro: se esperaban $($expectedFiles.Count) archivos, se encontraron $($files.Count)."
        }
        foreach ($expected in $expectedFiles) {
            if ($files -notcontains $expected) {
                throw "Paquete incompleto: falta $expected."
            }
        }
        if ($entries | Where-Object { $_ -match '(?i)(\.json|\.csv|\.html|\.pdb|\.config|\.bak)$' }) {
            throw 'Paquete inseguro: contiene datos de auditoría o depuración.'
        }
    }
    finally {
        $archive.Dispose()
    }

    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText(
        $checksumPath,
        "$hash  AuditaPC-win-x64-portable.zip" + [Environment]::NewLine,
        [System.Text.Encoding]::ASCII)
    Write-Output "Paquete portable: $zip"
    Write-Output "Checksum SHA256: $hash"
    Write-Output "Tamaño del ZIP: $([math]::Round((Get-Item $zip).Length / 1MB, 2)) MiB"
}
finally {
    if (Test-Path $work) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
