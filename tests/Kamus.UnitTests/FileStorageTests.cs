using System.Text;
using Kamus.Shared.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kamus.UnitTests;

public sealed class FileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"kamus-storage-{Guid.NewGuid():N}");

    private LocalDiskFileStorage Storage() =>
        new(Options.Create(new LocalDiskStorageOptions { RootPath = _root }), new TestEnvironment());

    [Fact]
    public async Task Apagar_arquivo_e_idempotente()
    {
        var storage = Storage();
        var ct = TestContext.Current.CancellationToken;
        await storage.SaveAsync("products/camisa/areia-1.svg", new MemoryStream(Encoding.UTF8.GetBytes("<svg/>")), "image/svg+xml", ct);

        await storage.DeleteAsync("products/camisa/areia-1.svg", ct);
        await storage.DeleteAsync("products/camisa/areia-1.svg", ct);
        await storage.DeleteAsync("pasta-que-nao-existe/x.svg", ct);

        (await storage.ExistsAsync("products/camisa/areia-1.svg", ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Apagar_fora_da_raiz_e_recusado()
    {
        var delete = () => Storage().DeleteAsync("../../etc/passwd", TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "Kamus.UnitTests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
