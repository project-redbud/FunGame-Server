using System.Collections.Concurrent;
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
    public class DataRequestController(ILogger<DataRequestController> logger) : ControllerBase
    {
        public static ConcurrentDictionary<Guid, SocketObject> ResultDatas { get; } = [];

        private readonly ILogger<DataRequestController> _logger = logger;

        [HttpPost("{username}")]
        public async Task<IActionResult> Post(string username, [FromBody] PayloadModel<DataRequestType> payload)
        {
            try
            {
                RESTfulAPIListener? apiListener = RESTfulAPIListener.Instance;
                if (apiListener != null && apiListener.UserList.ContainsKey(username))
                {
                    RESTfulAPIModel model = (RESTfulAPIModel)apiListener.UserList[username];
                    if (model.RequestID == Guid.Empty)
                    {
                        await Task.CompletedTask;
                        return Ok(payload);
                    }
                    else
                    {
                        return Ok(new SocketObject(SocketMessageType.System, model.Token, "请求未执行完毕，请等待！"));
                    }
                }
                return BadRequest("没有任何数据返回。");
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
                return StatusCode(500, "服务器内部错误。");
            }
        }
    }
}
