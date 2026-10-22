using System.Text.Json.Serialization;
using GameStore.Api;
using GameStore.Core;
using GameStore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddGameStoreServices();
builder.Services.AddGameStoreInMemoryData(builder.Configuration["Database:Name"] ?? "GameStore");

var app = builder.Build();

await app.Services.SeedGameStoreAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
