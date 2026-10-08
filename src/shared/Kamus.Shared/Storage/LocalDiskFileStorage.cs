using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kamus.Shared.Storage;

public sealed class LocalDiskStorageOptions
{
    public const string SectionName = "Storage:LocalDisk";

    public string RootPath { get; set; } = "storage";

    public string PublicPathPrefix { get; set; } = "/files";
}

public sealed class LocalDiskFileStorage(IOptions<LocalDiskStorageOptions> options, IHostEnvironment environment) : IFileStorage
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    // Caminho relativo é resolvido a partir do content root da aplicação, não do diretório atual.
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);
    private readonly string _prefix = options.Value.PublicPathPrefix.TrimEnd('/');

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var contentType = ContentTypes.TryGetContentType(path, out var type) ? type : "application/octet-stream";
        Stream stream = File.OpenRead(path);
        return Task.FromResult<StoredFile?>(new StoredFile(stream, contentType, File.GetLastWriteTimeUtc(path)));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(Resolve(key)));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        // Idempotente: arquivo (ou pasta) inexistente não é erro.
        var path = Resolve(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"{_prefix}/{key}";

    /// <summary>Resolve a chave dentro da raiz, bloqueando path traversal (ex.: "../../etc/passwd").</summary>
    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Chave de arquivo inválida.", nameof(key));
        }

        return path;
    }
}
