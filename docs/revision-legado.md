# Revisión del código legado

Esta es una revisión documental de los dos fragmentos del enunciado. No se incorporaron a la solución ni se crearon proyectos WebForms o MVC para ejecutarlos.

## Búsqueda en WebForms

La consulta concatena `txtCustomer.Text` dentro del SQL y permite inyección SQL. La corrección es mantener fija la consulta y enviar el texto mediante un parámetro tipado.

Este ejemplo supone que `Customer` es `nvarchar(100)`. Tanto el tipo como el tamaño deben ajustarse al esquema real; `100` es solo un valor de ejemplo.

```csharp
const string sql = "SELECT Id, Customer, Status FROM ServiceRequests WHERE Customer = @Customer";

using var connection = new SqlConnection(
    ConfigurationManager.ConnectionStrings["Main"].ConnectionString);
using var command = new SqlCommand(sql, connection);

command.Parameters.Add("@Customer", SqlDbType.NVarChar, 100).Value = txtCustomer.Text;

connection.Open();
using var reader = command.ExecuteReader();

GridView1.DataSource = reader;
GridView1.DataBind();
```

El lector permanece abierto hasta que termina `DataBind`. Los `using` liberan el lector, el comando y la conexión al salir del ámbito.

## Actualización de estado en MVC

Antes de cambiar `Status`, hay que comprobar si la solicitud es `null`, validar el estado recibido y aplicar las transiciones permitidas por el negocio. Una solicitud en `Completed` debe conservar ese estado. Asignar directamente cualquier texto permite estados desconocidos y cambios como volver a `Pending`.

La validación también debe protegerse frente a peticiones simultáneas: leer el estado y comprobarlo no basta. Se puede usar una versión (`rowversion` en SQL Server) y manejar los conflictos, o un `UPDATE` condicionado al estado o la versión esperada, comprobando cuántas filas se modificaron.

La respuesta debería devolver un DTO con los datos necesarios, sin serializar toda la entidad. Si la autenticación usa cookies, el formulario debe enviar un token antifalsificación y la acción debe validarlo; `[HttpPost]` por sí solo no ofrece esa protección.

En la solución nueva, el Worker administra el estado y no se expone una acción para modificarlo libremente.
