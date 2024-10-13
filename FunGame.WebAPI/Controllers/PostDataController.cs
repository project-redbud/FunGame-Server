using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Api.Utility;
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
                RESTfulAPIListener? apiListener = Singleton.Get<RESTfulAPIListener>();
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
                            return NotFound();
                        }
                    }
                    else
                    {
                        Ok(new SocketObject(SocketMessageType.System, model.Token, "����δִ����ϣ���ȴ���"));
                    }
                }
                return NotFound();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during post data");
                return StatusCode(500, "�������ڲ�����");
            }
        }
    }
}