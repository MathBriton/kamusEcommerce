using Kamus.Api;
using Kamus.Api.Health;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Storage;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

ValidationDefaults.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres")
    .AddCheck<RedisHealthCheck>("redis");

foreach (var module in ModuleRegistry.All)
{
    module.Register(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapFileStorage();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

foreach (var module in ModuleRegistry.All)
{
    module.MapEndpoints(app);
}

await app.InitializeModulesAsync();

await app.RunAsync();

public partial class Program;
