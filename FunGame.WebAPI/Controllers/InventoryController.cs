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
        /// 获取商店内容
        /// </summary>
        [HttpGet("getstore")]
        public async Task<IActionResult> GetStore(long[] ids)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_getstore",
                RequestType = DataRequestType.Inventory_GetStore
            };

            try
            {
                Dictionary<string, object> data = new()
                {
                    { "ids", ids }
                };
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_GetStore, data);
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
        /// 获取市场内容（用户）
        /// </summary>
        [HttpGet("getmarketbyuser")]
        public async Task<IActionResult> GetMarketByUser(long userid, MarketItemState state = MarketItemState.Listed)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_getmarket",
                RequestType = DataRequestType.Inventory_GetMarket
            };

            try
            {
                Dictionary<string, object> data = new()
                {
                    { "users", new long[] { userid } },
                    { "state", state }
                };
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_GetMarket, data);
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
        /// 获取市场内容（物品）
        /// </summary>
        [HttpGet("getmarketbyitem")]
        public async Task<IActionResult> GetMarketByItem(long itemid, MarketItemState state = MarketItemState.Listed)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_getmarket",
                RequestType = DataRequestType.Inventory_GetMarket
            };

            try
            {
                Dictionary<string, object> data = new()
                {
                    { "state", state },
                    { "items", new long[] { itemid } }
                };
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_GetMarket, data);
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
        /// 商店购买物品
        /// </summary>
        [HttpPost("storebuy")]
        public async Task<IActionResult> StoreBuy([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_storebuy",
                RequestType = DataRequestType.Inventory_StoreBuy
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_StoreBuy, data);
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
        /// 市场购买物品
        /// </summary>
        [HttpPost("marketbuy")]
        public async Task<IActionResult> MarketBuy([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_marketbuy",
                RequestType = DataRequestType.Inventory_MarketBuy
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_MarketBuy, data);
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
        /// 更新库存
        /// </summary>
        [HttpPost("updateinventory")]
        public async Task<IActionResult> UpdateInventory([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_updateinventory",
                RequestType = DataRequestType.Inventory_UpdateInventory
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_UpdateInventory, data);
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
        /// 使用库存物品
        /// </summary>
        [HttpPost("useitem")]
        public async Task<IActionResult> Use([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_use",
                RequestType = DataRequestType.Inventory_Use
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_Use, data);
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
        /// 商店出售物品
        /// </summary>
        [HttpPost("storesell")]
        public async Task<IActionResult> StoreSell([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_storesell",
                RequestType = DataRequestType.Inventory_StoreSell
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_StoreSell, data);
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
        /// 市场出售物品
        /// </summary>
        [HttpPost("marketsell")]
        public async Task<IActionResult> MarketSell([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_marketsell",
                RequestType = DataRequestType.Inventory_MarketSell
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_MarketSell, data);
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
        /// 下架市场物品
        /// </summary>
        [HttpPost("marketdelist")]
        public async Task<IActionResult> MarketDelist([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_marketdelist",
                RequestType = DataRequestType.Inventory_MarketDelist
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_MarketDelist, data);
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
        /// 更新市场价格
        /// </summary>
        [HttpPost("updatemarketprice")]
        public async Task<IActionResult> UpdateMarketPrice([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "inventory_updatemarketprice",
                RequestType = DataRequestType.Inventory_UpdateMarketPrice
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Inventory_UpdateMarketPrice, data);
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
        /// 获取交易报价
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
            response.Message = "服务器暂时无法处理此请求。";
            return StatusCode(500, response);
        }

        /// <summary>
        /// 创建交易报价
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
            response.Message = "服务器暂时无法处理此请求。";
            return StatusCode(500, response);
        }

        /// <summary>
        /// 修改交易报价
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
            response.Message = "服务器暂时无法处理此请求。";
            return StatusCode(500, response);
        }

        /// <summary>
        /// 回应交易报价
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
            response.Message = "服务器暂时无法处理此请求。";
            return StatusCode(500, response);
        }
    }
}
