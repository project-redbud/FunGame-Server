using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class DataRequestController(RESTfulAPIModel model, ILogger<DataRequestController> logger) : ControllerBase
    {
        private readonly ILogger<DataRequestController> _logger = logger;

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PayloadModel<DataRequestType> payload)
        {
            try
            {
                PayloadModel<DataRequestType> response = new()
                {
                    Event = "data_request",
                    RequestType = payload.RequestType
                };

                if (payload.RequestType == DataRequestType.RunTime_Logout || payload.RequestType == DataRequestType.Reg_Reg ||
                    payload.RequestType == DataRequestType.Login_Login || payload.RequestType == DataRequestType.Login_GetFindPasswordVerifyCode)
                {
                    response.StatusCode = 400;
                    response.Message = $"请求类型 {DataRequestSet.GetTypeString(payload.RequestType)} 不允许通过此接口处理！";
                    return StatusCode(400, response);
                }

                if (model.RequestID == Guid.Empty)
                {
                    Guid uid = Guid.NewGuid();
                    model.RequestID = uid;

                    using CancellationTokenSource cts = model.SetRequestTimeout(uid);
                    Dictionary<string, object> result = [];
                    try
                    {
                        result = await model.DataRequestController.GetResultData(payload.RequestType, payload.Data);
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

                    response.StatusCode = 200;
                    response.Data = result;
                    return Ok(response);
                }
                else
                {
                    response.StatusCode = 400;
                    response.Message = "请求未执行完毕，请等待！";
                    return BadRequest(response);
                }
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
