# Publicación y descarga de AuditaPC

## Repositorio

Repositorio público: [github.com/jmorgadodev/AuditaPC](https://github.com/jmorgadodev/AuditaPC).

La rama principal se llama `main`. La integración continua se ejecuta en los cambios a `main` y en las pull requests mediante `.github/workflows/ci.yml`.

## Descarga portable

El artefacto publicado es:

`AuditaPC-win-x64-portable.zip`

Incluye:
- `AuditaPC/AuditaPC.exe`: aplicación autocontenida para Windows x64;
- `AuditaPC/LEEME.txt`: instrucciones breves;
- `AuditaPC/Auditorias/`: carpeta vacía para los datos del usuario.

No contiene auditorías, capturas, informes, archivos de configuración ni símbolos de depuración. El flujo `.github/workflows/release.yml` genera también el checksum SHA-256.

**Estado actual:** el repositorio y la documentación están publicados; todavía no existe un Release binario. La descarga directa y el checksum estarán disponibles después de aprobar la licencia del software y publicar el primer tag de versión.

Una vez publicado, la URL estable de descarga será:

`https://github.com/jmorgadodev/AuditaPC/releases/latest/download/AuditaPC-win-x64-portable.zip`

El checksum SHA-256 se adjuntará al mismo Release.

## Crear un Release

En Windows, desde la raíz del repositorio:

```powershell
dotnet restore .\AuditaPC.sln
dotnet test .\AuditaPC.sln -c Release --no-restore
dotnet publish .\src\AuditaPC.App\AuditaPC.App.csproj -c Release -p:PublishProfile=PortableWinX64
.\tools\Build-PortableRelease.ps1
```

El paquete local queda en `artifacts/releases/`. El script de empaquetado copia únicamente el EXE, las instrucciones y una carpeta de auditorías vacía; valida el contenido del ZIP y calcula SHA-256.

Para publicar una versión automatizada:

```powershell
git tag v0.1.0-beta.1
git push origin v0.1.0-beta.1
```

El workflow se activa al enviar un tag `v*` y publica la versión como pre-release. Restaura, ejecuta las pruebas, publica el EXE, construye el ZIP y crea un GitHub Release con ambos archivos.

## Licencias

La documentación se distribuye bajo [CC BY 4.0](../LICENSE-DOCS.md) y el código fuente/ejecutable bajo [MIT](../LICENSE). Las dependencias y recursos de terceros mantienen sus propias licencias y avisos.

## Revisión de privacidad antes de cada publicación

No subir auditorías reales ni exportaciones generadas durante el trabajo de campo. Revisar `git status`, `git diff --cached --check` y los nombres de archivo antes de crear un commit. El `.gitignore` es una ayuda, no una garantía.

Los informes pueden contener identificadores de equipos, usuarios, direcciones IP, números de serie, activos, rutas y fotografías. Para capturas de pantalla, GIF y ejemplos del README se deben utilizar exclusivamente datos ficticios.

## Advertencias del ejecutable

El EXE no está firmado digitalmente. Windows puede mostrar advertencias de SmartScreen. El usuario debe verificar que descarga el archivo desde el repositorio oficial y comprobar el SHA-256 publicado. No se debe recomendar desactivar las protecciones de Windows.
