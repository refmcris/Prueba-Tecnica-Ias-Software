using Microsoft.EntityFrameworkCore;
using RequestsPortal.Api.Configuration;
using RequestsPortal.Core.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRequestServices();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<RequestsDbContext>();
    await context.Database.MigrateAsync();
}

app.Run();

// Permite iniciar la API desde las pruebas de integración.
public partial class Program
{
}
