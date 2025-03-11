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
                    RequestType = payload.RequestType
                };
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
                        _logger.LogWarning("����ʱ��");
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
                    response.Message = "����δִ����ϣ���ȴ���";
                    return BadRequest(response);
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
                return StatusCode(500);
            }
        }
    }
}
