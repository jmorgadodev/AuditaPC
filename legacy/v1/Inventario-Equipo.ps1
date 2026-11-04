#requires -Version 5.1
<#
Inventario de equipos Windows (v1.0)
- Interfaz local Windows Forms
- Recopila hardware, Windows, red, seguridad y software instalado
- Exporta CSV resumen (una fila/equipo), CSV software y JSON detallado
- No necesita modulos adicionales ni envia datos a Internet
#>

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

function Get-CimSafe {
    param([string]$ClassName, [string]$Namespace = 'root/cimv2', [string]$Filter = '')
    try {
        if ($Filter) {
            return @(Get-CimInstance -Namespace $Namespace -ClassName $ClassName -Filter $Filter -ErrorAction Stop)
        }
        return @(Get-CimInstance -Namespace $Namespace -ClassName $ClassName -ErrorAction Stop)
    } catch {
        return @()
    }
}

function Format-GB {
    param($Bytes)
    if ($null -eq $Bytes -or [string]$Bytes -eq '') { return $null }
    try { return [math]::Round(([double]$Bytes / 1GB), 2) } catch { return $null }
}

function Format-Date {
    param($DateValue)
    if ($null -eq $DateValue -or [string]$DateValue -eq '') { return '' }
    try { return ([datetime]$DateValue).ToString('yyyy-MM-dd HH:mm:ss') } catch { return [string]$DateValue }
}

function Join-Values {
    param($Values)
    return (@($Values | Where-Object { $null -ne $_ -and [string]$_ -ne '' }) -join ', ')
}

function Decode-MonitorText {
    param($Codes)
    if ($null -eq $Codes) { return '' }
    try {
        $characters = @($Codes | Where-Object { $_ -gt 0 } | ForEach-Object { [char][int]$_ })
        return [string]::Join('', [string[]]$characters)
    } catch { return '' }
}

function Get-ChassisType {
    param($Codes)
    $codesArray = @($Codes)
    if (@($codesArray | Where-Object { $_ -in @(8,9,10,11,12,14,18,21) }).Count -gt 0) { return 'Portatil' }
    if (@($codesArray | Where-Object { $_ -in @(30,31,32) }).Count -gt 0) { return 'Tablet / convertible' }
    if (@($codesArray | Where-Object { $_ -in @(17,23,28) }).Count -gt 0) { return 'Servidor / rack' }
    if (@($codesArray | Where-Object { $_ -in @(3,4,5,6,7,13,15,16,24,35,36) }).Count -gt 0) { return 'Escritorio' }
    return 'No determinado'
}

function Get-InstalledPrograms {
    $paths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )
    $seen = @{}
    $programs = New-Object System.Collections.Generic.List[object]
    foreach ($path in $paths) {
        $items = @(Get-ItemProperty -Path $path -ErrorAction SilentlyContinue)
        foreach ($item in $items) {
            $name = [string]$item.DisplayName
            if ([string]::IsNullOrWhiteSpace($name) -or $item.SystemComponent -eq 1) { continue }
            $version = [string]$item.DisplayVersion
            $publisher = [string]$item.Publisher
            $key = ($name.Trim() + '|' + $version.Trim() + '|' + $publisher.Trim())
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true
            $programs.Add([pscustomobject][ordered]@{
                Programa         = $name.Trim()
                Version          = $version
                Fabricante       = $publisher
                FechaInstalacion = [string]$item.InstallDate
                RutaInstalacion  = [string]$item.InstallLocation
            })
        }
    }
    return @($programs | Sort-Object Programa, Version)
}

