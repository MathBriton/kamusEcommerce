namespace Kamus.Catalog.Application;

/// <summary>Configuração do catálogo (seção <c>Catalog</c>).</summary>
internal sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    /// <summary>Dias que um item fica na lixeira antes do expurgo automático. Padrão: 30.</summary>
    public int TrashRetentionDays { get; set; } = 30;

    /// <summary>Intervalo entre as rodadas do expurgo automático. Padrão: 6 horas.</summary>
    public TimeSpan TrashPurgeInterval { get; set; } = TimeSpan.FromHours(6);
}
