# Decisiones técnicas y asuntos pendientes

## Decisiones ya adoptadas

| Tema | Decisión |
|---|---|
| Aplicación | Escritorio WPF para Windows |
| Plataforma | .NET 10 LTS (`net10.0-windows`) |
| Distribución prevista | `win-x64`, autocontenida y de un solo archivo |
| Persistencia | JSON local por auditoría y equipo |
| Informes | HTML individual/consolidado, CSV y JSON |
| Identidad visual | Profesional, consistente, accesible y funcional |
| Ejecución | Local y offline por defecto; comprobación HTTPS optativa de conectividad de licencias Windows/Office; sin instalación en los equipos auditados |
| Scripts anteriores | Solo referencia histórica en `legacy/v1/` |

.NET 10 es la línea LTS adecuada para un proyecto nuevo: Microsoft publica su soporte hasta noviembre de 2028. .NET 8 termina soporte el 11 de noviembre de 2026, por lo que el proyecto se actualizó antes de esa fecha.

## Validación y mantenimiento posterior a 1.0.0

1. **Licencias:** decisión adoptada. Código y ejecutable bajo MIT (`LICENSE`); documentación bajo CC BY 4.0 (`LICENSE-DOCS.md`). Revisar los avisos de dependencias y recursos de terceros al preparar distribuciones.
2. **Compatibilidad de Office:** ampliar, cuando haya equipos disponibles, la matriz de Microsoft 365 Apps, Office LTSC, licenciamiento por volumen, MSI y Click-to-Run.
3. **Seguridad:** continuar contrastando BitLocker, TPM, Secure Boot, antivirus y firewall según hardware, versión de Windows y permisos; el informe ya distingue resultados no verificables.
4. **Identificación:** seguir validando equipos renombrados, seriales genéricos, máquinas virtuales y modelos repetidos.
5. **Resistencia USB:** ampliar pruebas de desconexión inesperada, cambio de letra y poco espacio disponible.
6. **Privacidad:** evaluar una opción de redacción de usuarios, IP y números de serie antes de compartir informes externamente.
7. **Firma digital:** no hay certificado de firma de código configurado. Windows puede mostrar advertencias de SmartScreen; verificar siempre el origen y SHA-256.
8. **Publicación:** AuditaPC 1.0.0 se distribuye como versión estable desde GitHub Releases, con ZIP portable y checksum SHA-256.

## No ampliar el alcance

No agregar instalación de software, administración remota, escaneo de red, reparación, monitoreo continuo ni pruebas de rendimiento. Esas funciones no forman parte de AuditaPC.
