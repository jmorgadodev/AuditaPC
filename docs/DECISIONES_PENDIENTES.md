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

## Pendiente antes de publicar la primera versión estable

1. **Licencias:** decisión adoptada. Código y ejecutable bajo MIT (`LICENSE`); documentación bajo CC BY 4.0 (`LICENSE-DOCS.md`). Revisar los avisos de dependencias y recursos de terceros al preparar distribuciones.
2. **Validación física:** probar el ejecutable portable en Windows 10 y 11 con cuenta estándar y elevada.
3. **Office:** validar Microsoft 365 Apps, Office LTSC, Office por volumen, instalaciones MSI y Click-to-Run, en distintas arquitecturas.
4. **Seguridad:** validar BitLocker, TPM, Secure Boot, antivirus y firewall en equipos reales; evitar falsos positivos.
5. **Identificación:** comprobar equipos renombrados, seriales genéricos, máquinas virtuales y modelos repetidos.
6. **USB:** probar FAT32, exFAT y NTFS; desconexión inesperada, cambio de letra y poco espacio disponible.
7. **Privacidad:** decidir si la primera versión incluirá una opción de redacción de usuarios, IP y números de serie en los informes compartidos.
8. **Firma digital:** no hay certificado de firma de código configurado. El ejecutable puede mostrar advertencias de SmartScreen hasta que exista firma y reputación.
9. **Publicación:** repositorio público y Release `v0.1.0-beta.2` disponibles. La descarga portable y su checksum SHA-256 se publican automáticamente después de ejecutar las pruebas en Windows CI. La versión beta continúa como pre-release mientras se amplía la matriz de compatibilidad física.

## No ampliar el alcance

No agregar instalación de software, administración remota, escaneo de red, reparación, monitoreo continuo ni pruebas de rendimiento. Esas funciones no forman parte de AuditaPC.