function Get-Inventory {
    $computer  = @(Get-CimSafe 'Win32_ComputerSystem' | Select-Object -First 1)
    $bios      = @(Get-CimSafe 'Win32_BIOS' | Select-Object -First 1)
    $os        = @(Get-CimSafe 'Win32_OperatingSystem' | Select-Object -First 1)
    $board     = @(Get-CimSafe 'Win32_BaseBoard' | Select-Object -First 1)
    $enclosure = @(Get-CimSafe 'Win32_SystemEnclosure' | Select-Object -First 1)
    $processors = @(Get-CimSafe 'Win32_Processor')
    $memory     = @(Get-CimSafe 'Win32_PhysicalMemory')
    $memoryInfo = @(Get-CimSafe 'Win32_PhysicalMemoryArray' | Select-Object -First 1)
    $diskDevices = @(Get-CimSafe 'Win32_DiskDrive')
    $volumes = @(Get-CimSafe 'Win32_LogicalDisk' -Filter 'DriveType = 3')
    $nics = @(Get-CimSafe 'Win32_NetworkAdapterConfiguration' -Filter 'IPEnabled = TRUE')
    $video = @(Get-CimSafe 'Win32_VideoController')
    $monitorIds = @(Get-CimSafe 'WmiMonitorID' 'root/wmi')
    $batteries = @(Get-CimSafe 'Win32_Battery')
    $software = @(Get-InstalledPrograms)
    $avProducts = @(Get-CimSafe 'AntiVirusProduct' 'root/SecurityCenter2')

    $device = if ($computer.Count -gt 0) { $computer[0] } else { $null }
    $firmware = if ($bios.Count -gt 0) { $bios[0] } else { $null }
    $system = if ($os.Count -gt 0) { $os[0] } else { $null }
    $baseboard = if ($board.Count -gt 0) { $board[0] } else { $null }
    $case = if ($enclosure.Count -gt 0) { $enclosure[0] } else { $null }
    $memoryArray = if ($memoryInfo.Count -gt 0) { $memoryInfo[0] } else { $null }

    $ramModules = @(
        foreach ($module in $memory) {
            [pscustomobject][ordered]@{
                Ranura       = [string]$module.DeviceLocator
                CapacidadGB  = Format-GB $module.Capacity
                VelocidadMHz = $module.Speed
                Fabricante   = [string]$module.Manufacturer
                Modelo       = [string]$module.PartNumber
                NumeroSerie  = [string]$module.SerialNumber
            }
        }
    )
    $physicalDisks = @(
        foreach ($disk in $diskDevices) {
            [pscustomobject][ordered]@{
                Modelo      = [string]$disk.Model
                NumeroSerie = [string]$disk.SerialNumber
                CapacidadGB = Format-GB $disk.Size
                Interfaz    = [string]$disk.InterfaceType
                TipoMedio   = [string]$disk.MediaType
                IdDisco     = [string]$disk.DeviceID
            }
        }
    )
    $volumeDetails = @(
        foreach ($vol in $volumes) {
            [pscustomobject][ordered]@{
                Unidad       = [string]$vol.DeviceID
                Etiqueta     = [string]$vol.VolumeName
                Sistema      = [string]$vol.FileSystem
                CapacidadGB  = Format-GB $vol.Size
                LibreGB      = Format-GB $vol.FreeSpace
                LibrePorc    = if ([double]$vol.Size -gt 0) { [math]::Round(([double]$vol.FreeSpace / [double]$vol.Size) * 100, 1) } else { $null }
            }
        }
    )
    $networkDetails = @(
        foreach ($nic in $nics) {
            $ip4 = @($nic.IPAddress | Where-Object { $_ -match '^\d{1,3}(\.\d{1,3}){3}$' })
            $ip6 = @($nic.IPAddress | Where-Object { $_ -notmatch '^\d{1,3}(\.\d{1,3}){3}$' })
            [pscustomobject][ordered]@{
                Adaptador  = [string]$nic.Description
                MAC        = [string]$nic.MACAddress
                IPv4       = Join-Values $ip4
                IPv6       = Join-Values $ip6
                Gateway    = Join-Values $nic.DefaultIPGateway
                DNS        = Join-Values $nic.DNSServerSearchOrder
                DHCP       = [string]$nic.DHCPEnabled
            }
        }
    )
    $graphics = @(
        foreach ($gpu in $video) {
            [pscustomobject][ordered]@{
                Adaptador      = [string]$gpu.Name
                MemoriaGB      = Format-GB $gpu.AdapterRAM
                VersionDriver  = [string]$gpu.DriverVersion
                Resolucion     = if ($gpu.CurrentHorizontalResolution -and $gpu.CurrentVerticalResolution) { '{0} x {1}' -f $gpu.CurrentHorizontalResolution, $gpu.CurrentVerticalResolution } else { '' }
            }
        }
    )
    $monitors = @(
        foreach ($monitor in $monitorIds) {
            [pscustomobject][ordered]@{
                Modelo       = Decode-MonitorText $monitor.UserFriendlyName
                Fabricante   = Decode-MonitorText $monitor.ManufacturerName
                NumeroSerie  = Decode-MonitorText $monitor.SerialNumberID
                Anio         = $monitor.YearOfManufacture
                Identificador = [string]$monitor.InstanceName
            }
        }
    )
    $batteryDetails = @(
        foreach ($battery in $batteries) {
            [pscustomobject][ordered]@{
                Nombre         = [string]$battery.Name
                CargaEstimada  = $battery.EstimatedChargeRemaining
                Estado         = [string]$battery.BatteryStatus
            }
        }
    )

    $tpmStatus = 'No disponible / sin permisos'
    if (Get-Command Get-Tpm -ErrorAction SilentlyContinue) {
        try {
            $tpm = Get-Tpm -ErrorAction Stop
            $tpmStatus = if ($tpm.TpmPresent) { if ($tpm.TpmReady) { 'Presente y listo' } else { 'Presente, no listo' } } else { 'No presente' }
        } catch {}
    }
    $secureBoot = 'No disponible / sin permisos'
    if (Get-Command Confirm-SecureBootUEFI -ErrorAction SilentlyContinue) {
        try { $secureBoot = if (Confirm-SecureBootUEFI -ErrorAction Stop) { 'Activado' } else { 'Desactivado' } } catch {}
    }
    $bitlocker = @()
    if (Get-Command Get-BitLockerVolume -ErrorAction SilentlyContinue) {
        try {
            $bitlocker = @(Get-BitLockerVolume -ErrorAction Stop | ForEach-Object {
                [pscustomobject]@{ Unidad = [string]$_.MountPoint; Proteccion = [string]$_.ProtectionStatus; Cifrado = [string]$_.VolumeStatus }
            })
        } catch {}
    }
    $windowsActivation = 'No consultado / no disponible'
    try {
        $licensing = @(Get-CimSafe 'SoftwareLicensingProduct' 'root/cimv2' "ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL")
        if ($licensing.Count -gt 0) {
            $windowsActivation = if (@($licensing | Where-Object { $_.LicenseStatus -eq 1 }).Count -gt 0) { 'Licenciado' } else { 'No licenciado / revisar' }
        }
    } catch {}

    $officeNames = @($software | Where-Object { $_.Programa -match 'Microsoft 365 Apps|Microsoft Office|Office LTSC|Microsoft Visio|Microsoft Project' } | Select-Object -ExpandProperty Programa -Unique)
    $antivirusNames = @($avProducts | Select-Object -ExpandProperty displayName -Unique)
    $allIps = @($networkDetails | ForEach-Object { $_.IPv4 } | Where-Object { $_ })
    $allMacs = @($networkDetails | ForEach-Object { $_.MAC } | Where-Object { $_ })
    $diskText = @($physicalDisks | ForEach-Object { '{0} ({1} GB)' -f $_.Modelo, $_.CapacidadGB })
    $cpuNames = @($processors | ForEach-Object { $_.Name.Trim() } | Select-Object -Unique)
    $chassis = Get-ChassisType $case.ChassisTypes
    $summary = [pscustomobject][ordered]@{
        NombreEquipo        = if ($device.Name) { [string]$device.Name } else { $env:COMPUTERNAME }
        UsuarioActivo       = [string]$device.UserName
        DominioGrupo        = [string]$device.Domain
        TipoEquipo          = $chassis
        Fabricante          = [string]$device.Manufacturer
        Modelo              = [string]$device.Model
        NumeroSerieBIOS     = [string]$firmware.SerialNumber
        CodigoActivoBIOS    = [string]$case.SMBIOSAssetTag
        PlacaBase           = ('{0} {1}' -f $baseboard.Manufacturer, $baseboard.Product).Trim()
        SeriePlacaBase      = [string]$baseboard.SerialNumber
        Procesador          = ($cpuNames -join ' | ')
        Nucleos            = ($processors | Measure-Object -Property NumberOfCores -Sum).Sum
        Hilos              = ($processors | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
        RAMTotalGB         = Format-GB $device.TotalPhysicalMemory
        RanurasRAM         = $memoryArray.MemoryDevices
        SistemaOperativo   = [string]$system.Caption
        VersionWindows     = [string]$system.Version
        BuildWindows       = [string]$system.BuildNumber
        Arquitectura       = [string]$system.OSArchitecture
        InstalacionWindows = Format-Date $system.InstallDate
        UltimoInicio       = Format-Date $system.LastBootUpTime
        BIOSVersion        = [string]$firmware.SMBIOSBIOSVersion
        BIOSFecha          = Format-Date $firmware.ReleaseDate
        Discos             = ($diskText -join ' | ')
        CapacidadDiscosGB  = [math]::Round((@($physicalDisks | Measure-Object -Property CapacidadGB -Sum)[0].Sum), 2)
        IPv4               = ($allIps -join ' | ')
        MAC                = ($allMacs -join ' | ')
        OfficeDetectado    = ($officeNames -join ' | ')
        ActivacionWindows  = $windowsActivation
        TPM                = $tpmStatus
        SecureBoot         = $secureBoot
        Antivirus          = ($antivirusNames -join ' | ')
    }

    return [pscustomobject][ordered]@{
        VersionEsquema  = '1.0'
        FechaCaptura    = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        Resumen         = $summary
        MemoriaRAM      = $ramModules
        DiscosFisicos   = $physicalDisks
        Volumenes       = $volumeDetails
        Redes           = $networkDetails
        Graficos        = $graphics
        Monitores       = $monitors
        Baterias        = $batteryDetails
        Software        = $software
        Seguridad       = [pscustomobject][ordered]@{
            ActivacionWindows = $windowsActivation
            TPM               = $tpmStatus
            SecureBoot        = $secureBoot
            BitLocker         = $bitlocker
            Antivirus         = $antivirusNames
        }
    }
}

function New-GridTable {
    param([object[]]$Rows, [string[]]$Columns)
    $table = New-Object System.Data.DataTable
    foreach ($column in $Columns) { [void]$table.Columns.Add($column, [string]) }
    foreach ($item in @($Rows)) {
        if ($null -eq $item) { continue }
        $record = $table.NewRow()
        foreach ($column in $Columns) {
            $value = $null
            if ($item -is [System.Collections.IDictionary]) {
                $value = $item[$column]
            } elseif ($item.PSObject.Properties[$column]) {
                $value = $item.PSObject.Properties[$column].Value
            }
            $record[$column] = if ($null -eq $value) { '' } else { [string]$value }
        }
        [void]$table.Rows.Add($record)
    }
    return ,$table
}

function Set-GridTable {
    param([string]$Name, [object[]]$Rows, [string[]]$Columns)
    $grid = $script:Grids[$Name]
    $grid.DataSource = New-GridTable -Rows $Rows -Columns $Columns
    if ($grid.Columns.Count -gt 0) {
        $grid.Columns[0].FillWeight = if ($Columns.Count -eq 2) { 45 } else { 120 }
        if ($Columns.Count -eq 2) { $grid.Columns[1].FillWeight = 55 }
    }
}

function Update-Views {
    param($Inventory)
    $summaryRows = @(
        foreach ($property in $Inventory.Resumen.PSObject.Properties) {
            [pscustomobject]@{ Campo = $property.Name; Valor = [string]$property.Value }
        }
        [pscustomobject]@{ Campo = 'Programas instalados (registro)'; Valor = [string]@($Inventory.Software).Count }
    )
    Set-GridTable 'Resumen' $summaryRows @('Campo','Valor')
    Set-GridTable 'RAM' $Inventory.MemoriaRAM @('Ranura','CapacidadGB','VelocidadMHz','Fabricante','Modelo','NumeroSerie')
    Set-GridTable 'Discos' $Inventory.DiscosFisicos @('Modelo','NumeroSerie','CapacidadGB','Interfaz','TipoMedio','IdDisco')
    Set-GridTable 'Unidades' $Inventory.Volumenes @('Unidad','Etiqueta','Sistema','CapacidadGB','LibreGB','LibrePorc')
    Set-GridTable 'Redes' $Inventory.Redes @('Adaptador','MAC','IPv4','IPv6','Gateway','DNS','DHCP')
    Set-GridTable 'Graficos' $Inventory.Graficos @('Adaptador','MemoriaGB','VersionDriver','Resolucion')
    Set-GridTable 'Monitores' $Inventory.Monitores @('Modelo','Fabricante','NumeroSerie','Anio','Identificador')
    Set-GridTable 'Baterias' $Inventory.Baterias @('Nombre','CargaEstimada','Estado')
    Set-GridTable 'Software' $Inventory.Software @('Programa','Version','Fabricante','FechaInstalacion','RutaInstalacion')
    $securityRows = @(
        [pscustomobject]@{ Campo='Activacion Windows'; Valor=$Inventory.Seguridad.ActivacionWindows }
        [pscustomobject]@{ Campo='TPM'; Valor=$Inventory.Seguridad.TPM }
        [pscustomobject]@{ Campo='Secure Boot'; Valor=$Inventory.Seguridad.SecureBoot }
        [pscustomobject]@{ Campo='Antivirus detectados'; Valor=($Inventory.Seguridad.Antivirus -join ' | ') }
        foreach ($item in $Inventory.Seguridad.BitLocker) {
            [pscustomobject]@{ Campo=('BitLocker ' + $item.Unidad); Valor=($item.Proteccion + ' / ' + $item.Cifrado) }
        }
    )
    Set-GridTable 'Seguridad' $securityRows @('Campo','Valor')
}

function Get-SafeFileName {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return 'EQUIPO' }
    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    foreach ($char in $invalid) { $Text = $Text.Replace([string]$char, '_') }
    return $Text.Replace(' ', '_')
}

