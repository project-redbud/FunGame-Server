using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Milimoe.FunGame.Server.Services;

namespace Milimoe.FunGame.WebAPI.Services
{
    public class APIBearerAuthenticationHandler(IMemoryCache memoryCache, IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // 检查是否有 Authorization Header
            if (!Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues value))
            {
                return AuthenticateResult.Fail("Authorization header is missing.");
            }

            string authorizationHeader = value.ToString();
            if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Fail("Invalid Authorization header format.");
            }

            string token = authorizationHeader["Bearer ".Length..].Trim();

            // 验证 API Bearer Token
            string key;
            if (memoryCache.TryGetValue(FunGameSystem.FunGameWebAPITokenID, out object? cacheValue) && cacheValue is string str)
            {
                key = str;
            }
            else
            {
                key = FunGameSystem.GetAPISecretKey(FunGameSystem.FunGameWebAPITokenID);
                memoryCache.Set(FunGameSystem.FunGameWebAPITokenID, key, TimeSpan.FromMinutes(5));
            }
            if (key == "" || token != key)
            {
                await Task.CompletedTask;
                return AuthenticateResult.Fail("Invalid Token.");
            }

            // 如果验证通过，创建 ClaimsIdentity
            Claim[] claims = [new Claim(ClaimTypes.Name, "FunGame Web API Claim")];
            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
