# Seguridad y reporte de vulnerabilidades

AuditaPC procesa información de inventario que puede identificar equipos, usuarios y organizaciones.

## Alcance de seguridad

- La aplicación está diseñada para realizar consultas locales de solo lectura.
- No envía inventarios a servicios externos ni incluye telemetría. Por defecto no utiliza Internet; si el técnico activa la opción de licencias, solo comprueba conectividad HTTPS con endpoints oficiales de Microsoft para Windows y Office.
- No recopila contraseñas, claves de recuperación ni claves completas de producto.
- Las rutas relativas de evidencias deben validarse antes de leer o abrir archivos.
- Los campos de usuario deben codificarse en HTML y neutralizarse en CSV.

## No publicar datos sensibles

No adjuntar a incidencias públicas auditorías reales, fotografías, nombres de usuario, direcciones IP internas, números de serie, identificadores de Windows ni rutas de clientes. Crear una reproducción con datos ficticios.

## Reportar una vulnerabilidad

Cuando el repositorio se publique, utilizar GitHub Security Advisories para reportar vulnerabilidades de forma privada. No publicar detalles explotables en una incidencia pública. Hasta que el repositorio remoto esté configurado, coordinar el reporte directamente con el responsable del proyecto.

## Limitaciones

El `.gitignore` reduce el riesgo de publicar datos locales, pero no garantiza que cada archivo sea seguro. Revisar manualmente los cambios y archivos antes de cada commit o publicación.
