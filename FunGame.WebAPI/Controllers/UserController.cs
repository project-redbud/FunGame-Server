using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Models;
using Milimoe.FunGame.WebAPI.Services;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController(JWTService jwtTokenService, ILogger<AdapterController> logger) : ControllerBase
    {
        private readonly ILogger<AdapterController> _logger = logger;

        [HttpPost("reg")]
        public IActionResult Reg([FromBody] RegDTO dto)
        {
            try
            {
                string clientIP = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientIP) + " 通过 RESTful API 注册账号 . . .", InvokeMessageType.Core);

                string username = dto.Username;
                string password = dto.Password;
                string email = dto.Email;
                string verifycode = dto.VerifyCode;

                (string msg, RegInvokeType type, bool success) = DataRequestController<RESTfulAPI>.Reg(username, password, email, verifycode, clientIP);

                return Ok(new PayloadModel<DataRequestType>()
                {
                    RequestType = DataRequestType.Reg_Reg,
                    StatusCode = 200,
                    Message = msg,
                    Data = new()
                    {
                        { "success", success },
                        { "type", type }
                    }
                });
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }
            return BadRequest("服务器暂时无法处理注册请求。");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                string msg = "用户名或密码不正确。";
                string clientIP = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientIP) + " 通过 RESTful API 连接至服务器，正在登录 . . .", InvokeMessageType.Core);
                string username = dto.Username;
                string password = dto.Password;
                RESTfulAPIListener? apiListener = RESTfulAPIListener.Instance;
                if (apiListener != null)
                {
                    // 移除旧模型
                    if (apiListener.UserList.ContainsKey(username))
                    {
                        await apiListener.UserList[username].Send(SocketMessageType.Disconnect);
                    }
                    // 创建新模型
                    if (!apiListener.UserList.ContainsKey(username))
                    {
                        Config.ConnectingPlayerCount++;
                        RESTfulAPIModel model = new(apiListener, clientIP);
                        model.SetClientName(clientIP);
                        // 创建User对象
                        if (model.SQLHelper != null)
                        {
                            model.SQLHelper.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(model.SQLHelper, username, password));
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
                        await model.Send(SocketMessageType.Disconnect);
                    }
                }

                Config.ConnectingPlayerCount--;
                ServerHelper.WriteLine(msg, InvokeMessageType.Core);
                return Unauthorized(msg);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }
            return BadRequest("服务器暂时无法处理登录请求。");
        }

        [HttpPost("refresh")]
        [Authorize]
        public IActionResult Refresh([FromBody] LoginDTO dto)
        {
            try
            {
                return Ok(jwtTokenService.GenerateToken(dto.Username));
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }
            return BadRequest();
        }
    }
}
