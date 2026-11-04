# Pruebas y criterios de aceptación

## Pruebas automatizadas

Ejecutar en Windows con .NET 10 SDK:

```powershell
dotnet restore .\AuditaPC.sln
dotnet test .\AuditaPC.sln -c Release --no-restore --logger "console;verbosity=minimal"
```

Las pruebas cubren almacenamiento de auditorías, escritura atómica, rutas seguras, migración de la carpeta portable, coincidencia de equipos, fotografías, generación de informes con notas de terreno y acentos UTF-8, clasificación de permisos y estados offline, configuración portable, cancelación/tiempo agotado de colectores y terminación de procesos hijos al cancelar.

## Pruebas manuales obligatorias

| Escenario | Resultado esperado |
|---|---|
| Crear auditoría | Carpeta separada con empresa, fecha e ID único |
| Varias empresas | No se mezclan registros ni evidencias |
| Misma empresa y fecha | Cada auditoría conserva ID independiente |
| Cambiar letra del pendrive | Se encuentra la misma carpeta relativa |
| Primera ejecución del portable | Crea `Auditorias` directamente junto al ejecutable |
| Cambiar ubicación en Configuración | Las auditorías nuevas se crean en la ruta elegida |
| Carpeta del ejecutable sin escritura | Se informa el fallback a LocalAppData |
| Comprobación online desactivada | No se realizan solicitudes de red y las licencias quedan como no evaluadas sin conexión |
| Comprobación online activada | Solo se consulta conectividad HTTPS de Windows/Office; no se ejecutan activaciones ni se confirma titularidad |
| Permisos estándar y elevados | Se diferencia requiere administrador de acceso denegado incluso elevado |
| Programas instalados | Abre una ventana independiente y el buscador filtra nombre, editor, versión, fecha y ámbito |
| Notas de terreno | Se guardan por auditoría y aparecen en HTML/JSON separadas de los datos automáticos |
| Capturar varios equipos | Cada equipo se guarda sin sobrescribir otros |
| Volver a capturar un equipo | Se propone coincidencia y se conserva historial |
| Coincidencia ambigua | Se solicita confirmación; no se fusiona solo |
| Cuenta estándar | La aplicación inicia; las consultas restringidas se indican |
| Cancelar captura | No se permite guardar una captura cancelada como completa |
| WMI no disponible | Se registra no verificable/error, no un estado correcto |
| Office sin activación comprobable | Se separa detección de instalación y licencia |
| Agregar fotografías | Original y vista se guardan; al reabrir siguen disponibles |
| HTML offline | Abre sin Internet, filtra fichas localmente e imprime sin cargar recursos externos |
| Ayuda HTML separada | «Cómo leer el informe» abre un diálogo independiente, se puede cerrar y no se imprime |
| Reporte histórico | Los reportes previos siguen intactos; después de editar o capturar, generar un nuevo informe |
| Desconectar USB durante escritura | No se destruyen registros previos; se informa el error |
| Reabrir auditoría en otro PC | Se recuperan equipos, capturas, notas e imágenes |

## Matriz de entornos

**Validado manualmente en las pruebas de campo:**
- Pendrive FAT32, exFAT y NTFS.
- Ejecución con cuenta estándar y con privilegios de administrador.
- Captura de la información disponible del equipo y persistencia local de la auditoría.

**Ampliación de cobertura recomendada para futuras pruebas:**
- Windows 10 y Windows 11 en distintas compilaciones.
- Equipo unido a dominio Active Directory, Microsoft Entra ID o híbrido, y grupo de trabajo.
- Equipos físicos con HDD/SSD/NVMe y equipos virtuales.
- Desconexión inesperada, cambio de letra y poco espacio disponible.

## Evidencia de pruebas de campo y automatizadas

Las pruebas manuales confirmaron el funcionamiento del paquete portable desde unidades USB **FAT32, exFAT y NTFS**, con ejecución tanto mediante cuenta estándar como con privilegios de administrador. Se verificó la captura de información del equipo y el guardado local de las auditorías. Los datos reales utilizados durante las pruebas no se incluyen en el repositorio.

En el SDK .NET 10 se ejecutaron **62 pruebas automatizadas correctas**. La integración continua de Windows también valida restauración, pruebas, compilación y publicación del ejecutable portable `win-x64`. `assets/demo.gif` utiliza una auditoría sintética; no contiene datos de clientes.

## Reglas de evidencia

- No declarar una prueba aprobada si no se ejecutó.
- Registrar versión de Windows, arquitectura, permisos y resultado.
- Utilizar datos ficticios en ejemplos y documentación pública.
- Las pruebas con equipos de clientes requieren autorización y no deben incorporarse al repositorio.