function Export-Inventory {
    if ($null -eq $script:CurrentInventory) { return }
    $folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $folderDialog.Description = 'Selecciona la carpeta donde guardar los archivos del equipo'
    $folderDialog.SelectedPath = [Environment]::GetFolderPath('MyDocuments')
    if ($folderDialog.ShowDialog($script:Form) -ne [System.Windows.Forms.DialogResult]::OK) { return }

    $inventory = $script:CurrentInventory
    $folder = $folderDialog.SelectedPath
    $captureId = [guid]::NewGuid().ToString('N')
    $captureDate = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
    $base = (Get-SafeFileName $inventory.Resumen.NombreEquipo) + '_' + $stamp
    $inventoryFile = Join-Path $folder ('INV_' + $base + '.csv')
    $softwareFile  = Join-Path $folder ('SOFT_' + $base + '.csv')
    $detailFile    = Join-Path $folder ('DET_' + $base + '.json')

    $administrative = [pscustomobject][ordered]@{
        CodigoInventario = $script:txtAsset.Text.Trim()
        Responsable      = $script:txtOwner.Text.Trim()
        UbicacionArea    = $script:txtLocation.Text.Trim()
        Observaciones    = $script:txtNotes.Text.Trim()
    }

    $csvData = [ordered]@{
        VersionEsquema  = $inventory.VersionEsquema
        IdCaptura       = $captureId
        FechaInventario = $captureDate
        CodigoInventario = $administrative.CodigoInventario
        Responsable      = $administrative.Responsable
        UbicacionArea    = $administrative.UbicacionArea
        Observaciones    = $administrative.Observaciones
    }
    foreach ($property in $inventory.Resumen.PSObject.Properties) { $csvData[$property.Name] = $property.Value }
    $csvRow = [pscustomobject]$csvData

    $softwareRows = @(
        foreach ($program in $inventory.Software) {
            [pscustomobject][ordered]@{
                IdCaptura       = $captureId
                FechaInventario = $captureDate
                NombreEquipo    = $inventory.Resumen.NombreEquipo
                CodigoInventario = $administrative.CodigoInventario
                Programa         = $program.Programa
                Version          = $program.Version
                Fabricante       = $program.Fabricante
                FechaInstalacion = $program.FechaInstalacion
                RutaInstalacion  = $program.RutaInstalacion
            }
        }
    )
    $fullRecord = [pscustomobject][ordered]@{
        VersionEsquema   = $inventory.VersionEsquema
        IdCaptura        = $captureId
        FechaInventario  = $captureDate
        Administrativos  = $administrative
        DatosEquipo      = $inventory
    }

    try {
        $csvRow | Export-Csv -Path $inventoryFile -Delimiter ';' -NoTypeInformation -Encoding UTF8 -ErrorAction Stop
        if ($softwareRows.Count -gt 0) {
            $softwareRows | Export-Csv -Path $softwareFile -Delimiter ';' -NoTypeInformation -Encoding UTF8 -ErrorAction Stop
        } else {
            'IdCaptura;FechaInventario;NombreEquipo;CodigoInventario;Programa;Version;Fabricante;FechaInstalacion;RutaInstalacion' |
                Set-Content -Path $softwareFile -Encoding UTF8 -ErrorAction Stop
        }
        $fullRecord | ConvertTo-Json -Depth 12 | Set-Content -Path $detailFile -Encoding UTF8 -ErrorAction Stop
        $script:lblStatus.Text = 'Exportacion completada: ' + $folder
        [System.Windows.Forms.MessageBox]::Show(
            "Inventario exportado correctamente.`n`nArchivos creados:`n$inventoryFile`n$softwareFile`n$detailFile`n`nCopia los archivos INV_ y SOFT_ de todos los equipos a una carpeta y ejecuta Unificar-Inventarios.bat.",
            'Exportacion correcta', 'OK', 'Information') | Out-Null
    } catch {
        [System.Windows.Forms.MessageBox]::Show(('No se pudo exportar: ' + $_.Exception.Message), 'Error', 'OK', 'Error') | Out-Null
    }
}

