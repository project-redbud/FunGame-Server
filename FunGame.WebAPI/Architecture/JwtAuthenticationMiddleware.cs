using Microsoft.AspNetCore.Authorization;
using Milimoe.FunGame.WebAPI.Services;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class JwtAuthenticationMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            JWTService jwtService = context.RequestServices.GetRequiredService<JWTService>();

            // 获取 JWT Token
            string token = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

            if (token == "")
            {
                await next(context);
                return;
            }

            // 如果存在 Authorize 属性且指定了 CustomBearer 认证方案，跳过 JWT 吊销检查
            Endpoint? endpoint = context.GetEndpoint();
            IReadOnlyList<AuthorizeAttribute>? authorizeAttributes = endpoint?.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
            if (authorizeAttributes != null)
            {
                foreach (AuthorizeAttribute authorizeAttribute in authorizeAttributes)
                {
                    if (authorizeAttribute.AuthenticationSchemes == "APIBearer" || authorizeAttribute.AuthenticationSchemes == "CustomBearer")
                    {
                        await next(context);
                        return;
                    }
                }
            }

            // 检查 JWT 是否被吊销
            if (jwtService.IsTokenRevoked(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"此 Token 无效或已吊销，请重新登录以获取 Token。\"}");
                return;
            }

            await next(context);
        }
    }
}
