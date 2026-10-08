namespace Kamus.Audit.Application;

/// <summary>
/// Rótulos em português para o CSV (as telas do backoffice têm os mesmos rótulos em
/// <c>apps/admin/src/lib/audit.ts</c>). Código desconhecido aparece como está.
/// </summary>
internal static class AuditLabels
{
    private static readonly Dictionary<string, string> Modules = new()
    {
        ["catalog"] = "Catálogo",
        ["inventory"] = "Estoque",
        ["orders"] = "Pedidos",
    };

    private static readonly Dictionary<(string EntityType, string Action), string> Actions = new()
    {
        [("Product", "created")] = "Criou produto",
        [("Product", "updated")] = "Editou produto",
        [("Product", "published")] = "Publicou",
        [("Product", "unpublished")] = "Despublicou",
        [("Product", "deleted")] = "Excluiu",
        [("Product", "restored")] = "Restaurou",
        [("Product", "purged")] = "Excluiu de vez",
        [("Sku", "created")] = "Adicionou variação",
        [("Sku", "updated")] = "Editou variação",
        [("Sku", "deleted")] = "Excluiu variação",
        [("Sku", "restored")] = "Restaurou variação",
        [("Sku", "purged")] = "Excluiu variação de vez",
        [("ProductImage", "created")] = "Enviou imagem",
        [("ProductImage", "deleted")] = "Excluiu imagem",
        [("ProductImage", "restored")] = "Restaurou imagem",
        [("ProductImage", "purged")] = "Excluiu imagem de vez",
        [("StockLevel", "created")] = "Definiu estoque",
        [("StockLevel", "stock_adjusted")] = "Ajustou estoque",
        [("StockLevel", "stock_sold")] = "Baixa por venda",
        [("StockLevel", "purged")] = "Removeu estoque",
        [("Order", "created")] = "Criou pedido",
        [("Order", "payment_started")] = "Iniciou pagamento",
        [("Order", "paid")] = "Confirmou pagamento",
        [("Order", "payment_failed")] = "Pagamento recusado",
        [("Order", "shipped")] = "Despachou",
        [("Order", "delivered")] = "Confirmou entrega",
        [("Order", "cancelled")] = "Cancelou",
    };

    public static string Module(string module) => Modules.GetValueOrDefault(module, module);

    public static string Action(string entityType, string action) => Actions.GetValueOrDefault((entityType, action), action);

    public static string ActorKind(string kind) => kind switch
    {
        "Admin" => "Admin",
        "Customer" => "Cliente",
        "System" => "Sistema",
        _ => kind,
    };
}
