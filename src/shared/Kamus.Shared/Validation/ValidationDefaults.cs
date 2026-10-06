using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace Kamus.Shared.Validation;

public static class ValidationDefaults
{
    /// <summary>Mensagens em pt-BR e nomes de campos em camelCase, iguais ao JSON da API.</summary>
    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
    }
}
