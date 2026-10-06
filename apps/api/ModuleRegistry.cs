using Kamus.Cart;
using Kamus.Catalog;
using Kamus.Identity;
using Kamus.Inventory;
using Kamus.Orders;
using Kamus.Payments;
using Kamus.Shared.Modules;

namespace Kamus.Api;

/// <summary>Lista explícita dos módulos hospedados pelo monólito.</summary>
internal static class ModuleRegistry
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new CatalogModule(),
        new InventoryModule(),
        new CartModule(),
        new OrdersModule(),
        new PaymentsModule(),
        new IdentityModule(),
    ];
}
