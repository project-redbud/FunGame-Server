using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Milimoe.FunGame.Core.Library.Constant;

namespace Milimoe.FunGame.WebAPI.Services
{
    public class JWTService(IConfiguration configuration)
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
    }
}