function Refresh-Inventory {
    $script:btnRefresh.Enabled = $false
    $script:btnExport.Enabled = $false
    $script:lblStatus.Text = 'Leyendo los datos locales del equipo...'
    $script:Form.Cursor = [System.Windows.Forms.Cursors]::WaitCursor
    [System.Windows.Forms.Application]::DoEvents()
    try {
        $script:CurrentInventory = Get-Inventory
        Update-Views $script:CurrentInventory
        $script:lblStatus.Text = ('Listo: {0} | {1} programas detectados | Capturado {2}' -f $script:CurrentInventory.Resumen.NombreEquipo, @($script:CurrentInventory.Software).Count, (Get-Date -Format 'HH:mm:ss'))
        $script:btnExport.Enabled = $true
    } catch {
        [System.Windows.Forms.MessageBox]::Show(('Error al leer el inventario: ' + $_.Exception.Message), 'Error', 'OK', 'Error') | Out-Null
        $script:lblStatus.Text = 'No se pudo completar el inventario'
    } finally {
        $script:Form.Cursor = [System.Windows.Forms.Cursors]::Default
        $script:btnRefresh.Enabled = $true
    }
}

$script:CurrentInventory = $null
$script:Grids = @{}
$script:Form = New-Object System.Windows.Forms.Form
$script:Form.Text = 'Inventario TI - Windows  |  v1.0'
$script:Form.StartPosition = 'CenterScreen'
$script:Form.ClientSize = [System.Drawing.Size]::new(1180, 730)
$script:Form.MinimumSize = [System.Drawing.Size]::new(1196, 650)
$script:Form.Font = [System.Drawing.Font]::new('Segoe UI', 9)

