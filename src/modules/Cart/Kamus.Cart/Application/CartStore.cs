using System.Globalization;
using Kamus.Cart.Contracts;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Kamus.Cart.Application;

internal sealed class CartOptions
{
    public const string SectionName = "Cart";

    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromDays(7);

    public int MaxQuantityPerItem { get; set; } = 10;

    public int MaxItems { get; set; } = 50;
}

/// <summary>Dono do carrinho: um visitante anônimo (cookie) ou um cliente autenticado.</summary>
internal readonly record struct CartOwner(string Kind, string Id)
{
    public static CartOwner Visitor(string visitorId) => new("v", visitorId);

    public static CartOwner Customer(Guid customerId) => new("c", customerId.ToString("N"));

    public string Key => $"kamus:cart:{Kind}:{Id}";
}

/// <summary>
/// Carrinho em Redis: um hash por dono (campo = SKU, valor = "quantidade|preço-ao-adicionar"),
/// com TTL renovado a cada escrita. Escritas usam transação condicional (otimista) para não
/// perder atualizações de duas abas ao mesmo tempo.
/// </summary>
internal sealed class CartStore(IConnectionMultiplexer redis, IOptions<CartOptions> options)
{
    private readonly CartOptions _options = options.Value;

    private IDatabase Db => redis.GetDatabase();

    public async Task<IReadOnlyList<CartLine>> GetAsync(CartOwner owner)
    {
        var entries = await Db.HashGetAllAsync(owner.Key);
        return [.. entries.Select(e => Parse(Guid.Parse(e.Name.ToString()), e.Value.ToString())).OrderBy(l => l.SkuId)];
    }

    public async Task<CartLine?> GetLineAsync(CartOwner owner, Guid skuId)
    {
        var value = await Db.HashGetAsync(owner.Key, skuId.ToString());
        return value.IsNull ? null : Parse(skuId, value.ToString());
    }

    /// <summary>
    /// Altera uma linha com base no valor atual. <paramref name="change"/> recebe a linha atual (ou null)
    /// e devolve a nova (ou null para remover). Repete se outra escrita aconteceu no meio.
    /// </summary>
    public async Task<CartLine?> UpdateLineAsync(CartOwner owner, Guid skuId, Func<CartLine?, CartLine?> change)
    {
        var field = skuId.ToString();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var current = await Db.HashGetAsync(owner.Key, field);
            var next = change(current.IsNull ? null : Parse(skuId, current.ToString()));

            var tx = Db.CreateTransaction();
            tx.AddCondition(current.IsNull ? Condition.HashNotExists(owner.Key, field) : Condition.HashEqual(owner.Key, field, current));
            _ = next is null
                ? tx.HashDeleteAsync(owner.Key, field)
                : tx.HashSetAsync(owner.Key, field, Format(next));
            _ = tx.KeyExpireAsync(owner.Key, _options.TimeToLive);

            if (await tx.ExecuteAsync())
            {
                return next;
            }
        }

        throw new InvalidOperationException("Não foi possível atualizar o carrinho por concorrência.");
    }

    public async Task<int> CountLinesAsync(CartOwner owner) => (int)await Db.HashLengthAsync(owner.Key);

    public Task ClearAsync(CartOwner owner) => Db.KeyDeleteAsync(owner.Key);

    /// <summary>Junta o carrinho do visitante no do cliente (soma quantidades) e apaga o do visitante.</summary>
    public async Task MergeAsync(CartOwner from, CartOwner into)
    {
        var lines = await GetAsync(from);
        foreach (var line in lines)
        {
            await UpdateLineAsync(into, line.SkuId, existing => line with
            {
                Quantity = Math.Min(_options.MaxQuantityPerItem, (existing?.Quantity ?? 0) + line.Quantity),
            });
        }

        await ClearAsync(from);
    }

    private static string Format(CartLine line) =>
        string.Create(CultureInfo.InvariantCulture, $"{line.Quantity}|{line.PriceWhenAdded}");

    private static CartLine Parse(Guid skuId, string value)
    {
        var parts = value.Split('|');
        return new CartLine(skuId, int.Parse(parts[0], CultureInfo.InvariantCulture), decimal.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
