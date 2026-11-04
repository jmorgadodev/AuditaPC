# Contribuir a AuditaPC

Gracias por interesarte en AuditaPC. Las contribuciones deben mantener el objetivo del proyecto: levantamiento técnico e inventario local de equipos Windows.

## Antes de proponer cambios

- Lee `README.md`, `docs/ALCANCE.md` y `docs/ARQUITECTURA.md`.
- No agregues instalación de software, administración remota, escaneo de otros equipos, reparación ni monitoreo.
- No incluyas datos reales de clientes ni identificadores de equipos reales.
- Mantén separadas las consultas automáticas y las anotaciones manuales.
- Si una consulta no puede verificarse, no la conviertas en un resultado correcto.

## Desarrollo

1. Crea una rama para el cambio.
2. Implementa en el proyecto o módulo correspondiente.
3. Añade pruebas automatizadas cuando sea posible.
4. Ejecuta `dotnet test .\AuditaPC.sln -c Release` en Windows.
5. Documenta nuevas limitaciones, dependencias o cambios de esquema.

## Estilo

- Mantén el código legible, modular y con nombres descriptivos.
- Evita dependencias innecesarias y recursos externos en los informes.
- La interfaz debe ser profesional, sobria, consistente y accesible; cada elemento debe cumplir una función clara.
- No introduzcas telemetría ni llamadas de red adicionales. La única excepción permitida son las comprobaciones HTTPS optativas y explícitas de endpoints oficiales de activación de Windows y Office; nunca deben activar productos, transmitir claves o validar titularidad sin autenticación.

## Pull requests

Describe el problema, la solución, las pruebas ejecutadas y los riesgos. Adjunta capturas solo de datos ficticios. No publiques artefactos con información de auditorías reales.
