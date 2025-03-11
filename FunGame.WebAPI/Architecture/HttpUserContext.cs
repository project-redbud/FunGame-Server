using System.Security.Claims;
using Milimoe.FunGame.WebAPI.Interfaces;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class HttpUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
    {
        public string Username => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    }
}