$root = New-Object System.Windows.Forms.TableLayoutPanel
$root.Dock = 'Fill'
$root.ColumnCount = 1
$root.RowCount = 3
[void]$root.RowStyles.Add([System.Windows.Forms.RowStyle]::new([System.Windows.Forms.SizeType]::Absolute, 120))
[void]$root.RowStyles.Add([System.Windows.Forms.RowStyle]::new([System.Windows.Forms.SizeType]::Percent, 100))
[void]$root.RowStyles.Add([System.Windows.Forms.RowStyle]::new([System.Windows.Forms.SizeType]::Absolute, 68))
$script:Form.Controls.Add($root)

$header = New-Object System.Windows.Forms.Panel
$header.Dock = 'Fill'
$header.BackColor = [System.Drawing.Color]::FromArgb(242, 246, 251)
$root.Controls.Add($header, 0, 0)
$heading = New-Object System.Windows.Forms.Label
$heading.Text = 'Inventario tecnico del equipo'
$heading.Font = [System.Drawing.Font]::new('Segoe UI', 15, [System.Drawing.FontStyle]::Bold)
$heading.AutoSize = $true
$heading.Location = [System.Drawing.Point]::new(16, 10)
$header.Controls.Add($heading)
$subheading = New-Object System.Windows.Forms.Label
$subheading.Text = 'Completa los campos administrativos opcionales y revisa las pestanas antes de exportar.'
$subheading.AutoSize = $true
$subheading.ForeColor = [System.Drawing.Color]::DimGray
$subheading.Location = [System.Drawing.Point]::new(18, 43)
$header.Controls.Add($subheading)

