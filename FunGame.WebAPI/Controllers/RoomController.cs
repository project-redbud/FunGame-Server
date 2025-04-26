using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.WebAPI.Models;

namespace Milimoe.FunGame.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class RoomController(RESTfulAPIModel model, ILogger<RoomController> logger) : ControllerBase
    {
        private readonly ILogger<RoomController> _logger = logger;

        /// <summary>
        /// ��������
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateRoom([FromBody] Dictionary<string, object> data)
        {
            string msg = "��������ʱ�޷����������";
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_create",
                RequestType = DataRequestType.Main_CreateRoom
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_CreateRoom, data);
                if (result.TryGetValue("room", out object? value) && value is Room room && room.Roomid != "-1")
                {
                    response.StatusCode = 200;
                    response.Data = result;
                    return Ok(response);
                }
                msg = "��������ʧ�ܣ�";
            }
            catch (Exception e)
            {
                _logger.LogError("Error: {e}", e);
            }

            response.StatusCode = 500;
            response.Message = msg;
            return StatusCode(500, response);
        }

        /// <summary>
        /// ��ȡ/���·����б�
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetRoomList()
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_list",
                RequestType = DataRequestType.Main_UpdateRoom
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_UpdateRoom, []);
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
        /// ���뷿��
        /// </summary>
        [HttpPost("join")]
        public async Task<IActionResult> IntoRoom([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_join",
                RequestType = DataRequestType.Main_IntoRoom
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_IntoRoom, data);
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
        /// �˳�����
        /// </summary>
        [HttpPost("quit")]
        public async Task<IActionResult> QuitRoom([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_quit",
                RequestType = DataRequestType.Main_QuitRoom
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_QuitRoom, data);
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
        /// ƥ�䷿��
        /// </summary>
        [HttpPost("match")]
        public async Task<IActionResult> MatchRoom([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_match",
                RequestType = DataRequestType.Main_MatchRoom
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_MatchRoom, data);
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
        /// ����׼��״̬
        /// </summary>
        [HttpPost("ready")]
        public async Task<IActionResult> SetReady([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_ready",
                RequestType = DataRequestType.Main_Ready
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_Ready, data);
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
        /// ȡ��׼��״̬
        /// </summary>
        [HttpPost("cancelready")]
        public async Task<IActionResult> CancelReady([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_cancelready",
                RequestType = DataRequestType.Main_CancelReady
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_CancelReady, data);
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
        /// ��ʼ��Ϸ
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartGame([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_start",
                RequestType = DataRequestType.Main_StartGame
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Main_StartGame, data);
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
        /// ���·�������
        /// </summary>
        [HttpPost("updateroomsettings")]
        public async Task<IActionResult> UpdateRoomSettings([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_updatesettings",
                RequestType = DataRequestType.Room_UpdateRoomSettings
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Room_UpdateRoomSettings, data);
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
        /// ��ȡ�������������
        /// </summary>
        [HttpPost("playercount")]
        public async Task<IActionResult> GetRoomPlayerCount([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_playercount",
                RequestType = DataRequestType.Room_GetRoomPlayerCount
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Room_GetRoomPlayerCount, data);
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
        /// ���·���
        /// </summary>
        [HttpPost("updatemaster")]
        public async Task<IActionResult> UpdateRoomMaster([FromBody] Dictionary<string, object> data)
        {
            PayloadModel<DataRequestType> response = new()
            {
                Event = "room_updatemaster",
                RequestType = DataRequestType.Room_UpdateRoomMaster
            };

            try
            {
                Dictionary<string, object> result = await model.DataRequestController.GetResultData(DataRequestType.Room_UpdateRoomMaster, data);
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
