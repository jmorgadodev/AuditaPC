# Guía de uso · AuditaPC

AuditaPC permite realizar levantamientos presenciales de computadores Windows. Es portable, no administra equipos remotos y funciona sin Internet por defecto.

## 1. Descargar y preparar

1. Descarga [AuditaPC-win-x64-portable.zip](https://github.com/jmorgadodev/AuditaPC/releases/download/v0.1.0-beta.2/AuditaPC-win-x64-portable.zip) desde GitHub Releases.
2. Descarga el [checksum SHA-256](https://github.com/jmorgadodev/AuditaPC/releases/download/v0.1.0-beta.2/AuditaPC-win-x64-portable.zip.sha256) y verifica que coincida con el archivo antes de ejecutarlo.
3. Esta es una pre-release; revisa las limitaciones y pruebas pendientes antes de usarla en producción.
4. Descomprime la carpeta completa `AuditaPC` en el pendrive o en una ubicación escribible.
5. Ejecuta `AuditaPC.exe`. No requiere instalador ni runtime .NET instalado en el equipo auditado.
6. La carpeta `Auditorias` se crea junto al ejecutable si no existe. La configuración aparece al guardar preferencias.

```text
AuditaPC/
├── AuditaPC.exe
├── LEEME.txt
└── Auditorias/
    └── [una carpeta por auditoría]
```

Windows puede advertir que la aplicación no está firmada digitalmente. Verifica el origen y el checksum antes de ejecutarla; no desactives las protecciones del equipo.

## 2. Crear o reabrir una auditoría

En **Auditorías**, elige **Nueva auditoría** o abre una existente. Introduce empresa, técnico, fecha y notas iniciales. Cada auditoría recibe una carpeta e identificador únicos. Comprueba en la cabecera qué expediente está activo antes de capturar un equipo.

Puedes mantener varias empresas en el mismo pendrive. Los datos permanecen separados por auditoría.

## 3. Capturar un computador

1. Conecta el USB al equipo que vas a levantar y ejecuta `AuditaPC.exe`.
2. Abre la auditoría correspondiente y selecciona **Capturar este equipo**.
3. Espera las consultas locales. Algunas pueden requerir permisos elevados o devolver resultados parciales.
4. Selecciona **Revisar resultados**. La captura todavía no está guardada.
5. Revisa si corresponde a una ficha existente o a un equipo nuevo.
6. Confirma con **Guardar captura**.

La captura solo se conserva al confirmar el guardado. Si la coincidencia es ambigua, no fusiones fichas sin revisar los identificadores. Las capturas anteriores se conservan como historial.

## 4. Completar la visita

En cada ficha puedes anotar responsable, ubicación, código de activo, hallazgos, intervenciones, pendientes y recomendaciones. **Notas de terreno** permite registrar observaciones generales de la auditoría, como conectividad, módulos, condiciones, limitaciones y acciones.

Las notas manuales se muestran separadas de los datos automáticos. Guarda los cambios antes de cerrar o cambiar de auditoría.

Las fotografías son opcionales. Antes de compartir un informe, comprueba que no contenga personas, credenciales, documentos sensibles ni información confidencial.

## 5. Generar informes

Pulsa **Generar informes**. La ventana de resultados permite abrir el HTML consolidado, los HTML individuales y los archivos CSV/JSON.

Los HTML funcionan sin conexión, permiten buscar y filtrar fichas y se pueden imprimir o guardar como PDF desde el navegador. El botón **Cómo leer el informe** abre una guía separada del contenido técnico y no se incluye al imprimir.

Los informes son una exportación del estado guardado en el momento de generarlos. Si modificas notas, fichas o capturas, vuelve a generarlos. Los informes anteriores no se actualizan automáticamente.

## 6. Cerrar y respaldar

Cierra AuditaPC, confirma que no quedan cambios pendientes y expulsa el USB mediante Windows. No retires la unidad durante una escritura o generación de informes. Para respaldar una auditoría, copia su carpeta completa a un destino autorizado y protegido.

## Limitaciones

- Objetivo de distribución: Windows 10/11 x64. La compatibilidad debe validarse en las ediciones y políticas de cada organización.
- Algunas consultas dependen de permisos, proveedores WMI, firmware y versión de Windows.
- La detección de software puede omitir aplicaciones portables, instalaciones por usuario o aplicaciones Microsoft Store.
- La activación local de Windows/Office no demuestra titularidad ni vigencia contractual de Microsoft 365.
- Si el USB no permite escritura, la aplicación puede usar el perfil local de Windows. Comprueba el modo de almacenamiento en Configuración.

Más información: [Interpretación de informes](INTERPRETACION_INFORMES.md), [FAQ](FAQ.md), [Alcance](ALCANCE.md), [Pruebas](PRUEBAS.md) y [Seguridad](../SECURITY.md).
