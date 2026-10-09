using Kamus.Api;
using Kamus.Api.Health;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Storage;
using Kamus.Shared.Validation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

ValidationDefaults.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddFileStorage(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

// A API fica atrás do Next.js (BFF): confia nos cabeçalhos X-Forwarded-* para saber se a
// requisição original era HTTPS (cookies Secure) e qual era o IP do cliente (gravado na auditoria).
// Só confia neles quando vêm da rede interna (onde roda o BFF): quem chamar a API direto da internet
// não consegue forjar o IP. O BFF repassa o X-Forwarded-For que recebeu: o IP só é confiável atrás
// de um proxy de borda que sobrescreva esse cabeçalho (o deploy planejado usa Caddy, que faz isso
// por padrão). Sem ele, quem age pode forjar o próprio IP, nunca a identidade (ver ADR 0014).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var network in (string[])["127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "::1/128", "fc00::/7"])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

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

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

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
