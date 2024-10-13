using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Models;
using Milimoe.FunGame.WebAPI.Services;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController(JWTService jwtTokenService) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            string msg = "用户名或密码不正确。";
            string clientip = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
            ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 通过 RESTful API 连接至服务器，正在登录 . . .", Core.Library.Constant.InvokeMessageType.Core);
            string username = loginModel.Username;
            string password = loginModel.Password;
            RESTfulAPIListener? apiListener = RESTfulAPIListener.Instance;
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
                    RESTfulAPIModel model = new(apiListener, clientip);
                    model.SetClientName(clientip);
                    // 创建User对象
                    if (model.SQLHelper != null)
                    {
                        model.SQLHelper.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(username, password));
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
                            return Ok(new { BearerToken = token, OpenToken = model.Token });
                        }
                    }
                    else msg = "服务器暂时无法处理登录请求。";
                    await model.Send(Core.Library.Constant.SocketMessageType.Disconnect);
                }
            }

            Config.ConnectingPlayerCount--;
            ServerHelper.WriteLine(msg, Core.Library.Constant.InvokeMessageType.Core);
            return Unauthorized(msg);
        }

        [HttpPost("refresh")]
        [Authorize]
        public IActionResult Refresh([FromBody] LoginModel request)
        {
            return Ok(jwtTokenService.GenerateToken(request.Username));
        }
    }
}
