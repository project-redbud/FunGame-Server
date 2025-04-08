using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class InventoryController(RESTfulAPIModel model, ILogger<InventoryController> logger) : ControllerBase
    {
        private readonly ILogger<InventoryController> _logger = logger;

        /// <summary>
        /// ��ȡ���ױ���
        /// </summary>
        [HttpGet("getoffer")]
        public async Task<IActionResult> GetOffer([FromQuery] long offerId)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_getoffer",
                RequestType = DataRequestType.Inventory_GetOffer
            };

            try
            {
                Dictionary<string, object> data = new()
                {
                    { OffersQuery.Column_Id, offerId },
                    { "apiQuery", true }
                };
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_GetOffer, data);
                response.StatusCode = 200;
                response.Data = result;
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = "��������ʱ�޷����������";
            return StatusCode(500, response);
        }

        /// <summary>
        /// �������ױ���
        /// </summary>
        [HttpPost("makeoffer")]
        public async Task<IActionResult> MakeOffer([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_makeoffer",
                RequestType = DataRequestType.Inventory_MakeOffer
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_MakeOffer, data);
                response.StatusCode = 200;
                response.Data = result;
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = "��������ʱ�޷����������";
            return StatusCode(500, response);
        }

        /// <summary>
        /// �޸Ľ��ױ���
        /// </summary>
        [HttpPost("reviseoffer")]
        public async Task<IActionResult> ReviseOffer([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_reviseoffer",
                RequestType = DataRequestType.Inventory_ReviseOffer
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_ReviseOffer, data);
                response.StatusCode = 200;
                response.Data = result;
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = "��������ʱ�޷����������";
            return StatusCode(500, response);
        }

        /// <summary>
        /// ��Ӧ���ױ���
        /// </summary>
        [HttpPost("respondoffer")]
        public async Task<IActionResult> RespondOffer([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_respondoffer",
                RequestType = DataRequestType.Inventory_RespondOffer
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_RespondOffer, data);
                response.StatusCode = 200;
                response.Data = result;
                return Ok(response);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = "��������ʱ�޷����������";
            return StatusCode(500, response);
        }
    }
}
