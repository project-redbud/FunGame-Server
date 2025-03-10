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
    public class AdapterController(ILogger<AdapterController> logger) : ControllerBase
    {
        public static ConcurrentDictionary<Guid, SocketObject> ResultDatas { get; } = [];

        private readonly ILogger<AdapterController> _logger = logger;

        [HttpPost("{username}")]
        public async Task<IActionResult> Post(string username, [FromBody] SocketObject obj)
        {
            try
            {
                RESTfulAPIListener? apiListener = RESTfulAPIListener.Instance;
                if (apiListener != null && apiListener.UserList.ContainsKey(username))
                {
                    RESTfulAPIModel model = (RESTfulAPIModel)apiListener.UserList[username];
                    if (model.RequestID == Guid.Empty)
                    {
                        Guid uid = Guid.NewGuid();
                        model.RequestID = uid;

                        int timeoutMilliseconds = 60 * 1000;
                        CancellationTokenSource cts = new(timeoutMilliseconds);
                        cts.Token.Register(() =>
                        {
                            if (model.RequestID == uid)
                            {
                                model.RequestID = Guid.Empty;
                                _logger.LogWarning("请求 {uid} 超时，已释放 RequestID。", uid);
                            }
                            cts.Dispose();
                        });

                        await model.SocketMessageHandler(model.Socket, obj);

                        cts.Cancel();
                        cts.Dispose();
                        model.RequestID = Guid.Empty;

                        if (ResultDatas.TryGetValue(uid, out SocketObject response))
                        {
                            return Ok(response);
                        }
                        else
                        {
                            return BadRequest("没有任何数据返回。");
                        }
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