$fields = @(
    @{ Label='Codigo de inventario'; X=17; Name='txtAsset' },
    @{ Label='Responsable'; X=308; Name='txtOwner' },
    @{ Label='Ubicacion / area'; X=599; Name='txtLocation' },
    @{ Label='Observaciones'; X=890; Name='txtNotes' }
)
foreach ($field in $fields) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $field.Label
    $label.AutoSize = $true
    $label.Location = [System.Drawing.Point]::new($field.X, 69)
    $header.Controls.Add($label)
    $entry = New-Object System.Windows.Forms.TextBox
    $entry.Location = [System.Drawing.Point]::new($field.X, 88)
    $entry.Size = [System.Drawing.Size]::new(264, 24)
    $entry.Anchor = 'Top,Left'
    $header.Controls.Add($entry)
    Set-Variable -Scope Script -Name $field.Name -Value $entry
}

$tabs = New-Object System.Windows.Forms.TabControl
$tabs.Dock = 'Fill'
$tabs.Multiline = $true
$root.Controls.Add($tabs, 0, 1)
foreach ($tabName in @('Resumen','RAM','Discos','Unidades','Redes','Graficos','Monitores','Baterias','Software','Seguridad')) {
    $page = New-Object System.Windows.Forms.TabPage
    $page.Text = $tabName
    $grid = New-Object System.Windows.Forms.DataGridView
    $grid.Dock = 'Fill'
    $grid.ReadOnly = $true
    $grid.AllowUserToAddRows = $false
    $grid.AllowUserToDeleteRows = $false
    $grid.AllowUserToResizeRows = $false
    $grid.SelectionMode = 'FullRowSelect'
    $grid.MultiSelect = $false
    $grid.RowHeadersVisible = $false
    $grid.AutoSizeColumnsMode = 'Fill'
    $grid.BackgroundColor = [System.Drawing.Color]::White
    $grid.AlternatingRowsDefaultCellStyle.BackColor = [System.Drawing.Color]::FromArgb(246, 249, 252)
    $grid.ColumnHeadersHeightSizeMode = 'AutoSize'
    $page.Controls.Add($grid)
    [void]$tabs.TabPages.Add($page)
    $script:Grids[$tabName] = $grid
}

