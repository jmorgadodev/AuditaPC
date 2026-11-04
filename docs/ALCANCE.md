# Alcance funcional acordado

AuditaPC es una herramienta de levantamiento técnico e inventario local de equipos Windows, complementada con notas de intervención en terreno.

## Incluido

- Inventario automático de identificación, hardware, Windows, almacenamiento, red, software, Office y seguridad.
- Auditorías independientes por empresa y fecha, con identificador interno único.
- Nueva auditoría y continuación de una existente.
- Captura progresiva de equipos usando el mismo pendrive en distintos computadores.
- Actualización de un equipo con historial de capturas preservado.
- Observaciones, intervenciones, datos administrativos y fotografías opcionales por equipo.
- Notas de terreno por auditoría: resumen de visita, conectividad, módulos, estado, hallazgos, acciones, limitaciones y otras notas libres.
- Ventana independiente de programas instalados con búsqueda local.
- Informes HTML individuales y consolidados, CSV y JSON.
- Funcionamiento local sin Internet por defecto y sin permisos de administrador para iniciar.
- Comprobación HTTPS opcional y desactivada por defecto para endpoints de activación de Windows y Office; solo informa conectividad, no titularidad ni vigencia contractual.

## Excluido expresamente

- Instalación o desinstalación de programas.
- Administración remota, ejecución remota o escaneo de otros equipos.
- Modificación de configuración o reparación automática.
- Benchmarks, pruebas de estrés o monitoreo permanente.
- Telemetría, sincronización en nube o transmisión de inventarios. La única comunicación opcional son comprobaciones HTTPS de conectividad con servicios de activación de Microsoft para Windows y Office.

## Reglas de calidad

- No confundir instalado con activado, ni activación local con vigencia contractual.
- No convertir una consulta fallida en un estado correcto o en una ausencia confirmada.
- Mostrar fuente, estado y limitación cuando corresponda.
- Una categoría fallida no debe impedir la recopilación de las demás.
- Las capturas previas nunca se sobrescriben silenciosamente.
- Los datos automáticos deben permanecer separados de las notas introducidas por el técnico.
- Los informes y ejemplos deben evitar datos sensibles de clientes.

## Compatibilidad prevista

Windows 10 y Windows 11 de 64 bits. La compatibilidad efectiva debe verificarse en distintas ediciones y niveles de actualización. Windows Server, x86 y ARM64 no se consideran objetivos de la primera publicación.

## Limitaciones conocidas

- La vigencia de suscripciones Microsoft 365 no puede confirmarse offline. La comprobación HTTPS opcional solo verifica conectividad; la titularidad y vigencia contractual requieren autenticación o revisión del portal.
- Algunos estados de seguridad y salud de discos dependen del proveedor WMI, firmware, controlador y permisos.
- Las aplicaciones portables y ciertas instalaciones por usuario pueden no aparecer en los registros de desinstalación.
- Una sesión estándar puede obtener menos datos. Las consultas que necesitan elevación deben indicarlo expresamente; un acceso denegado incluso en una sesión elevada se clasifica por separado.
