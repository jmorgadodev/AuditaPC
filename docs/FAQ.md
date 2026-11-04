# Preguntas frecuentes

## ¿AuditaPC necesita instalarse?

No. Es una aplicación WPF portable para Windows x64. El paquete de GitHub Releases contiene un ejecutable autocontenido y las instrucciones. No instala servicios ni requiere instalar .NET en el equipo auditado.

## ¿Necesita Internet?

No para la captura local habitual. La comprobación HTTPS opcional relacionada con servicios de activación de Windows y Office está desactivada por defecto. Esa comprobación informa conectividad; no activa productos ni confirma la titularidad o vigencia de una suscripción.

## ¿Necesito ejecutar como administrador?

No para abrir la aplicación. Algunas consultas de Windows requieren elevación. Cuando no se dispone de permisos, el informe debe indicarlo como «Requiere administrador» o «Acceso denegado», en lugar de asumir que el dato es correcto.

## ¿Dónde se guardan las auditorías?

Por defecto, en la carpeta `Auditorias` junto a `AuditaPC.exe`. La ruta se puede cambiar en Configuración. Si el destino no permite escritura, la aplicación puede usar el perfil local de Windows; revisa el modo de almacenamiento antes de trabajar si necesitas mantener todo en el USB.

## ¿Por qué un resultado aparece como «No verificable» o «Alcance limitado»?

La fuente de Windows, el hardware, los permisos o la edición instalada pueden no proporcionar información suficiente. Estos estados no equivalen a «Problema» ni a «No detectado». Consulta [Interpretación de informes](INTERPRETACION_INFORMES.md).

## ¿Por qué el informe no refleja una edición reciente?

Los informes son exportaciones de los datos guardados en el momento de generarlos. Después de agregar una captura, cambiar notas o editar una ficha, vuelve a generar los informes.

## ¿La detección de Windows u Office demuestra que la licencia es válida?

No. La activación local y los datos de instalación ayudan al inventario, pero no prueban propiedad, contrato ni vigencia de Microsoft 365. La vigencia contractual requiere autenticación o revisión del portal correspondiente.

## ¿Puedo subir un informe a GitHub para pedir ayuda?

No si contiene datos de una organización o equipo real. Los informes pueden incluir nombres de equipo, usuario, IP, números de serie, activos, rutas y fotografías. Crea una reproducción con datos ficticios y elimina las evidencias reales.

## ¿Qué hago si el USB cambia de letra?

Mantén la carpeta completa de AuditaPC y conserva las rutas de auditoría relativas al programa. No muevas solo el EXE. Comprueba la ubicación activa en Configuración antes de capturar.

## ¿La aplicación administra los equipos?

No. AuditaPC realiza un levantamiento local de solo lectura. No instala ni desinstala software, no escanea otros equipos, no repara Windows y no ejecuta pruebas de rendimiento.
