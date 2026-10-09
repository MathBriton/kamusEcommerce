namespace Kamus.Shared.Time;

/// <summary>
/// Fuso da loja (Brasília). Datas que a equipe digita ou lê no backoffice ("dia 08/10") são dias
/// nesse fuso, não em UTC: relatórios e filtros convertem os limites do dia por aqui.
/// </summary>
public static class StoreTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    /// <summary>Instante UTC em que o dia <paramref name="date"/> começa no fuso da loja.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Dia, no fuso da loja, de um instante.</summary>
    public static DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            // Imagens sem base de fusos: Brasília não tem horário de verão desde 2019.
            return TimeZoneInfo.CreateCustomTimeZone("America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
        }
    }
}
