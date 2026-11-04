# Arquitectura de AuditaPC

## Plataforma

- Aplicación de escritorio WPF para Windows.
- .NET 10 LTS, objetivo `net10.0-windows`.
- Publicación prevista: `win-x64`, autocontenida y de un solo archivo mediante `PortableWinX64.pubxml`.
- El ejecutable publicado no requiere un runtime .NET instalado en el computador auditado.
- `System.Management` consulta WMI/CIM; `Microsoft.Win32.Registry` consulta valores locales; `dsregcmd /status` se utiliza para información de unión del dispositivo.

## Proyectos

### `src/AuditaPC.App`

Interfaz WPF y navegación. Los ViewModels coordinan la apertura y creación de auditorías, captura, edición manual, fotografías, configuración y generación de informes. La interfaz no debe contener lógica de consultas WMI.

### `src/AuditaPC.Core`

- `Inventory/`: orquestación y contratos de colectores.
- `Inventory/Collectors/`: identificación, sistema operativo, hardware, almacenamiento, red, software, Office y seguridad.
- `Model/`: modelos serializables de auditoría, equipo, captura, inventario, notas de terreno y evidencias.
- `Storage/`: rutas, JSON, escritura atómica, bloqueo de auditoría y fotografías.
- `Matching/`: huellas y evaluación de coincidencias de equipos.
- `Reporting/`: preparación de datos, HTML consolidado/individual con notas de terreno, CSV y JSON.

### `tests/AuditaPC.Core.Tests`

Pruebas automatizadas de persistencia, rutas, coincidencias, fotografías, reportes y orquestación.

### `tools/AuditaPC.ExampleBuilder`

Genera una auditoría demostrativa con datos ficticios. No debe consultar el equipo real ni incorporar datos de clientes.

## Flujo de captura

1. `InventoryRunner` crea una `InventorySnapshot` y ejecuta colectores independientes con concurrencia limitada.
2. Cada colector devuelve su categoría, comprobaciones y estado.
3. Los resultados distinguen datos verificables, elevación requerida, acceso denegado, falta de conexión, limitaciones conocidas, errores y tiempos agotados; cada estado incompleto debe explicar el motivo.
4. Si se cancela la captura, el runner no devuelve una instantánea que pueda guardarse como completa.
5. `AuditRepository` conserva la captura y actualiza la ficha del equipo sin eliminar capturas anteriores.

## Persistencia portable

- La auditoría se almacena como archivos JSON versionados dentro de una carpeta propia.
- La carpeta predeterminada está junto al ejecutable, en `Auditorias`; el técnico puede cambiarla desde Configuración.
- Las rutas de configuración que están dentro de la carpeta de la aplicación se guardan relativas al ejecutable; no dependen de la letra USB.
- Las escrituras pasan por `AtomicFileWriter`, con archivo temporal, verificación y respaldo cuando corresponde.
- `AuditLock` evita que dos instancias escriban simultáneamente en la misma auditoría.
- Las fotografías originales y sus copias de visualización se almacenan dentro de la auditoría.

## Identidad de equipo

La huella combina identificadores disponibles y datos técnicos. No se debe fusionar automáticamente una captura cuando la coincidencia es ambigua. El nombre del equipo por sí solo no identifica de forma fiable un activo.

## Informes

`ReportData` prepara una vista de los datos guardados. `HtmlReportGenerator` produce HTML local con búsqueda y filtrado de fichas, notas de terreno separadas de la captura automática, impresión y una pequeña rutina JavaScript incorporada; no carga bibliotecas ni recursos de Internet. El apartado «Cómo leer el informe» aparece como una ventana de ayuda independiente fuera de `main` y queda oculto al imprimir el informe. `ReportWriter` genera el consolidado, una ficha HTML por equipo y exportaciones CSV/JSON.

Las rutas de imágenes se validan dentro de la carpeta de auditoría antes de leerlas. Los campos dinámicos del HTML deben codificarse; los campos CSV deben protegerse frente a inyección de fórmulas.

## Principios de seguridad

- Consultas locales y no invasivas.
- Sin telemetría ni administración remota. Por defecto no hay red saliente; solo se permiten consultas HTTPS optativas a endpoints oficiales de activación de Windows y Office, sin iniciar sesión ni ejecutar activaciones.
- No leer contraseñas, claves de recuperación ni claves completas de producto.
- No modificar la configuración del sistema auditado.
- Las consultas inaccesibles deben aparecer como no verificables, no como correctas. Los valores iniciales de `Win32_Tpm` no se interpretan como confirmación de preparación operativa del TPM.

## Decisiones que requieren validación en equipos físicos

- Diferencias entre versiones y proveedores WMI.
- Detección de Office por edición, arquitectura, instalación por usuario y modalidad de activación.
- Acceso a BitLocker, TPM, Secure Boot y estado del antivirus sin elevación.
- Identificación de equipos con seriales genéricos o máquinas virtuales.
- Comportamiento de escritura y bloqueo en pendrives FAT32, exFAT y NTFS.
