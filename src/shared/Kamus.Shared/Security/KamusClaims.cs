namespace Kamus.Shared.Security;

/// <summary>Claims próprias do Kamus, emitidas pelo módulo Identity no cookie de autenticação.</summary>
public static class KamusClaims
{
    /// <summary>Nome completo do usuário (<c>KamusUser.FullName</c>), usado como nome do ator na auditoria.</summary>
    public const string FullName = "kamus:full_name";
}
