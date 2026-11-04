# Ejemplos

La carpeta `auditoria-ficticia/` se **genera localmente** con `AuditaPC.ExampleBuilder` y está excluida de Git para evitar publicar archivos de inventario por accidente. El ejemplo incluye cinco equipos ficticios (cuatro con captura y una ficha manual), historial de capturas, una foto ilustrativa e informes HTML individuales/consolidados, CSV y JSON.

Para regenerarla desde la raíz del repositorio:

```powershell
dotnet run --project .\tools\AuditaPC.ExampleBuilder\AuditaPC.ExampleBuilder.csproj -c Release -- .\examples\auditoria-ficticia
```

El generador solo elimina la carpeta de salida si contiene el marcador `.auditapc-example` que él mismo crea; en caso contrario se detiene para proteger datos ajenos.

Solo colocar aquí inventarios, capturas y reportes generados con **datos ficticios o anonimizados de forma irreversible**.

No publicar datos reales de empresas, fotografías, usuarios, números de serie, IP internas o identificadores de acceso remoto.
