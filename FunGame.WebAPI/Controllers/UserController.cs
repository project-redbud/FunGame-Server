using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Constant;
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
        [Authorize(AuthenticationSchemes = "APIBearer")]
        public IActionResult Reg([FromBody] RegDTO dto)
        {
            // 因为注册 API 不需要先登录，所以需要进行 API Bearer Token 验证，防止 API 滥用
            // API Bearer Token 保存在数据库 apitokens 表中，由服务器管理员配置
            try
            {
                string clientIP = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientIP) + " 通过 RESTful API 注册账号 . . .", InvokeMessageType.Core);
                string username = dto.Username;
                string password = dto.Password;
                string email = dto.Email;
                string verifycode = dto.VerifyCode;

                (string msg, RegInvokeType type, bool success) = DataRequestService.Reg(apiListener, username, password, email, verifycode, clientIP);

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
            Config.ConnectingPlayerCount++;
            try
            {
                PayloadModel<DataRequestType> response = new()
                {
                    RequestType = DataRequestType.Login_Login
                };
                string msg = "服务器暂时无法处理登录请求。";

                string clientIP = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientIP) + " 通过 RESTful API 连接至服务器，正在登录 . . .", InvokeMessageType.Core);
                string username = dto.Username;
                string password = dto.Password;

                RESTfulAPIModel? model = await CheckConnection(username, clientIP);
                if (model != null)
                {
                    // 预登录
                    (bool success, DataSet dsUser, msg, Guid key) = DataRequestService.PreLogin(this, username, password);
                    if (success)
                    {
                        model.PreLogin(dsUser, key);
                        // 确认登录
                        await model.CheckLogin();
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
                    await model.Send(SocketMessageType.Disconnect);
                }

                Config.ConnectingPlayerCount--;
                response.Message = msg;
                response.StatusCode = 401;
                return Unauthorized(response);
            }
            catch (Exception e)
            {
                Config.ConnectingPlayerCount--;
                logger.LogError("Error: {e}", e);
            }
            return BadRequest();
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> LogOut()
        {
            try
            {
                PayloadModel<DataRequestType> response = new()
                {
                    RequestType = DataRequestType.RunTime_Logout
                };
                string username = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

                if (apiListener.UserList.ContainsKey(username))
                {
                    RESTfulAPIModel model = (RESTfulAPIModel)apiListener.UserList[username];
                    await model.Send(SocketMessageType.Disconnect);
                    RevokeToken();
                    model.GetUsersCount();
                    response.Message = "你已成功退出登录！ ";
                    response.StatusCode = 200;
                    return Ok(response);
                }

                response.Message = "退出登录失败！";
                response.StatusCode = 400;
                return BadRequest(response);
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
                RevokeToken();
                string username = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                if (username.Trim() != "")
                {
                    return Ok(jwtTokenService.GenerateToken(username));
                }
            }
            catch (Exception e)
            {
                logger.LogError("Error: {e}", e);
            }
            return BadRequest();
        }

        private async Task<RESTfulAPIModel?> CheckConnection(string username, string clientIP)
        {
            if (apiListener != null)
            {
                // 移除旧模型
                if (apiListener.UserList.ContainsKey(username))
                {
                    await apiListener.UserList[username].Send(SocketMessageType.Disconnect);
                    RevokeToken();
                }
                // 创建新模型
                if (!apiListener.UserList.ContainsKey(username))
                {
                    RESTfulAPIModel model = new(apiListener, clientIP);
                    model.SetClientName(clientIP);
                    return model;
                }
            }
            return null;
        }

        private void RevokeToken()
        {
            // 吊销令牌
            string oldToken = HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            if (oldToken.Trim() != "") jwtTokenService.RevokeToken(oldToken);
        }
    }
}
