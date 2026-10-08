namespace Kamus.Shared.Auditing;

/// <summary>
/// Contexto de quem está agindo na requisição (ou no escopo de um worker). Registrado como
/// <i>scoped</i>: tudo o que roda no mesmo escopo de DI enxerga o mesmo ator.
/// </summary>
/// <remarks>
/// Ordem de resolução de <see cref="Actor"/>: override ativo (<see cref="ActAs"/>) &gt; usuário
/// autenticado do <c>HttpContext</c> (Admin se tiver o papel Admin, senão Customer) &gt;
/// <c>AuditActor.System("Sistema")</c>.
/// </remarks>
public interface ICurrentActor
{
    /// <summary>Ator atual, já resolvido.</summary>
    AuditActor Actor { get; }

    /// <summary>
    /// Age como <paramref name="actor"/> até o <see cref="IDisposable.Dispose"/> do retorno. Aninhável:
    /// ao descartar, o ator anterior volta a valer. Use em workers e webhooks, ex.:
    /// <c>using (currentActor.ActAs(AuditActor.System("FakePay"))) { ... }</c>.
    /// </summary>
    IDisposable ActAs(AuditActor actor);

    /// <summary>
    /// Nada é auditado dentro do escopo retornado (seeders, cargas de dados de exemplo). Aninhável.
    /// O soft delete continua funcionando normalmente.
    /// </summary>
    IDisposable SuppressAuditing();

    bool IsAuditingSuppressed { get; }

    /// <summary>Id da requisição (<c>HttpContext.TraceIdentifier</c>); <see langword="null"/> fora de requisições.</summary>
    string? CorrelationId { get; }

    /// <summary>IP do cliente (respeita <c>X-Forwarded-For</c>, via <c>UseForwardedHeaders</c>).</summary>
    string? IpAddress { get; }
}
