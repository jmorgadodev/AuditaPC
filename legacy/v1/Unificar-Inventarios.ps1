#requires -Version 5.1
<#
Combina los CSV generados por Inventario-Equipo.ps1.
Crea Inventario_Maestro.csv (ultima captura por equipo),
Inventario_Historico.csv (todas las capturas) y
Software_Maestro.csv (software de la ultima captura).
#>
param([string]$Carpeta)

Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()

if ([string]::IsNullOrWhiteSpace($Carpeta)) {
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Selecciona la carpeta que contiene los archivos INV_ y SOFT_ de todos los equipos'
    $dialog.SelectedPath = [Environment]::GetFolderPath('MyDocuments')
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 0 }
    $Carpeta = $dialog.SelectedPath
}

if (-not (Test-Path -LiteralPath $Carpeta -PathType Container)) {
    [System.Windows.Forms.MessageBox]::Show('La carpeta indicada no existe.', 'Error', 'OK', 'Error') | Out-Null
    exit 1
}

try {
    $inventoryFiles = @(Get-ChildItem -LiteralPath $Carpeta -Filter 'INV_*.csv' -File)
    if ($inventoryFiles.Count -eq 0) { throw 'No se encontraron archivos INV_*.csv en esa carpeta.' }

    $inventory = @(
        foreach ($file in $inventoryFiles) {
            Import-Csv -LiteralPath $file.FullName -Delimiter ';' -Encoding UTF8 -ErrorAction Stop
        }
    )
    if ($inventory.Count -eq 0) { throw 'Los archivos de inventario estan vacios.' }

    $latest = @(
        $inventory | Group-Object -Property {
            $serial = [string]$_.NumeroSerieBIOS
            $hostName = [string]$_.NombreEquipo
            $assetCode = [string]$_.CodigoInventario
            if (-not [string]::IsNullOrWhiteSpace($assetCode)) {
                'ASSET:' + $assetCode.Trim().ToUpperInvariant()
            } elseif (-not [string]::IsNullOrWhiteSpace($serial) -and
                $serial -notmatch '(?i)^\s*(to be filled.*|default string|system serial.*|serial number|not specified|not applicable|none|unknown|n/?a|0+|1234567890)\s*$') {
                'SERIAL:' + $serial.Trim().ToUpperInvariant()
            } else {
                'HOST:' + $hostName.Trim().ToUpperInvariant()
            }
        } | ForEach-Object {
            $_.Group | Sort-Object -Property @{ Expression = {
                try { [datetimeoffset]::Parse($_.FechaInventario) } catch { [datetimeoffset]::MinValue }
            }} -Descending | Select-Object -First 1
        }
    )

    $masterFile = Join-Path $Carpeta 'Inventario_Maestro.csv'
    $historyFile = Join-Path $Carpeta 'Inventario_Historico.csv'
    $softwareMasterFile = Join-Path $Carpeta 'Software_Maestro.csv'
    $inventory | Export-Csv -LiteralPath $historyFile -Delimiter ';' -NoTypeInformation -Encoding UTF8 -ErrorAction Stop
    $latest | Sort-Object NombreEquipo | Export-Csv -LiteralPath $masterFile -Delimiter ';' -NoTypeInformation -Encoding UTF8 -ErrorAction Stop

    $softwareFiles = @(Get-ChildItem -LiteralPath $Carpeta -Filter 'SOFT_*.csv' -File)
    $allowedCaptures = @{}
    foreach ($row in $latest) { if ($row.IdCaptura) { $allowedCaptures[[string]$row.IdCaptura] = $true } }
    $latestSoftware = @(
        foreach ($file in $softwareFiles) {
            $rows = @(Import-Csv -LiteralPath $file.FullName -Delimiter ';' -Encoding UTF8 -ErrorAction Stop)
            foreach ($row in $rows) {
                if ($allowedCaptures.ContainsKey([string]$row.IdCaptura) -and -not [string]::IsNullOrWhiteSpace($row.Programa)) {
                    $row
                }
            }
        }
    )
    if ($latestSoftware.Count -gt 0) {
        $latestSoftware | Sort-Object NombreEquipo, Programa | Export-Csv -LiteralPath $softwareMasterFile -Delimiter ';' -NoTypeInformation -Encoding UTF8 -ErrorAction Stop
    } else {
        'IdCaptura;FechaInventario;NombreEquipo;CodigoInventario;Programa;Version;Fabricante;FechaInstalacion;RutaInstalacion' |
            Set-Content -LiteralPath $softwareMasterFile -Encoding UTF8 -ErrorAction Stop
    }

    $message = "Inventarios unificados correctamente.`n`nEquipos unicos: $($latest.Count)`nCapturas totales: $($inventory.Count)`nProgramas en capturas vigentes: $($latestSoftware.Count)`n`nArchivos:`n$masterFile`n$historyFile`n$softwareMasterFile"
    [System.Windows.Forms.MessageBox]::Show($message, 'Inventario consolidado', 'OK', 'Information') | Out-Null
    Write-Host $message
} catch {
    [System.Windows.Forms.MessageBox]::Show(('No se pudo unificar: ' + $_.Exception.Message), 'Error', 'OK', 'Error') | Out-Null
    Write-Error $_
    exit 1
}
