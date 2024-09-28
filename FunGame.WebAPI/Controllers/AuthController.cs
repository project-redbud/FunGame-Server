using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Models;
using Milimoe.FunGame.WebAPI.Services;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController(JWTService jwtTokenService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            string username = loginModel.Username;
            string password = loginModel.Password;
            RESTfulAPIListener? apiListener = Singleton.Get<RESTfulAPIListener>();
            if (apiListener != null)
            {
                // 移除旧模型
                if (apiListener.UserList.ContainsKey(username))
                {
                    await apiListener.UserList[username].Send(Core.Library.Constant.SocketMessageType.Disconnect);
                }
                // 创建新模型
                if (!apiListener.UserList.ContainsKey(username))
                {
                    Config.ConnectingPlayerCount++;
                    string clientip = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                    RESTfulAPIModel model = new(apiListener, clientip);
                    model.SetClientName(clientip);
                    // 创建User对象
                    model.SQLHelper?.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(username, password));
                    Core.Entity.User user = Factory.GetUser(model.SQLHelper?.DataSet ?? new());
                    if (user.Id != 0)
                    {
                        model.User = user;
                        // 检查有没有重复登录的情况
                        await model.ForceLogOutDuplicateLogonUser();
                        // 添加至玩家列表
                        model.AddUser();
                        model.GetUsersCount();
                        string token = jwtTokenService.GenerateToken(username);
                        Config.ConnectingPlayerCount--;
                        return Ok(new { BearerToken = token, InternalToken = model.Token });
                    }
                    await model.Send(Core.Library.Constant.SocketMessageType.Disconnect);
                }
            }

            Config.ConnectingPlayerCount--;
            return Unauthorized("无效的认证");
        }

        [HttpPost("refresh")]
        [Authorize]
        public IActionResult Refresh([FromBody] LoginModel request)
        {
            return Ok(jwtTokenService.GenerateToken(request.Username));
        }
    }
}