$footer = New-Object System.Windows.Forms.Panel
$footer.Dock = 'Fill'
$root.Controls.Add($footer, 0, 2)
$script:lblStatus = New-Object System.Windows.Forms.Label
$script:lblStatus.Text = 'Preparado para examinar el equipo'
$script:lblStatus.Location = [System.Drawing.Point]::new(15, 13)
$script:lblStatus.Size = [System.Drawing.Size]::new(680, 42)
$script:lblStatus.Anchor = 'Left,Right,Top'
$script:lblStatus.ForeColor = [System.Drawing.Color]::FromArgb(48, 60, 75)
$footer.Controls.Add($script:lblStatus)

$script:btnRefresh = New-Object System.Windows.Forms.Button
$script:btnRefresh.Text = 'Actualizar datos'
$script:btnRefresh.Size = [System.Drawing.Size]::new(137, 35)
$script:btnRefresh.Location = [System.Drawing.Point]::new(718, 14)
$script:btnRefresh.Anchor = 'Top,Right'
$script:btnRefresh.Add_Click({ Refresh-Inventory })
$footer.Controls.Add($script:btnRefresh)

$script:btnExport = New-Object System.Windows.Forms.Button
$script:btnExport.Text = 'Exportar inventario'
$script:btnExport.Size = [System.Drawing.Size]::new(168, 35)
$script:btnExport.Location = [System.Drawing.Point]::new(864, 14)
$script:btnExport.Anchor = 'Top,Right'
$script:btnExport.Enabled = $false
$script:btnExport.BackColor = [System.Drawing.Color]::FromArgb(20, 91, 167)
$script:btnExport.ForeColor = [System.Drawing.Color]::White
$script:btnExport.FlatStyle = 'Flat'
$script:btnExport.Add_Click({ Export-Inventory })
$footer.Controls.Add($script:btnExport)

$exitButton = New-Object System.Windows.Forms.Button
$exitButton.Text = 'Salir'
$exitButton.Size = [System.Drawing.Size]::new(103, 35)
$exitButton.Location = [System.Drawing.Point]::new(1042, 14)
$exitButton.Anchor = 'Top,Right'
$exitButton.Add_Click({ $script:Form.Close() })
$footer.Controls.Add($exitButton)

$script:Form.Add_Shown({ Refresh-Inventory })
[void]$script:Form.ShowDialog()
$script:Form.Dispose()
