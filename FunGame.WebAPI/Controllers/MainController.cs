using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class MainController(RESTfulAPIModel model, ILogger<MainController> logger) : ControllerBase
    {
        private readonly ILogger<MainController> _logger = logger;

        /// <summary>
        /// 获取公告
        /// </summary>
        [HttpGet("notice")]
        public async Task<IActionResult> GetNotice()
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "main_getnotice",
                RequestType = DataRequestType.Main_GetNotice
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_GetNotice, []);
                response.StatusCode = 200;
                response.Data = result;
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = "服务器暂时无法处理此请求。";
            return StatusCode(500, response);
        }

        /// <summary>
        /// 发送聊天消息
        /// </summary>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "main_chat",
                RequestType = DataRequestType.Main_Chat
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_Chat, data);
                response.StatusCode = 200;
                response.Data = result;
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = "服务器暂时无法处理此请求。";
            return StatusCode(500, response);
        }
    }
}
