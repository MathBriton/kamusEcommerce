using System.Globalization;

namespace Kamus.Shared.Auditing;

/// <summary>
/// Formatação padrão dos valores auditados (o que aparece em "antes → depois"). Independe da
/// cultura do servidor: o resultado é sempre em pt-BR.
/// </summary>
public static class AuditFormat
{
    private static readonly NumberFormatInfo PtBr = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
    };

    /// <summary>
    /// <see langword="null"/> → <see langword="null"/>; <see cref="decimal"/> → "R$ 249,90";
    /// <see cref="bool"/> → "Sim"/"Não"; demais → <c>ToString()</c> (cultura invariante).
    /// </summary>
    public static string? Value(object? value) => value switch
    {
        null => null,
        decimal money => Money(money),
        bool flag => flag ? "Sim" : "Não",
        DateTimeOffset at => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>Valor monetário em reais: "R$ 1.249,90" (negativos: "-R$ 10,00").</summary>
    public static string Money(decimal value)
    {
        var text = Math.Abs(value).ToString("N2", PtBr);
        return value < 0 ? $"-R$ {text}" : $"R$ {text}";
    }
}
