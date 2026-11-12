# Worker como servicio de Windows

El Worker integra `AddWindowsService` con el nombre `RequestsPortal.Worker`. Para ejecutarlo como servicio se publica, se define una carpeta de datos compartida con la API y se registra en Windows. La instalación del servicio no es necesaria para ejecutar la prueba local.

## Publicación

Desde la raíz del repositorio.

```powershell
dotnet publish .\src\RequestsPortal.Worker\RequestsPortal.Worker.csproj -c Release -o .\.artifacts\worker-publish
```

La publicación requiere el runtime de .NET 10. La carpeta completa de salida, incluido `appsettings.json`, se copia a una ubicación estable, por ejemplo `C:\Services\RequestsPortal\worker`.

## Configuración

En el `appsettings.json` publicado se agrega `Portal`, conservando las secciones existentes.

```json
"Portal": {
  "DataDirectory": "C:\\Services\\RequestsPortal\\data"
}
```

La API debe usar la misma ruta absoluta. La cuenta del servicio necesita lectura sobre la publicación y escritura sobre los datos. El Worker aplica las migraciones al arrancar y carga su configuración desde la carpeta del ejecutable.

## Registro

PowerShell con privilegios de administrador.

```powershell
New-Service -Name 'RequestsPortal.Worker' -BinaryPathName '"C:\Services\RequestsPortal\worker\RequestsPortal.Worker.exe"' -DisplayName 'Requests Portal Worker' -StartupType Automatic
Start-Service -Name 'RequestsPortal.Worker'
Get-Service -Name 'RequestsPortal.Worker'
```

[Referencia de Microsoft](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service).
