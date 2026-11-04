using AuditaPC.Core.Model;
using Microsoft.Win32;

namespace AuditaPC.Core.Inventory.Collectors;

public sealed class SecurityCollector : IInventoryCollector
{
    public string Id => "security";
    public string Title => "Seguridad de Windows";
    public TimeSpan Timeout => TimeSpan.FromSeconds(18);

    public Task<CollectorOutcome> CollectAsync(CollectorContext context)
    {
        var checks = new List<CheckRecord>();
        var data = new SecurityData();
        var obtained = false;

        obtained |= ReadAntivirus(data, checks);
        obtained |= ReadDefender(data, checks);
        obtained |= ReadFirewall(data, checks);
        obtained |= ReadBitLocker(data, checks);
        obtained |= ReadTpm(data, checks);
        obtained |= ReadSecureBoot(data, checks);

        checks.Add(CheckRecord.NotApplicable(
            "security.scope",
            "Modificación de la configuración",
            "AuditaPC solo consulta el estado. No cambia antivirus, firewall, BitLocker, TPM ni Secure Boot.",
            "Alcance de AuditaPC"));

        var result = new CategoryResult<SecurityData>
        {
            Id = Id,
            Title = Title,
            Data = data,
            Checks = checks,
            Status = CategoryStatus.Aggregate(checks, obtained)
        };

        return Task.FromResult(new CollectorOutcome
        {
            Id = Id,
            Status = result.Status,
            Apply = snapshot => snapshot.Security = result
        });
    }

    public void ApplyUnavailable(InventorySnapshot target, CheckStatus status, string message, string? errorType)
    {
        target.Security = CategoryResult<SecurityData>.Unavailable(Id, Title, status, message, errorType);
    }

    private static bool ReadAntivirus(SecurityData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "AntiVirusProduct",
                namespacePath: @"root\SecurityCenter2",
                timeout: TimeSpan.FromSeconds(8),
                selectColumns: "displayName, productState, pathToSignedProductExe");

            if (rows.Count == 0)
            {
                checks.Add(CheckRecord.NotDetected("security.av", "Antivirus",
                    "Security Center no informó productos antivirus.", "WMI SecurityCenter2"));
                return true;
            }

            foreach (var row in rows)
            {
                var state = row.GetInt("productState");
                var scanner = state is null ? (int?)null : (state.Value >> 8) & 0xFF;
                var signature = state is null ? (int?)null : state.Value & 0xFF;
                data.Antivirus.Add(new AntivirusInfo
                {
                    DisplayName = Clean(row.GetString("displayName")),
                    ProductStateRaw = state,
                    Enabled = scanner switch
                    {
                        null => null,
                        0xFF => null,
                        _ => (scanner.Value & 0x10) != 0
                    },
                    DefinitionsUpToDate = signature switch
                    {
                        0x00 => true,
                        0x10 => false,
                        _ => null
                    },
                    Provider = state is null ? null : $"0x{((state.Value >> 16) & 0xFF):X2}",
                    Source = "WMI SecurityCenter2",
                    Note = "Estado decodificado del campo productState; puede variar según el producto."
                });
            }

