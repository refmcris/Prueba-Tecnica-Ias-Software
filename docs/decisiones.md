# Decisiones técnicas

La prioridad fue separar el registro de una solicitud de su procesamiento y conservar suficiente información para recuperarla si el proveedor falla o el Worker se reinicia.

## Organización del backend

La API recibe las peticiones HTTP y responde mediante controladores y DTOs. El Worker procesa las solicitudes en segundo plano. Ambos usan Core, que reúne el modelo, las validaciones, la persistencia y el procesamiento.

Esta división permite recibir solicitudes aunque el Worker esté detenido. Core mantiene juntas las responsabilidades compartidas para evitar proyectos adicionales en una solución de este tamaño. Cada ejecutable registra sus propias dependencias y utiliza `PortalPaths` para resolver la misma carpeta de datos.

## Persistencia

Se eligió SQLite para tener almacenamiento real sin instalar un servidor. EF Core define los mapeos y las migraciones. SQL Server o PostgreSQL serían alternativas para una operación con más concurrencia o varios servidores.

La API y el Worker comparten `requests.db`. El proveedor guarda sus recibos en `provider.db`. Esta separación permite recuperar una operación cuyo efecto ya se guardó, aunque el Worker no alcanzara a marcarla como completada.

SQLite serializa las escrituras. Las esperas del proveedor ocurren fuera de las transacciones para no mantenerlas abiertas durante la llamada.

## Identidad de la operación

Cada operación se identifica por la combinación de `customerId` y `externalReference`. Un índice único evita registros duplicados, incluso cuando llegan dos peticiones al mismo tiempo.

Los campos se guardan sin espacios exteriores y se distinguen mayúsculas de minúsculas. Si se repite una operación con el mismo servicio y descripción, se devuelve la solicitud existente con `200`. Si el contenido cambia, se devuelve `409`. La primera creación responde con `201`.

Los límites de 128 caracteres para referencia, 100 para cliente, 80 para servicio y 2000 para descripción se definieron para validar las entradas. No son valores exigidos por el enunciado.

## Contratos

Los DTOs separan la entrada y la respuesta HTTP de la entidad almacenada. El cliente envía los cuatro campos del formulario y recibe los datos necesarios para consultar el estado. El token y el vencimiento de la reserva permanecen internos.

Los contratos de Core permiten utilizar esa información sin depender de ASP.NET Core.

## Procesamiento y recuperación

El Worker reclama una solicitud con una actualización condicional y guarda un token con vencimiento. Para guardar un resultado debe seguir siendo el propietario y conservar una reserva vigente. Si se interrumpe, otro Worker puede recuperar la solicitud cuando vence la reserva.

El proveedor usa el UUID de la solicitud como clave idempotente. Si ya tiene un recibo, devuelve el mismo. También registra el intento más reciente para impedir que una llamada anterior produzca un efecto después de otra más nueva.

La reserva protege los cambios sobre la solicitud y el recibo protege el efecto del proveedor. Puede haber varias llamadas para una operación, pero deben conservar un solo efecto. Esto requiere que el proveedor respete la clave y mantenga sus recibos.

Un fallo confirmado consume el presupuesto de reintentos. Con tres fallos confirmados, la solicitud pasa a `Failed`. Ante un timeout o una respuesta perdida, primero se consulta el recibo. Si no hay confirmación, se reintenta sin consumir ese presupuesto, por lo que esos casos pueden superar tres intentos.

`Completed` es terminal. Las consultas de procesamiento y las actualizaciones condicionales impiden que vuelva a un estado anterior.

## Interfaz y validación

React presenta el formulario, la lista y el filtro. La actualización se programa dos segundos después de terminar cada consulta, sin superponer peticiones. Las respuestas de consultas anteriores se descartan cuando cambia el filtro.

Las pruebas usan SQLite real en archivos temporales para comprobar las restricciones y la concurrencia. Un reloj controlable y proveedores de prueba permiten verificar reintentos, reservas vencidas y pérdida de confirmación. Las pruebas de React cubren la interacción del formulario y las consultas a la API.

## Alternativa con NoSQL

Cada solicitud podría guardarse como un documento, con una clave que mantenga la unicidad de cliente y referencia.
La reserva necesitaría una escritura condicional sobre el estado o la versión del documento.
Los recibos seguirían guardándose por clave idempotente.
La implementación dependería de las garantías de consistencia y las operaciones atómicas del motor elegido.
