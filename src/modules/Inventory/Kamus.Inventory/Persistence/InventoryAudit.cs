using Kamus.Catalog.Contracts;
using Kamus.Inventory.Domain;
using Kamus.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace Kamus.Inventory.Persistence;

/// <summary>
/// Política de auditoria do módulo Inventory: só o estoque físico (<see cref="StockLevel.Quantity"/>)
/// é auditado. Reservas entram e saem a cada checkout e não são auditadas (nem a entidade
/// <see cref="Reservation"/>, nem a coluna <see cref="StockLevel.Reserved"/>).
/// </summary>
internal static class InventoryAudit
{
    public const string Module = "inventory";

    /// <summary>Ajuste manual do físico (backoffice ou carga), sem mexer no reservado.</summary>
    public const string StockAdjusted = "stock_adjusted";

    /// <summary>Baixa por venda: a reserva do pedido pago vira saída do físico.</summary>
    public const string StockSold = "stock_sold";

    public static void Configure(AuditPolicyBuilder audit) => audit
        .Module(Module)
        .Entity<StockLevel>(e => e
            .SubjectAsync((sp, level, ct) => sp.GetRequiredService<StockAuditSubjects>().ResolveAsync(level, ct))
            .Track(s => s.Quantity, "Físico")
            .Action(RefineAction));

    /// <summary>
    /// <see cref="StockAdjusted"/> quando só o físico mudou; <see cref="StockSold"/> quando físico e
    /// reservado caíram juntos. Mudanças só no reservado (reservar, liberar, expirar) ficam "updated"
    /// sem campo rastreado e, por isso, não geram registro.
    /// </summary>
    public static string? RefineAction(EntityEntry<StockLevel> entry)
    {
        var quantity = entry.Property(s => s.Quantity);
        var reserved = entry.Property(s => s.Reserved);
        return Classify(quantity.OriginalValue, quantity.CurrentValue, reserved.OriginalValue, reserved.CurrentValue);
    }

    public static string? Classify(int quantityBefore, int quantityAfter, int reservedBefore, int reservedAfter) =>
        (quantityAfter != quantityBefore, reservedAfter != reservedBefore) switch
        {
            (true, false) => StockAdjusted,
            (true, true) when quantityAfter < quantityBefore && reservedAfter < reservedBefore => StockSold,
            _ => null,
        };
}

/// <summary>
/// Agregado exibido na auditoria do estoque: o produto do SKU (rótulo = nome do produto, detalhe =
/// "Cor · Tamanho"), via contrato do Catalog. SKU desconhecido do Catalog (ex.: já expurgado) fica
/// como agregado "Sku", sem rótulo.
/// </summary>
/// <remarks>
/// Scoped. Na primeira consulta de um SaveChanges, descreve de uma vez todos os SKUs alterados no
/// change tracker (uma consulta por gravação, não uma por linha) e guarda o resultado no escopo.
/// </remarks>
internal sealed class StockAuditSubjects(InventoryDbContext db, ICatalogService catalog)
{
    public const string SkuSubjectType = "Sku";

    public const string ProductSubjectType = "Product";

    private readonly Dictionary<Guid, SkuDescription?> _known = [];

    public async ValueTask<AuditSubject> ResolveAsync(StockLevel level, CancellationToken ct)
    {
        if (!_known.TryGetValue(level.SkuId, out var sku))
        {
            var pending = db.ChangeTracker.Entries<StockLevel>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => e.Entity.SkuId)
                .Append(level.SkuId)
                .Where(id => !_known.ContainsKey(id))
                .ToHashSet();

            var found = await catalog.DescribeSkusAsync(pending, ct);
            foreach (var id in pending)
            {
                _known[id] = found.GetValueOrDefault(id);
            }

            sku = _known[level.SkuId];
        }

        return sku is null
            ? new AuditSubject(SkuSubjectType, level.SkuId, null)
            : new AuditSubject(ProductSubjectType, sku.ProductId, sku.ProductName, $"{sku.Color} · {sku.Size}");
    }
}