            var enabled = data.Antivirus.Count(product => product.Enabled == true);
            var disabled = data.Antivirus.Count(product => product.Enabled == false);
            if (enabled > 0)
            {
                checks.Add(CheckRecord.Verified("security.av", "Antivirus",
                    $"{enabled} producto(s) activo(s): " +
                    string.Join(" | ", data.Antivirus
                        .Where(product => product.Enabled == true)
                        .Select(product => product.DisplayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))),
                    "WMI SecurityCenter2"));
            }
            else if (disabled > 0)
            {
                checks.Add(CheckRecord.Problem("security.av", "Antivirus",
                    "Security Center detectó productos antivirus, pero ninguno está activo.",
                    "WMI SecurityCenter2"));
            }
            else
            {
                checks.Add(CheckRecord.Detected("security.av", "Antivirus",
                    "Security Center detectó productos, pero no se pudo interpretar si están activos.",
                    "WMI SecurityCenter2"));
            }

            return true;
        }
        catch (WmiQueryException ex)
        {
            checks.Add(CollectorErrors.FromWmi(
                "security.av", "Antivirus", ex, notFoundIsNotApplicable: true));
            return false;
        }
    }

    private static bool ReadDefender(SecurityData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "MSFT_MpComputerStatus",
                namespacePath: @"root\Microsoft\Windows\Defender",
                timeout: TimeSpan.FromSeconds(8),
                selectColumns: "AntivirusEnabled, RealTimeProtectionEnabled, " +
                               "AntivirusSignatureVersion, AntivirusSignatureAge, IsTamperProtected");

            if (rows.Count == 0)
            {
                checks.Add(CheckRecord.NotApplicable("security.defender", "Microsoft Defender",
                    "Microsoft Defender no está presente o no expone estado.", "WMI Defender"));
                return false;
            }

            var row = rows[0];
            data.Defender = new DefenderStatusInfo
            {
                AntivirusEnabled = row.GetBool("AntivirusEnabled"),
                RealTimeProtectionEnabled = row.GetBool("RealTimeProtectionEnabled"),
                SignatureVersion = Clean(row.GetString("AntivirusSignatureVersion")),
                SignatureAgeDays = row.GetInt("AntivirusSignatureAge"),
                TamperProtectionEnabled = row.GetBool("IsTamperProtected"),
                Source = "WMI MSFT_MpComputerStatus"
            };

            if (data.Defender.RealTimeProtectionEnabled == true)
            {
                checks.Add(CheckRecord.Verified("security.defender", "Microsoft Defender",
                    $"Protección en tiempo real activa · firmas {Formatting.OrDash(data.Defender.SignatureVersion)}" +
                    (data.Defender.SignatureAgeDays is { } age ? $" · antigüedad {age} día(s)" : string.Empty),
                    "WMI Defender"));
            }
            else if (data.Defender.RealTimeProtectionEnabled == false)
            {
                checks.Add(CheckRecord.Problem("security.defender", "Microsoft Defender",
                    "La protección en tiempo real está desactivada.",
                    "WMI Defender"));
            }
            else
            {
                checks.Add(CheckRecord.NotVerifiable("security.defender", "Microsoft Defender",
                    "Windows no informó el estado de la protección en tiempo real.",
                    "WMI Defender"));
            }

            return true;
        }
        catch (WmiQueryException ex)
        {
            checks.Add(CollectorErrors.FromWmi(
                "security.defender", "Microsoft Defender", ex, notFoundIsNotApplicable: true));
            return false;
        }
    }

    private static bool ReadFirewall(SecurityData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "MSFT_NetFirewallProfile",
                namespacePath: @"root\StandardCimv2",
                timeout: TimeSpan.FromSeconds(8),
                selectColumns: "Name, Enabled");

            if (rows.Count > 0)
            {
                foreach (var row in rows)
                {
                    var name = Clean(row.GetString("Name"));
                    var enabled = row.GetBool("Enabled")
                                  ?? (row.GetInt("Enabled") switch
                                  {
                                      1 => true,
                                      0 => false,
                                      _ => (bool?)null
                                  });
                    data.Firewall.Add(new FirewallProfileInfo
                    {
                        Profile = MapFirewallProfile(name),
                        Enabled = enabled,
                        Source = "WMI MSFT_NetFirewallProfile"
                    });
                }

                AddFirewallCheck(data, checks, "WMI MSFT_NetFirewallProfile");
                return true;
            }
        }
        catch (WmiQueryException)
        {
            // Se intenta el Registro como alternativa.
        }

        return ReadFirewallRegistry(data, checks);
    }

    private static bool ReadFirewallRegistry(SecurityData data, List<CheckRecord> checks)
    {
        var profiles = new (string Key, string Name)[]
        {
            ("DomainProfile", "Dominio"),
            ("StandardProfile", "Privada"),
            ("PublicProfile", "Pública")
        };

        var readAny = false;
        try
        {
            using var baseKey = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy",
                RegistryView.Registry64);
            if (baseKey is null)
            {
                checks.Add(CheckRecord.NotVerifiable("security.firewall", "Firewall",
                    "No se pudo leer la configuración del firewall.",
                    "Registro de Windows"));
                return false;
            }

            foreach (var (keyName, displayName) in profiles)
            {
                using var profileKey = baseKey.OpenSubKey(keyName, writable: false);
                if (profileKey is null)
                {
                    continue;
                }

                var rawEnabled = RegistryHelper.GetInt(profileKey, "EnableFirewall");
                bool? enabled = rawEnabled switch
                {
                    1 => true,
                    0 => false,
                    _ => null
                };
                data.Firewall.Add(new FirewallProfileInfo
                {
                    Profile = displayName,
                    Enabled = enabled,
                    Source = "Registro de Windows"
                });
                readAny = true;
            }

            if (readAny)
            {
                AddFirewallCheck(data, checks, "Registro de Windows");
            }
            else
            {
                checks.Add(CheckRecord.NotVerifiable("security.firewall", "Firewall",
                    "No se encontraron perfiles de firewall en el Registro.",
                    "Registro de Windows"));
            }
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry("security.firewall", "Firewall", ex));
        }

        return readAny;
    }

    private static void AddFirewallCheck(SecurityData data, List<CheckRecord> checks, string source)
    {
        var disabled = data.Firewall.Where(profile => profile.Enabled == false).ToList();
        var unknown = data.Firewall.Count(profile => profile.Enabled is null);
        var summary = string.Join(" · ", data.Firewall
            .Select(profile => $"{profile.Profile}: {(profile.Enabled == true ? "activo" : profile.Enabled == false ? "inactivo" : "sin datos")}"));

        if (disabled.Count > 0)
        {
            checks.Add(CheckRecord.Problem("security.firewall", "Firewall",
                "Perfiles inactivos: " + string.Join(", ", disabled.Select(profile => profile.Profile)) +
                ". " + summary, source));
        }
        else if (unknown > 0)
        {
            checks.Add(CheckRecord.NotVerifiable("security.firewall", "Firewall",
                "No se pudo interpretar el estado de algunos perfiles. " + summary, source));
        }
        else
        {
            checks.Add(CheckRecord.Verified("security.firewall", "Firewall",
                summary, source));
        }
    }

    private static bool ReadBitLocker(SecurityData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "Win32_EncryptableVolume",
                namespacePath: @"root\cimv2\security\microsoftvolumeencryption",
                timeout: TimeSpan.FromSeconds(9),
                selectColumns: "DriveLetter, ProtectionStatus, ConversionStatus, EncryptionMethod");

            if (rows.Count == 0)
            {
                checks.Add(CheckRecord.NotDetected("security.bitlocker", "BitLocker",
                    "No se encontraron volúmenes cifrables.",
                    "WMI Win32_EncryptableVolume"));
                return true;
            }

            foreach (var row in rows)
            {
                var protection = row.GetInt("ProtectionStatus");
                var conversion = row.GetInt("ConversionStatus");
                data.BitLocker.Add(new BitLockerVolumeInfo
                {
                    DriveLetter = Clean(row.GetString("DriveLetter")),
                    ProtectionStatus = protection,
                    ConversionStatus = conversion,
                    ProtectionText = MapProtectionStatus(protection),
                    ConversionText = MapConversionStatus(conversion),
                    EncryptionMethod = MapEncryptionMethod(row.GetInt("EncryptionMethod")),
                    Source = "WMI Win32_EncryptableVolume"
                });
            }

            var protectedVolumes = data.BitLocker.Count(volume => volume.ProtectionStatus == 1);
            checks.Add(CheckRecord.Detected("security.bitlocker", "BitLocker",
                $"{protectedVolumes} de {data.BitLocker.Count} volumen(es) con protección activa. " +
                "No se leen claves de recuperación.",
                "WMI Win32_EncryptableVolume"));
            return true;
        }
        catch (WmiQueryException ex)
        {
            checks.Add(CollectorErrors.FromWmi(
                "security.bitlocker", "BitLocker", ex, notFoundIsNotApplicable: true));
            return false;
        }
    }

    private static bool ReadTpm(SecurityData data, List<CheckRecord> checks)
    {
        try
        {
            var rows = WmiQuery.Query(
                "Win32_Tpm",
                namespacePath: @"root\cimv2\security\microsofttpm",
                timeout: TimeSpan.FromSeconds(8),
                selectColumns: "IsEnabled_InitialValue, IsActivated_InitialValue, SpecVersion, ManufacturerIdTxt");

            if (rows.Count == 0)
            {
                checks.Add(CheckRecord.NotVerifiable("security.tpm", "TPM",
                    "El proveedor WMI no devolvió datos. No es posible distinguir entre un TPM ausente " +
                    "y una consulta no disponible.",
                    "WMI Win32_Tpm"));
                return true;
            }

            var row = rows[0];
            data.Tpm = new TpmInfo
            {
                Present = true,
                Enabled = row.GetBool("IsEnabled_InitialValue"),
                Activated = row.GetBool("IsActivated_InitialValue"),
                Ready = null,
                SpecVersion = Clean(row.GetString("SpecVersion")),
                Manufacturer = Clean(row.GetString("ManufacturerIdTxt")),
                Source = "WMI Win32_Tpm"
            };

            // Win32_Tpm expone valores iniciales; no confirma por sí solo
            // el estado operativo actual ni que el TPM esté listo para uso.
            var detail = $"Presente · valor inicial habilitado: {Formatting.YesNo(data.Tpm.Enabled)} · " +
                         $"valor inicial activado: {Formatting.YesNo(data.Tpm.Activated)} · " +
                         $"especificación: {Formatting.OrDash(data.Tpm.SpecVersion)}. " +
                         "El proveedor no confirma el estado operativo actual.";
            checks.Add(CheckRecord.Detected("security.tpm", "TPM", detail, "WMI Win32_Tpm"));
            return true;
        }
        catch (WmiQueryException ex)
        {
            checks.Add(CollectorErrors.FromWmi(
                "security.tpm", "TPM", ex, notFoundIsNotApplicable: false));
            return false;
        }
    }

    private static bool ReadSecureBoot(SecurityData data, List<CheckRecord> checks)
    {
        var firmware = FirmwareInfo.GetFirmwareType();
        if (firmware?.StartsWith("BIOS", StringComparison.OrdinalIgnoreCase) == true)
        {
            checks.Add(CheckRecord.NotApplicable("security.secureboot", "Secure Boot",
                "El equipo usa BIOS heredado; Secure Boot no aplica.",
                "Firmware del sistema"));
            return true;
        }

        try
        {
            using var key = RegistryHelper.OpenKey(
                RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State",
                RegistryView.Registry64);
            var value = RegistryHelper.GetInt(key, "UEFISecureBootEnabled");
            if (value is not (0 or 1))
            {
                checks.Add(CheckRecord.NotVerifiable("security.secureboot", "Secure Boot",
                    "Windows no informó un valor válido para el estado de Secure Boot.",
                    "Registro de Windows"));
                return false;
            }

            data.SecureBoot = new SecureBootInfo
            {
                Enabled = value.Value == 1,
                FirmwareType = firmware,
                Source = "Registro de Windows"
            };

            if (value.Value == 1)
            {
                checks.Add(CheckRecord.Verified("security.secureboot", "Secure Boot",
                    $"Activado · firmware {Formatting.OrDash(firmware)}",
                    "Registro de Windows"));
            }
            else
            {
                checks.Add(CheckRecord.Problem("security.secureboot", "Secure Boot",
                    $"Desactivado · firmware {Formatting.OrDash(firmware)}",
                    "Registro de Windows"));
            }

            return true;
        }
        catch (RegistryReadException ex)
        {
            checks.Add(CollectorErrors.FromRegistry("security.secureboot", "Secure Boot", ex));
            return false;
        }
    }

    private static string? MapFirewallProfile(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "domain" => "Dominio",
        "private" => "Privada",
        "public" => "Pública",
        null or "" => null,
        _ => name!.Trim()
    };

    private static string? MapProtectionStatus(int? status) => status switch
    {
        0 => "Sin protección",
        1 => "Protegido",
        2 => "Desconocido",
        _ => null
    };

    private static string? MapConversionStatus(int? status) => status switch
    {
        0 => "Totalmente descifrado",
        1 => "Totalmente cifrado",
        2 => "Cifrado en curso",
        3 => "Descifrado en curso",
        4 => "Cifrado en pausa",
        5 => "Descifrado en pausa",
        _ => null
    };

    private static string? MapEncryptionMethod(int? method) => method switch
    {
        1 => "AES-CBC 128",
        2 => "AES-CBC 256",
        3 => "AES-XTS 128",
        4 => "AES-XTS 256",
        5 => "AES-XTS 128 (nuevo)",
        6 => "AES-XTS 256 (nuevo)",
        _ => method is null ? null : $"Método {method}"
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
