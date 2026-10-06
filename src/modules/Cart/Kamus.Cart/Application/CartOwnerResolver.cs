using System.Security.Cryptography;
using Kamus.Shared.Security;
using Microsoft.AspNetCore.Http;

namespace Kamus.Cart.Application;

/// <summary>Descobre de quem é o carrinho da requisição atual e cuida do cookie de visitante.</summary>
internal sealed class CartOwnerResolver(IHttpContextAccessor accessor)
{
    public const string VisitorCookie = "kamus_vid";

    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("Sem HttpContext.");

    /// <summary>Dono atual. Sem login e sem cookie, retorna null (carrinho vazio, nada a criar).</summary>
    public CartOwner? Current()
    {
        if (Http.User.GetCustomerId() is { } customerId)
        {
            return CartOwner.Customer(customerId);
        }

        return VisitorId() is { } visitor ? CartOwner.Visitor(visitor) : null;
    }

    /// <summary>Dono atual, criando o cookie de visitante se ainda não existir.</summary>
    public CartOwner CurrentOrCreate()
    {
        if (Current() is { } owner)
        {
            return owner;
        }

        var visitorId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        Http.Response.Cookies.Append(VisitorCookie, visitorId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Http.Request.IsHttps,
            MaxAge = TimeSpan.FromDays(365),
            IsEssential = true,
        });
        return CartOwner.Visitor(visitorId);
    }

    public string? VisitorId() =>
        Http.Request.Cookies.TryGetValue(VisitorCookie, out var value) && value.Length == 32 && value.All(char.IsAsciiHexDigitLower)
            ? value
            : null;
}
