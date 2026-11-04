# Cómo interpretar los informes de AuditaPC

Esta guía explica los conceptos y no forma parte de los hallazgos de una auditoría específica. La misma explicación está disponible desde el botón **Cómo leer el informe**, separado del contenido principal del HTML.

## Lectura recomendada

Lee primero el **Resumen ejecutivo** para entender el número de equipos, problemas, resultados que requieren revisión y fotografías registradas. Después revisa **Notas de terreno**, **Inventario consolidado**, **Fichas técnicas**, **Licenciamiento**, **Hallazgos** y **Plan de acción**. Las notas e intervenciones manuales no son resultados obtenidos automáticamente.

El HTML consolidado presenta la captura vigente de cada equipo. El historial de capturas queda conservado localmente, pero no representa necesariamente el estado actual del computador. La fecha de generación y las fechas de captura son distintas.

## Significado de los estados

| Estado | Qué significa |
|---|---|
| Verificado | El dato se obtuvo y se pudo validar. |
| Detectado | Se observó presencia o configuración; no necesariamente su correcto funcionamiento. |
| No detectado | La consulta pudo ejecutarse, pero el elemento no se encontró. |
| Problema | Se confirmó un resultado que merece revisión técnica. |
| Parcial | Faltan datos de la categoría; debe leerse el motivo indicado. |
| No verificable | El proveedor, equipo o contexto no entrega suficiente información para concluir. |
| Requiere administrador | La consulta exige privilegios elevados y no se pudieron usar en esa sesión. |
| Acceso denegado | Windows o una política impidió el acceso. |
| No evaluado sin conexión | La comprobación depende de Internet o de una opción en línea desactivada. |
| Alcance limitado | La comprobación queda fuera del alcance o no puede obtenerse de forma fiable con la metodología utilizada. |
| No aplicable | La prueba no corresponde al equipo o edición. |
| Error de consulta | Falló una operación de lectura. |
| Tiempo agotado | La consulta no terminó dentro del límite establecido. |

**No verificable, no evaluado, no detectado y problema no son equivalentes.** Un estado incompleto no significa que el equipo esté averiado ni que una licencia haya caducado.

## Windows y Office

La activación local, versión y canal son datos útiles para inventariar; **no son comprobación de propiedad legal, contrato ni renovación de Microsoft 365**. Un producto detectado no implica licencia vigente. La comprobación HTTPS opcional solo verifica conectividad a servicios Microsoft; no activa productos ni valida una suscripción.

## Seguridad, almacenamiento y red

Las consultas de TPM, BitLocker, Secure Boot, antivirus, firewall, discos, red y dominio dependen de permisos, proveedores WMI, versión del sistema y políticas corporativas. Cuando la consulta no es concluyente, el informe debe conservarlo como limitación. El espacio en disco es una observación del momento de captura, no una prueba de velocidad o duración del hardware.

## Alcance y privacidad

AuditaPC inventaría localmente. No escanea redes, no instala software y no ejecuta benchmarks. Los informes pueden contener nombres de equipo, IP, serie, usuario y fotografías; **deben tratarse como información potencialmente confidencial**. No los adjuntes a GitHub ni a incidencias públicas. Genera ejemplos únicamente con información ficticia.
