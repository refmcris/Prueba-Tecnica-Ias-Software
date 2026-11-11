# Portal de solicitudes

Portal para registrar solicitudes de servicio y consultar su estado. La API guarda las solicitudes y un Worker las procesa contra un proveedor simulado. Los reintentos y reinicios se manejan sin repetir el efecto de una misma operación.

La solución usa ASP.NET Core, React con TypeScript, EF Core y SQLite.

## Requisitos

SDK de .NET 10.0.404 y Node 24, desde la versión 24.15 y menor que 25. Las dependencias están fijadas en `packages.lock.json` y `package-lock.json`. `global.json` permite actualizaciones de parche dentro de la misma banda del SDK.

## Preparación

Desde la raíz del repositorio.

```powershell
dotnet restore .\RequestsPortal.sln --locked-mode
dotnet build .\RequestsPortal.sln --no-restore
```

Frontend.

```powershell
cd .\frontend
npm ci
npm run build
```

## Ejecución

Los comandos de la API y el Worker parten de la raíz del repositorio. `--no-build` utiliza la compilación existente.

API

```powershell
dotnet run --project .\src\RequestsPortal.Api --no-build --no-launch-profile -- --urls http://127.0.0.1:5080
```

Worker

```powershell
dotnet run --project .\src\RequestsPortal.Worker --no-build --no-launch-profile
```

Frontend

```powershell
cd .\frontend
npm run dev
```

La aplicación queda disponible en [http://127.0.0.1:5173](http://127.0.0.1:5173). El proxy de desarrollo de Vite dirige `/api` a la API del puerto 5080.

## Almacenamiento y configuración

Las migraciones se aplican al iniciar. La API inicializa la base de solicitudes y el Worker inicializa ambas bases. También se pueden crear sin iniciar el procesamiento.

```powershell
dotnet run --project .\src\RequestsPortal.Worker --no-build --no-launch-profile -- --init-db
```

Los archivos se guardan en `data/requests.db` y `data/provider.db`. El primero contiene las solicitudes y sus reservas. El segundo conserva los efectos y recibos del proveedor. Ambos quedan fuera de Git.

La configuración está en el `appsettings.json` de cada ejecutable. Para usar otro directorio, la API y el Worker deben recibir la misma ruta absoluta mediante `--Portal:DataDirectory "E:\DatosPortal"`, después del separador `--` de `dotnet run`. Esta configuración es necesaria cuando se publica fuera del repositorio.

Para aplicar las migraciones manualmente están disponibles los siguientes comandos. Se indica el contexto porque hay dos en el mismo proyecto.

```powershell
dotnet tool restore
dotnet ef database update --project .\src\RequestsPortal.Core --context RequestsDbContext
dotnet ef database update --project .\src\RequestsPortal.Core --context ProviderDbContext
```

## API

La creación recibe cuatro campos obligatorios.

```json
{
  "externalReference": "ORD-001",
  "customerId": "CLIENTE-01",
  "serviceCode": "INSTALACION",
  "description": "Instalar el servicio en la sede principal"
}
```

| Operación | Respuesta |
| --- | --- |
| `POST /api/requests` | `201` con el recurso y `Location` |
| POST repetido con el mismo contenido | `200` con la solicitud existente |
| POST repetido con contenido diferente | `409` sin modificar la solicitud |
| `GET /api/requests/{id}` | `200` con el recurso o `404` |
| `GET /api/requests` | Lista de solicitudes |
| `GET /api/requests?status=Pending` | Lista filtrada por estado |

Una operación se identifica por `customerId` y `externalReference`. Se recortan los espacios exteriores y se distinguen mayúsculas de minúsculas. Para una operación existente, cambiar el servicio o la descripción produce un conflicto.

Los límites de entrada son 128 caracteres para referencia, 100 para cliente, 80 para servicio y 2000 para descripción. Los estados aceptados son `Pending`, `Processing`, `Completed` y `Failed`. Un filtro vacío, repetido o desconocido devuelve `400`.

La respuesta incluye el identificador, los cuatro campos de entrada, estado, `createdAt`, `updatedAt`, `attemptCount`, `lastError` y `providerReceiptId`. Las fechas se manejan en UTC y los datos internos de la reserva no se exponen. Los errores usan `ProblemDetails`, con el campo correspondiente cuando falla una validación.

El estado lo administra el Worker. La API no expone una operación para modificarlo directamente.

## Organización

`src/RequestsPortal.Api` contiene los controladores, DTOs, conversiones y manejo de errores HTTP.

`src/RequestsPortal.Core` reúne el modelo, las validaciones, la persistencia y el procesamiento que utilizan los ejecutables.

`src/RequestsPortal.Worker` configura y ejecuta el procesamiento en segundo plano.

`frontend/src` contiene la pantalla React, sus componentes y el acceso a la API. `tests/RequestsPortal.Tests` contiene las pruebas de .NET.

La API y el Worker registran sus dependencias por separado porque son procesos distintos. Ambos usan `PortalPaths` para encontrar la misma base de solicitudes. Cada proceso mantiene sus propios contextos de EF Core.

## Pruebas

Desde la raíz del repositorio.

```powershell
dotnet test .\RequestsPortal.sln
```

Desde `frontend`.

```powershell
npm test
npm run build
```

Las 112 pruebas de .NET cubren el registro, las validaciones, los duplicados concurrentes, las reservas, los reintentos y la recuperación tras una interrupción. Usan archivos SQLite temporales. Las 37 de React cubren el formulario, las respuestas de la API, los filtros y la actualización de la lista. Ambas suites pasaron en la revisión de la entrega.

## Documentación

- [Decisiones técnicas](docs/decisiones.md)
- [Revisión del código legado](docs/revision-legado.md)
- [Worker como servicio de Windows](docs/servicio-windows.md)
- [Uso de IA](docs/uso-ia.md)
