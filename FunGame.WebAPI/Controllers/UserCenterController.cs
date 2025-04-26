using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class UserCenterController(RESTfulAPIModel model, ILogger<UserCenterController> logger) : ControllerBase
    {
        private readonly ILogger<UserCenterController> _logger = logger;

        /// <summary>
        /// �����û�
        /// </summary>
        [HttpPut("updateuser")]
        public async Task<IActionResult> UpdateUser(Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "user_update",
                RequestType = DataRequestType.UserCenter_UpdateUser
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.UserCenter_UpdateUser, data);
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
        /// ��������
        /// </summary>
        [HttpPut("updatepassword")]
        public async Task<IActionResult> UpdatePassword(Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "user_updatepassword",
                RequestType = DataRequestType.UserCenter_UpdatePassword
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.UserCenter_UpdatePassword, data);
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
        /// ÿ��ǩ��
        /// </summary>
        [HttpPost("dailysignin")]
        public async Task<IActionResult> DailySignIn(Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "user_dailysignin",
                RequestType = DataRequestType.UserCenter_DailySignIn
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.UserCenter_DailySignIn, data);
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
