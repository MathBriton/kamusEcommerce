using System.Globalization;
using System.Text;
using Kamus.Audit.Api;

namespace Kamus.Audit.Application;

/// <summary>
/// Exportação da auditoria em CSV para Excel/LibreOffice em pt-BR: UTF-8 com BOM, separador <c>;</c>
/// e cabeçalho em português.
/// </summary>
internal static class AuditCsv
{
    public const char Separator = ';';

    private static readonly string[] Header =
    [
        "Data e hora (UTC)", "Módulo", "Ação", "Item", "Detalhe", "Alterações", "Usuário",
        "Tipo de usuário", "E-mail", "Entidade", "Id da entidade", "IP", "Requisição",
    ];

    public static byte[] Write(IEnumerable<AuditEntryDto> entries)
    {
        var csv = new StringBuilder();
        AppendRow(csv, Header);

        foreach (var e in entries)
        {
            AppendRow(csv,
            [
                e.OccurredAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                AuditLabels.Module(e.Module),
                AuditLabels.Action(e.EntityType, e.Action),
                e.SubjectLabel ?? $"{e.SubjectType} {e.SubjectId}",
                e.Detail,
                string.Join(" | ", e.Changes.Select(c => $"{c.Field}: {c.Before ?? "—"} → {c.After ?? "—"}")),
                e.Actor.Name,
                AuditLabels.ActorKind(e.Actor.Kind),
                e.Actor.Email,
                e.EntityType,
                e.EntityId.ToString(),
                e.IpAddress,
                e.CorrelationId,
            ]);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv.ToString());
        return [.. preamble, .. body];
    }

    /// <summary>
    /// Proteção contra injeção de fórmula (CSV injection): células que começam com <c>= + - @</c>,
    /// TAB ou CR ganham um apóstrofo na frente, para a planilha tratá-las como texto.
    /// </summary>
    public static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    /// <summary>Célula neutralizada e, se preciso, entre aspas (com aspas internas duplicadas).</summary>
    public static string Cell(string? value)
    {
        var text = Neutralize(value);
        return text.IndexOfAny([Separator, '"', '\n', '\r']) >= 0
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;
    }

    private static void AppendRow(StringBuilder csv, string?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
            {
                csv.Append(Separator);
            }

            csv.Append(Cell(cells[i]));
        }

        csv.Append("\r\n");
    }
}
