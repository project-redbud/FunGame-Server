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

            // 检查 JWT 是否被吊销
            if (jwtService.IsTokenRevoked(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"此 Token 已吊销，请重新登录以获取 Token。\"}");
                return;
            }

            await next(context);
        }
    }
}
