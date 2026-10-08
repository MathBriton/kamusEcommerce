namespace Kamus.Shared.Auditing;

/// <summary>Tipo de quem fez a alteração auditada.</summary>
public enum AuditActorKind
{
    Admin,
    Customer,
    System,
}

/// <summary>
/// Quem fez a alteração. <paramref name="Name"/> é o nome exibido ("Administrador Kamus",
/// "Maria Silva", "FakePay"). Ações automáticas usam <see cref="System(string)"/>, sem id nem e-mail.
/// </summary>
public sealed record AuditActor(AuditActorKind Kind, Guid? UserId, string Name, string? Email)
{
    /// <summary>Nome usado quando não há usuário autenticado nem ator explícito.</summary>
    public const string DefaultSystemName = "Sistema";

    /// <summary>Ator automático (worker, webhook, expurgo...).</summary>
    public static AuditActor System(string name) => new(AuditActorKind.System, null, name, null);
}
