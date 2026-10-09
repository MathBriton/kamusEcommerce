using Kamus.Catalog.Api;
using Kamus.Catalog.Contracts;
using Kamus.Catalog.Persistence;
using Kamus.Orders.Contracts;
using Kamus.Shared.Events;
using Kamus.Shared.Infrastructure;
using Kamus.Shared.Results;
using Kamus.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamus.Catalog.Application;

internal enum TrashItemType
{
    Product,
    Sku,
    Image,
}

internal static class TrashItemTypes
{
    public static string Code(this TrashItemType type) => type switch
    {
        TrashItemType.Product => "product",
        TrashItemType.Sku => "sku",
        _ => "image",
    };

    /// <summary>"product", "sku" ou "image" (sem diferenciar maiúsculas).</summary>
    public static bool TryParse(string? value, out TrashItemType type)
    {
        foreach (var candidate in Enum.GetValues<TrashItemType>())
        {
            if (string.Equals(value, candidate.Code(), StringComparison.OrdinalIgnoreCase))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }
}

/// <summary>
/// Lixeira do catálogo: lista o que foi excluído, restaura e expurga (apaga de vez). A exclusão em
/// si é um <c>db.Remove</c> comum, transformado em soft delete pelo interceptor do Shared.
/// </summary>
/// <remarks>
/// Produto excluído leva os SKUs e imagens ativos com o mesmo <c>DeletedAt</c>: é por esse instante
/// que eles voltam juntos na restauração e que a listagem os mostra só dentro do produto.
/// </remarks>
internal sealed class CatalogTrashService(
    CatalogDbContext db,
    IFileStorage storage,
    IOrderImageReferences orderImages,
    IEventPublisher events,
    IOptions<CatalogOptions> options,
    TimeProvider clock,
    ILogger<CatalogTrashService> logger)
{
    public async Task<Result<TrashPage>> ListAsync(string? type, int page, int pageSize, CancellationToken ct)
    {
        TrashItemType? filter = null;
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!TrashItemTypes.TryParse(type, out var parsed))
            {
                return Error.Validation("catalog.invalid_trash_type", "Tipo inválido: use all, product, sku ou image.");
            }

            filter = parsed;
        }

        // A lixeira é pequena (o expurgo apaga o que passa da retenção): as chaves cabem em memória,
        // o que permite juntar os três tipos numa única ordenação e paginar sem UNION.
        var keys = await KeysAsync(ct);
        var counts = new TrashCounts(
            keys.Count(k => k.Type == TrashItemType.Product),
            keys.Count(k => k.Type == TrashItemType.Sku),
            keys.Count(k => k.Type == TrashItemType.Image));

        var filtered = keys
            .Where(k => filter is null || k.Type == filter)
            .OrderByDescending(k => k.DeletedAt).ThenBy(k => k.Id)
            .ToList();
        var items = await DescribeAsync([.. filtered.Skip((page - 1) * pageSize).Take(pageSize)], ct);

