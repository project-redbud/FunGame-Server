using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class PostDataController(ILogger<PostDataController> logger) : ControllerBase
    {
        public static Dictionary<Guid, SocketObject> ResultDatas { get; } = [];

        private readonly ILogger<PostDataController> _logger = logger;

        [HttpPost("{username}", Name = "username")]
        public async Task<IActionResult> Post(string username, [FromBody] SocketObject obj)
        {
            try
            {
                RESTfulAPIListener? apiListener = RESTfulAPIListener.Instance;
                if (apiListener != null && apiListener.UserList.ContainsKey(username))
                {
                    RESTfulAPIModel model = (RESTfulAPIModel)apiListener.UserList[username];
                    if (model.LastRequestID == Guid.Empty)
                    {
                        Guid uid = Guid.NewGuid();
                        model.LastRequestID = uid;
                        await model.SocketMessageHandler(model.Socket, obj);
                        model.LastRequestID = Guid.Empty;
                        if (ResultDatas.TryGetValue(uid, out SocketObject list))
                        {
                            return Ok(list);
                        }
                        else
                        {
                            return BadRequest("没有任何数据返回");
                        }
                    }
                    else
                    {
                        Ok(new SocketObject(SocketMessageType.System, model.Token, "请求未执行完毕，请等待！"));
                    }
                }
                return BadRequest("没有任何数据返回");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "服务器内部错误");
                return StatusCode(500, "服务器内部错误");
            }
        }
    }
}
