namespace Kamus.Identity.Contracts;

/// <summary>Evento: um cliente acabou de entrar (login ou cadastro) nesta requisição.</summary>
public sealed record CustomerSignedIn(Guid CustomerId);

public interface ICustomerDirectory
{
    Task<CustomerInfo?> FindAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public sealed record CustomerInfo(Guid Id, string Email, string FullName);
