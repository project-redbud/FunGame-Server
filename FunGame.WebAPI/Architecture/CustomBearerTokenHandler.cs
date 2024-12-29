using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class CustomBearerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
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

            // 验证自定义 Token
            string name = WebAPIAuthenticator.ValidateToken(token);
            if (name == "")
            {
                await Task.Delay(1);
                return AuthenticateResult.Fail("Invalid Token.");
            }

            // 如果验证通过，创建 ClaimsIdentity
            Claim[] claims = [new Claim(ClaimTypes.Name, name)];
            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