        return new TrashPage(items, filtered.Count, counts);
    }

    public async Task<Result<RestoreResult>> RestoreAsync(string type, Guid id, CancellationToken ct)
    {
        if (!TrashItemTypes.TryParse(type, out var parsed))
        {
            return NotInTrash();
        }

        try
        {
            return parsed switch
            {
                TrashItemType.Product => await RestoreProductAsync(id, ct),
                TrashItemType.Sku => await RestoreSkuAsync(id, ct),
                _ => await RestoreImageAsync(id, ct),
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            // Alguém expurgou ou alterou o item entre a leitura e a gravação (token xmin).
            db.ChangeTracker.Clear();
            return Changed();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Corrida com outra alteração entre a verificação e a gravação.
            return Error.Conflict("catalog.restore_conflict", "Outro item ativo passou a usar o mesmo endereço ou código. Tente novamente.");
        }
    }

    public Task<Result> PurgeAsync(string type, Guid id, CancellationToken ct) =>
        TrashItemTypes.TryParse(type, out var parsed) ? PurgeAsync(parsed, id, ct) : Task.FromResult<Result>(NotInTrash());

    /// <summary>
    /// Apaga de vez um item que está na lixeira (produto: com todos os SKUs e imagens, inclusive os
    /// excluídos antes). Depois de gravar, avisa os outros módulos (<see cref="SkusPurged"/>) e
    /// remove os arquivos das imagens.
    /// </summary>
    public async Task<Result> PurgeAsync(TrashItemType type, Guid id, CancellationToken ct)
    {
        List<Guid> skuIds;
        List<string> files;
        switch (type)
        {
            case TrashItemType.Product:
                var product = await db.Products.IgnoreQueryFilters()
                    .Include(p => p.Skus).Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt != null, ct);
                if (product is null)
                {
                    return NotInTrash();
                }

                skuIds = [.. product.Skus.Select(s => s.Id)];
                files = [.. product.Images.Select(i => i.StorageKey)];
                db.Products.Remove(product); // os filhos rastreados vão junto (cascade do EF)
                break;

            case TrashItemType.Sku:
                var sku = await db.Skus.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt != null, ct);
                if (sku is null)
                {
                    return NotInTrash();
                }

                skuIds = [sku.Id];
                files = [];
                db.Skus.Remove(sku);
                break;

            default:
                var image = await db.Images.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt != null, ct);
                if (image is null)
                {
                    return NotInTrash();
                }

                skuIds = [];
                files = [image.StorageKey];
                db.Images.Remove(image);
                break;
        }

        // Remover o que já está na lixeira é DELETE de verdade (SoftDeleteInterceptor), auditado como "purged".
        // O DELETE leva o xmin lido: se o item foi restaurado (ou alterado) nesse meio-tempo, nada é apagado.
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Changed();
        }

        // O expurgo já foi gravado: falhar aqui só faria a tela mostrar erro de algo que aconteceu.
        // Um aviso perdido deixa estoque órfão (invisível na loja) até a R3 trazer o outbox.
        if (skuIds.Count > 0)
        {
            try
            {
                await events.PublishAsync(new SkusPurged(skuIds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Expurgo gravado, mas o aviso SkusPurged falhou para {Count} SKU(s): estoque pode ficar órfão", skuIds.Count);
            }
        }

        await DeleteFilesAsync(files, CancellationToken.None);
        return Result.Success();
    }

    /// <summary>
    /// Expurgo automático: apaga de vez o que foi para a lixeira antes de <paramref name="cutoff"/>.
    /// Cada item é gravado separadamente: uma falha não impede os demais. Retorna quantos foram apagados.
    /// </summary>
    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var purged = 0;

        // Produtos primeiro: levam junto os filhos, que então não são expurgados de novo.
        foreach (var type in Enum.GetValues<TrashItemType>())
        {
            var ids = type switch
            {
                TrashItemType.Product => await db.Products.IgnoreQueryFilters().Where(p => p.DeletedAt < cutoff)
                    .OrderBy(p => p.DeletedAt).ThenBy(p => p.Id).Select(p => p.Id).ToListAsync(ct),
                TrashItemType.Sku => await db.Skus.IgnoreQueryFilters().Where(s => s.DeletedAt < cutoff)
                    .OrderBy(s => s.DeletedAt).ThenBy(s => s.Id).Select(s => s.Id).ToListAsync(ct),
                _ => await db.Images.IgnoreQueryFilters().Where(i => i.DeletedAt < cutoff)
                    .OrderBy(i => i.DeletedAt).ThenBy(i => i.Id).Select(i => i.Id).ToListAsync(ct),
            };

            foreach (var id in ids)
            {
                db.ChangeTracker.Clear();
                try
                {
                    if ((await PurgeAsync(type, id, ct)).IsSuccess)
                    {
                        purged++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ex.: conflito com uma restauração simultânea, ou falha de quem trata SkusPurged.
                    logger.LogError(ex, "Falha ao expurgar {Type} {Id} da lixeira", type.Code(), id);
                }
            }
        }

        db.ChangeTracker.Clear();
        return purged;
    }

    private async Task<Result<RestoreResult>> RestoreProductAsync(Guid id, CancellationToken ct)
    {
        var product = await db.Products.IgnoreQueryFilters()
            .Include(p => p.Skus).Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt != null, ct);
        if (product is null)
        {
            return NotInTrash();
        }

        var other = await db.Products
            .Where(p => p.Slug == product.Slug && p.Id != product.Id)
            .Select(p => p.Category.Path + "/" + p.Slug)
            .FirstOrDefaultAsync(ct);
        if (other is not null)
        {
            return Error.Conflict("catalog.slug_taken", $"Já existe outro produto com o endereço /{other}. Renomeie o outro antes de restaurar.");
        }

        // Voltam só os filhos que foram para a lixeira junto com o produto (mesmo instante).
        var deletedAt = product.DeletedAt;
        var skus = product.Skus.Where(s => s.DeletedAt == deletedAt).ToList();
        var images = product.Images.Where(i => i.DeletedAt == deletedAt).ToList();

        var codes = skus.Select(s => s.Code).ToList();
        var takenCode = await db.Skus.Where(s => codes.Contains(s.Code)).Select(s => s.Code).OrderBy(c => c).FirstOrDefaultAsync(ct);
        if (takenCode is not null)
        {
            return Error.Conflict("catalog.sku_exists", $"Já existe outra variação ativa com o código {takenCode}. Exclua-a antes de restaurar.");
        }

        var now = clock.GetUtcNow();
        product.Restore();
        skus.ForEach(s => s.Restore());
        images.ForEach(i => i.Restore());
        product.Touch(now);

        // Publicado sem nenhuma variação quebraria a vitrine: nesse caso volta como rascunho.
        if (product.IsActive && product.ActiveSkuCount == 0)
        {
            product.Deactivate(now);
        }

        await db.SaveChangesAsync(ct);
        return new RestoreResult(TrashText.ProductRestored(product.Name, product.IsActive, skus.Count, images.Count));
    }

    private async Task<Result<RestoreResult>> RestoreSkuAsync(Guid id, CancellationToken ct)
    {
        var sku = await db.Skus.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt != null, ct);
        if (sku is null)
        {
            return NotInTrash();
        }

        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == sku.ProductId, ct);
        if (product.DeletedAt is not null)
        {
            return ParentDeleted();
        }

        if (await db.Skus.AnyAsync(s => s.ProductId == sku.ProductId && s.Color == sku.Color && s.Size == sku.Size, ct))
        {
            return Error.Conflict("catalog.sku_exists", $"Já existe {sku.Color} no tamanho {sku.Size} neste produto. Exclua a variação nova antes de restaurar.");
        }

        if (await db.Skus.AnyAsync(s => s.Code == sku.Code, ct))
        {
            return Error.Conflict("catalog.sku_exists", $"Já existe outra variação ativa com o código {sku.Code}. Exclua-a antes de restaurar.");
        }

        sku.Restore();
        product.Touch(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new RestoreResult(TrashText.SkuRestored(product.Name, sku.Color, sku.Size));
    }

    private async Task<Result<RestoreResult>> RestoreImageAsync(Guid id, CancellationToken ct)
    {
        var image = await db.Images.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt != null, ct);
        if (image is null)
        {
            return NotInTrash();
        }

        var product = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == image.ProductId, ct);
        if (product.DeletedAt is not null)
        {
            return ParentDeleted();
        }

        // Vai para o fim da galeria da cor: a posição antiga pode ter sido ocupada por outra foto.
        var last = await db.Images.Where(i => i.ProductId == image.ProductId && i.Color == image.Color)
            .MaxAsync(i => (int?)i.SortOrder, ct);
        image.Restore();
        image.MoveTo((last ?? -1) + 1);
        product.Touch(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new RestoreResult(TrashText.ImageRestored(product.Name, image.Color));
    }

    /// <summary>Chaves (tipo, id, quando) de tudo que está na lixeira, sem os filhos excluídos junto com o produto.</summary>
    private async Task<List<TrashKey>> KeysAsync(CancellationToken ct)
    {
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.DeletedAt != null)
            .Select(p => new { p.Id, DeletedAt = p.DeletedAt!.Value })
            .ToListAsync(ct);

        var skus = await (
                from s in db.Skus.IgnoreQueryFilters().AsNoTracking()
                join p in db.Products.IgnoreQueryFilters() on s.ProductId equals p.Id
                where s.DeletedAt != null && s.DeletedAt != p.DeletedAt
                select new { s.Id, DeletedAt = s.DeletedAt!.Value })
            .ToListAsync(ct);

        var images = await (
                from i in db.Images.IgnoreQueryFilters().AsNoTracking()
                join p in db.Products.IgnoreQueryFilters() on i.ProductId equals p.Id
                where i.DeletedAt != null && i.DeletedAt != p.DeletedAt
                select new { i.Id, DeletedAt = i.DeletedAt!.Value })
            .ToListAsync(ct);

        return
        [
            .. products.Select(p => new TrashKey(TrashItemType.Product, p.Id, p.DeletedAt)),
            .. skus.Select(s => new TrashKey(TrashItemType.Sku, s.Id, s.DeletedAt)),
            .. images.Select(i => new TrashKey(TrashItemType.Image, i.Id, i.DeletedAt)),
        ];
    }

    /// <summary>Monta os itens de uma página, na ordem das chaves.</summary>
    private async Task<List<TrashItemDto>> DescribeAsync(List<TrashKey> keys, CancellationToken ct)
    {
        var retention = options.Value.TrashRetentionDays;
        var byType = keys.ToLookup(k => k.Type, k => k.Id);
        var rows = new Dictionary<TrashItemType, Dictionary<Guid, TrashRow>>
        {
            [TrashItemType.Product] = await ProductRowsAsync([.. byType[TrashItemType.Product]], ct),
            [TrashItemType.Sku] = await SkuRowsAsync([.. byType[TrashItemType.Sku]], ct),
            [TrashItemType.Image] = await ImageRowsAsync([.. byType[TrashItemType.Image]], ct),
        };

        var items = new List<TrashItemDto>(keys.Count);
        foreach (var key in keys)
        {
            // Pode ter sido restaurado ou expurgado entre as duas consultas.
            if (!rows[key.Type].TryGetValue(key.Id, out var row))
            {
                continue;
            }

            items.Add(new TrashItemDto(
                key.Type.Code(),
                row.Id,
                row.ProductId,
                row.Name,
                row.Detail,
                row.ImageKey is null ? null : storage.GetPublicUrl(row.ImageKey),
                row.DeletedAt,
                row.DeletedById is null && row.DeletedByName is null ? null : new TrashActorDto(row.DeletedById, row.DeletedByName),
                row.DeletedAt.AddDays(retention),
                row.SkuCount,
                row.ImageCount,
                row.WasActive));
        }

        return items;
    }

    private async Task<Dictionary<Guid, TrashRow>> ProductRowsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Brand,
                p.IsActive,
                DeletedAt = p.DeletedAt!.Value,
                p.DeletedById,
                p.DeletedByName,
                Skus = p.Skus.Count(s => s.DeletedAt == p.DeletedAt),
                Images = p.Images.Count(i => i.DeletedAt == p.DeletedAt),
                // Miniatura: a primeira imagem do produto, mesmo que tenha ido para a lixeira junto.
                Image = p.Images.Where(i => i.DeletedAt == null || i.DeletedAt == p.DeletedAt)
                    .OrderBy(i => i.SortOrder).ThenBy(i => i.Color).ThenBy(i => i.Id)
                    .Select(i => i.StorageKey).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => new TrashRow(
            r.Id, r.Id, r.Name, TrashText.ProductDetail(r.Brand, r.Skus, r.Images), r.Image,
            r.DeletedAt, r.DeletedById, r.DeletedByName, r.Skus, r.Images, r.IsActive));
    }

    private async Task<Dictionary<Guid, TrashRow>> SkuRowsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await (
                from s in db.Skus.IgnoreQueryFilters().AsNoTracking()
                join p in db.Products.IgnoreQueryFilters() on s.ProductId equals p.Id
                where ids.Contains(s.Id)
                select new
                {
                    s.Id,
                    s.ProductId,
                    ProductName = p.Name,
                    s.Color,
                    s.Size,
                    s.Code,
                    DeletedAt = s.DeletedAt!.Value,
                    s.DeletedById,
                    s.DeletedByName,
                    OnSale = p.IsActive && p.DeletedAt == null,
                    // Miniatura: a primeira imagem da cor (as ativas antes das excluídas).
                    Image = db.Images.Where(i => i.ProductId == s.ProductId && i.Color == s.Color)
                        .OrderBy(i => i.DeletedAt != null).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                        .Select(i => i.StorageKey).FirstOrDefault(),
                })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => new TrashRow(
            r.Id, r.ProductId, TrashText.SkuName(r.ProductName, r.Color, r.Size), r.Code, r.Image,
            r.DeletedAt, r.DeletedById, r.DeletedByName, 0, 0, r.OnSale));
    }

    private async Task<Dictionary<Guid, TrashRow>> ImageRowsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await (
                from i in db.Images.IgnoreQueryFilters().AsNoTracking()
                join p in db.Products.IgnoreQueryFilters() on i.ProductId equals p.Id
                where ids.Contains(i.Id)
                select new
                {
                    i.Id,
                    i.ProductId,
                    ProductName = p.Name,
                    i.Color,
                    i.SortOrder,
                    i.StorageKey,
                    DeletedAt = i.DeletedAt!.Value,
                    i.DeletedById,
                    i.DeletedByName,
                    OnSale = p.IsActive && p.DeletedAt == null,
                })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => new TrashRow(
            r.Id, r.ProductId, TrashText.ImageName(r.ProductName, r.SortOrder), $"Cor {r.Color}", r.StorageKey,
            r.DeletedAt, r.DeletedById, r.DeletedByName, 0, 0, r.OnSale));
    }

    /// <summary>Apaga os arquivos que nenhuma imagem (ativa ou na lixeira) usa mais.</summary>
    private async Task DeleteFilesAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        var candidates = keys.Distinct(StringComparer.Ordinal).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        // Pedidos guardam a URL da foto no momento da compra: esses arquivos ficam, senão o histórico
        // do cliente e do backoffice perde a miniatura (o pedido nunca é excluído).
        var inOrders = await orderImages.FindReferencedAsync([.. candidates.Select(storage.GetPublicUrl)], ct);

        foreach (var key in candidates)
        {
            if (inOrders.Contains(storage.GetPublicUrl(key))
                || await db.Images.IgnoreQueryFilters().AnyAsync(i => i.StorageKey == key, ct))
            {
                continue;
            }

            try
            {
                await storage.DeleteAsync(key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // O banco já foi gravado: um arquivo órfão não justifica falhar o expurgo.
                logger.LogWarning(ex, "Não foi possível apagar o arquivo {Key} do armazenamento", key);
            }
        }
    }

    private static Error NotInTrash() => Error.NotFound("catalog.trash_item_not_found", "Item não encontrado na lixeira.");

    private static Error ParentDeleted() => Error.Conflict("catalog.parent_deleted", "Restaure o produto primeiro.");

    private static Error Changed() => Error.Conflict(
        "catalog.trash_changed",
        "Este item mudou enquanto você mexia na lixeira (alguém o restaurou, excluiu de vez ou alterou). Recarregue a página.");

    private sealed record TrashKey(TrashItemType Type, Guid Id, DateTimeOffset DeletedAt);

    private sealed record TrashRow(
        Guid Id,
        Guid ProductId,
        string Name,
        string? Detail,
        string? ImageKey,
        DateTimeOffset DeletedAt,
        Guid? DeletedById,
        string? DeletedByName,
        int SkuCount,
        int ImageCount,
        bool WasActive);
}
