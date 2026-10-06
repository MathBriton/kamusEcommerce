namespace Kamus.Payments.Contracts;

/// <summary>Contrato público do módulo Payments.</summary>
public interface IPaymentService
{
    /// <summary>Cria a cobrança no provedor. O resultado chega depois, por webhook, como evento.</summary>
    Task<PaymentStarted> StartAsync(StartPayment request, CancellationToken cancellationToken = default);
}

public sealed record StartPayment(Guid OrderId, string OrderNumber, decimal Amount, string CardNumber);

public sealed record PaymentStarted(Guid PaymentId);

/// <summary>Evento: o provedor confirmou o pagamento.</summary>
public sealed record PaymentApproved(Guid OrderId, Guid PaymentId);

/// <summary>Evento: o provedor recusou o pagamento.</summary>
public sealed record PaymentDeclined(Guid OrderId, Guid PaymentId, string Reason);

/// <summary>Cartões de teste do FakePay. Qualquer outro número válido (Luhn) é aprovado.</summary>
public static class FakePayTestCards
{
    public const string Approved = "4242424242424242";
    public const string Declined = "4000000000000002";
    public const string Timeout = "4000000000000119";
    public const string DuplicateWebhook = "4000000000000259";
}

/// <summary>Regras de formato de cartão, compartilhadas para validação antecipada no checkout.</summary>
public static class CardNumberRules
{
    public static string Normalize(string value) => new([.. value.Where(char.IsAsciiDigit)]);

    /// <summary>Validação de Luhn (dígito verificador de cartões).</summary>
    public static bool IsValid(string? value)
    {
        if (value is null)
        {
            return false;
        }

        var digits = Normalize(value);
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
        }

        return sum % 10 == 0;
    }
}
