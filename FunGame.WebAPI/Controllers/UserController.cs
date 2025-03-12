using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Models;
using Milimoe.FunGame.WebAPI.Services;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController(RESTfulAPIListener apiListener, JWTService jwtTokenService, ILogger<AdapterController> logger) : ControllerBase
    {
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

                (string msg, RegInvokeType type, bool success) = DataRequestService.Reg(username, password, email, verifycode, clientIP);

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
                logger.LogError("Error: {e}", e);
            }
            return BadRequest("服务器暂时无法处理注册请求。");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                PayloadModel<DataRequestType> response = new()
                {
                    RequestType = DataRequestType.Login_Login
                };
                string msg = "用户名或密码不正确。";

                string clientIP = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientIP) + " 通过 RESTful API 连接至服务器，正在登录 . . .", InvokeMessageType.Core);
                string username = dto.Username;
                string password = dto.Password;

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
                                response.StatusCode = 200;
                                response.Message = "登录成功！";
                                response.Data = new()
                                {
                                    { "bearerToken", token },
                                    { "openToken", model.Token }
                                };
                                return Ok(response);
                            }
                        }
                        else msg = "服务器暂时无法处理登录请求。";
                        await model.Send(SocketMessageType.Disconnect);
                    }
                }

                Config.ConnectingPlayerCount--;
                ServerHelper.WriteLine(msg, InvokeMessageType.Core);
                response.Message = msg;
                response.StatusCode = 401;
                return Unauthorized(response);
            }
            catch (Exception e)
            {
                logger.LogError("Error: {e}", e);
            }
            return BadRequest();
        }

        [HttpPost("refresh")]
        [Authorize]
        public IActionResult Refresh()
        {
            try
            {
                string oldToken = HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

                // 吊销
                jwtTokenService.RevokeToken(oldToken);

                // 生成
                string username = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                string newToken = jwtTokenService.GenerateToken(username);

                return Ok(newToken);
            }
            catch (Exception e)
            {
                logger.LogError("Error: {e}", e);
            }
            return BadRequest();
        }
    }
}
