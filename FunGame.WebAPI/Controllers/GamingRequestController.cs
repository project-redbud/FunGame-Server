using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class GamingRequestController(RESTfulAPIModel model, ILogger<GamingRequestController> logger) : ControllerBase
    {
        private readonly ILogger<GamingRequestController> _logger = logger;

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PayloadModel<GamingType> payload)
        {
            try
            {
                PayloadModel<GamingType> response = new()
                {
                    RequestType = payload.RequestType
                };
                if (model.RequestID == Guid.Empty)
                {
                    if (model.NowGamingServer is null)
                    {
                        response.StatusCode = 400;
                        response.Message = "û���������е���Ϸ��������";
                        return BadRequest(response);
                    }
                    else
                    {
                        Guid uid = Guid.NewGuid();
                        model.RequestID = uid;

                        using CancellationTokenSource cts = model.SetRequestTimeout(uid);
                        Dictionary<string, object> result = [];
                        try
                        {
                            result = await model.NowGamingServer.GamingMessageHandler(model, payload.RequestType, payload.Data);
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
                }
                else
                {
                    response.StatusCode = 400;
                    response.Message = "����δִ����ϣ���ȴ���";
                    return BadRequest(response);
                }
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
        }
    }
}
