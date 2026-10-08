namespace Kamus.Shared.Storage;

/// <summary>
/// Abstração de armazenamento de arquivos (imagens de produto). Implementada em disco local no
/// MVP; na R4 ganha uma implementação S3-compatível sem mudar quem consome.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Apaga o arquivo. Idempotente: chave inexistente não é erro.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>URL pública (relativa ao host da API) para servir o arquivo.</summary>
    string GetPublicUrl(string key);
}

public sealed record StoredFile(Stream Content, string ContentType, DateTimeOffset LastModified);
