using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class AdapterController(RESTfulAPIModel model, ILogger<AdapterController> logger) : ControllerBase
    {
        public static ConcurrentDictionary<Guid, SocketObject> ResultDatas { get; } = [];

        private readonly ILogger<AdapterController> _logger = logger;

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SocketObject obj)
        {
            try
            {
                if (model.RequestID == Guid.Empty)
                {
                    Guid uid = Guid.NewGuid();
                    model.RequestID = uid;

                    using CancellationTokenSource cts = model.SetRequestTimeout(uid);

                    try
                    {
                        await model.SocketMessageHandler(model.Socket, obj);
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("请求超时。");
                        return StatusCode(408);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error: {e}", e);
                        return StatusCode(500);
                    }
                    finally
                    {
                        model.RequestID = Guid.Empty;
                    }

                    if (ResultDatas.TryRemove(uid, out SocketObject response))
                    {
                        return Ok(response);
                    }
                }
                return BadRequest(new SocketObject(SocketMessageType.System, model.Token, "请求未执行完毕，请等待！"));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("请求超时。");
                return StatusCode(408);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
                return StatusCode(500);
            }
        }
    }
}
