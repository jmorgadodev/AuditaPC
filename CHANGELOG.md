# Historial de cambios

## En desarrollo

- Reorganizada la guía «Cómo leer este informe» como ayuda independiente fuera del contenido principal, excluida de impresión/PDF.
- Preparación del ZIP portable autónomo para GitHub Releases, sin instalador, dependencias externas ni datos de auditorías.
- Documentación de uso, interpretación de informes, privacidad y publicación segura.
- Prueba desde USB FAT32 con una ficha de equipo y dos capturas, sin publicar sus datos.
- Rediseño del producto como levantamiento técnico e inventario local.
- Carpeta portable predeterminada `Auditorias` junto al ejecutable y ubicación configurable.
- Fallback de activación local de Office mediante `OSPP.VBS /dstatus` cuando WMI no está disponible; no lee ni guarda claves.
- Detección de Office deduplicada entre Click-to-Run y MSI, incluso con nombres localizados.
- Informes HTML con caracteres Unicode legibles y estados explicados en la revisión de captura.
- Notas de terreno estructuradas por auditoría, exportadas en HTML y JSON.
- Ventana independiente de programas instalados con búsqueda.
- Estados diferenciados para elevación, acceso denegado, comprobaciones no intentadas sin conexión y limitaciones conocidas.
- Comprobación HTTPS opcional de conectividad de servicios Microsoft para Windows y Office, desactivada por defecto.
- Barra lateral contraíble, nueva identidad visual y atribución de autor en Configuración.
- Evolución de la herramienta original a una aplicación de escritorio WPF independiente.
- Inventario local modular para identidad, Windows, hardware, almacenamiento, red, software, Office y seguridad.
- Gestión de auditorías por empresa/fecha, capturas e historial.
- Almacenamiento JSON local, escrituras atómicas y rutas portables.
- Datos manuales y fotografías opcionales.
- Informes HTML consolidados e individuales, CSV y JSON.
- Migración de la solución a .NET 10 LTS.
- Protección frente a cancelación de captura, fórmulas CSV y rutas de fotografías no confiables.
- Perfil de publicación autocontenida `win-x64` e integración continua en Windows.

## 1.0.0 — Versión estable

- Primera versión estable de AuditaPC para Windows x64.
- Aplicación portable y autocontenida, con almacenamiento local y funcionamiento offline por defecto.
- Validación de campo en USB FAT32, exFAT y NTFS, con permisos estándar y de administrador.
- Captura de información del equipo, auditorías por empresa y fecha, historial, notas de terreno y fotografías opcionales.
- Informes HTML individuales y consolidados, CSV y JSON; ayuda de interpretación HTML independiente y excluida de impresión.
- Código fuente y ejecutable bajo MIT; documentación bajo CC BY 4.0.
- ZIP portable y checksum SHA-256 publicados en GitHub Releases.
