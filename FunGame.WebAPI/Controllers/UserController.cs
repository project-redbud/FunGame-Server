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
        /// <summary>
        /// 因为注册 API 不需要先登录，所以需要进行 API Bearer Token 验证，防止 API 滥用。<para/>
        /// API Bearer Token 保存在数据库 apitokens 表中，服务器在首次运行、初始化数据库时会自动生成一个 API Bearer Token。<para/>
        /// 默认的 TokenID 为 <see cref="FunGameSystem.FunGameWebAPITokenID"/>，如需使用此 ID 对应的秘钥可以联系服务器管理员。<para/>
        /// 因此，注册账号通常需要使用服务器运营者提供的客户端。<para/>
        /// 除此之外，已注册过并登录的用户可以为自己生成一个 API Bearer Token，用于创建客户端并访问使用 APIBearer 验证方案的 API 端口。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("register")]
        [Authorize(AuthenticationSchemes = "APIBearer")]
        public IActionResult Register([FromBody] RegDTO dto)
        {
            string msg = "服务器暂时无法处理注册请求。";
            PayloadModel<DataRequestType> response = new()
            {
                Event = "user_register",
                RequestType = DataRequestType.Reg_Reg
            };

            try
            {
                string clientIP = HttpContext.Connection.RemoteIpAddress?.ToString() + ":" + HttpContext.Connection.RemotePort;
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientIP) + " 通过 RESTful API 注册账号 . . .", InvokeMessageType.Core);
                string username = dto.Username;
                string password = dto.Password;
                string email = dto.Email;
                string verifycode = dto.VerifyCode;

                (msg, RegInvokeType type, bool success) = DataRequestService.Reg(apiListener, username, password, email, verifycode, clientIP);

                response.StatusCode = 200;
                response.Message = msg;
                response.Data = new()
                {
                    { "success", success },
                    { "type", type }
                };
                return Ok(response);
            }
            catch (Exception e)
            {
                logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = msg;
            return StatusCode(500, response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            string msg = "服务器暂时无法处理登录请求。";
            Config.ConnectingPlayerCount++;
            PayloadModel<DataRequestType> response = new()
            {
                Event = "user_login",
                RequestType = DataRequestType.Login_Login
            };

            try
            {
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

            response.StatusCode = 500;
            response.Message = msg;
            return StatusCode(500, response);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> LogOut()
        {
            string msg = "退出登录失败！服务器可能暂时无法处理此请求。";
            PayloadModel<DataRequestType> response = new()
            {
                Event = "user_logout",
                RequestType = DataRequestType.RunTime_Logout
            };

            try
            {
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
            }
            catch (Exception e)
            {
                logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = msg;
            return StatusCode(500, response);
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
            return StatusCode(500);
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
