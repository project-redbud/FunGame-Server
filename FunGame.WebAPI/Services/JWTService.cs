using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Milimoe.FunGame.Core.Library.Constant;

namespace Milimoe.FunGame.WebAPI.Services
{
    public class JWTService(IConfiguration configuration, IMemoryCache memoryCache)
    {
        public string GenerateToken(string username)
        {
            // 创建一个包含用户信息的声明
            Claim[] claims = [
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ];

            // 获取密钥和发行者
            SymmetricSecurityKey key = new(General.DefaultEncoding.GetBytes(configuration["Jwt:Key"] ?? "undefined"));
            SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(30), // 设置过期时间
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public void RevokeToken(string token)
        {
            // 从 Token 中提取过期时间
            JwtSecurityToken jwtSecurityToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
            string? expiryClaim = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;

            if (expiryClaim != null && long.TryParse(expiryClaim, out long expiryUnixTimestamp))
            {
                DateTime expiryDateTime = DateTimeOffset.FromUnixTimeSeconds(expiryUnixTimestamp).LocalDateTime;
                TimeSpan remainingTime = expiryDateTime - DateTime.Now;

                // 将 Token 存储到 MemoryCache 中，过期时间为 Token 的剩余有效期
                memoryCache.Set(token, true, remainingTime);
            }
            else
            {
                memoryCache.Set(token, true, TimeSpan.FromMinutes(30));
            }
        }

        public bool IsTokenRevoked(string token)
        {
            // 检查 Token 是否被吊销
            return memoryCache.TryGetValue(token, out _);
        }
    }
}
