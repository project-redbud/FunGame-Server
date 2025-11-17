using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Common.Event;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services;
using ProjectRedbud.FunGame.SQLQueryExtension;

namespace Milimoe.FunGame.Server.Controller
{
    /// <summary>
    /// <typeparamref name="T"/> 继承自 <see cref="ISocketMessageProcessor"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataRequestController<T> where T : ISocketMessageProcessor
    {
        public ServerModel<T> Server { get; }
        public SQLHelper? SQLHelper => Server.SQLHelper;
        public MailSender? MailSender => Server.MailSender;
        public Authenticator? Authenticator { get; }
        public DataRequestType LastRequest => _lastRequest;

        private DataRequestType _lastRequest = DataRequestType.UnKnown;
        private readonly bool[] _isReadyCheckCD = [false, false];
        protected string _username = "";
        protected bool _isMatching;

        /// <summary>
        /// 数据请求控制器
        /// </summary>
        /// <param name="server"></param>
        public DataRequestController(ServerModel<T> server)
        {
            Server = server;
            if (SQLHelper != null) Authenticator = new(Server, SQLHelper, MailSender);
        }

        /// <summary>
        /// 处理客户端的数据请求
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> GetResultData(DataRequestType type, Dictionary<string, object> data)
        {
            Dictionary<string, object> result = [];
            _lastRequest = type;
            ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_lastRequest), InvokeMessageType.DataRequest);

            switch (type)
            {
                case DataRequestType.UnKnown:
                    break;

                case DataRequestType.RunTime_Logout:
                    LogOut(data, result);
                    break;

                case DataRequestType.Main_GetNotice:
                    GetServerNotice(result);
                    break;

                case DataRequestType.Main_CreateRoom:
                    CreateRoom(data, result);
                    break;

                case DataRequestType.Main_UpdateRoom:
                    UpdateRoom(result);
                    break;

                case DataRequestType.Main_IntoRoom:
                    await IntoRoom(data, result);
                    break;

                case DataRequestType.Main_QuitRoom:
                    await QuitRoom(data, result);
                    break;

                case DataRequestType.Main_MatchRoom:
                    MatchRoom(data, result);
                    break;

                case DataRequestType.Main_Chat:
                    await Chat(data);
                    break;

                case DataRequestType.Main_Ready:
                    SetReady(data, result);
                    break;

                case DataRequestType.Main_CancelReady:
                    CancelReady(data, result);
                    break;

                case DataRequestType.Main_StartGame:
                    StartGame(data, result);
                    break;

                case DataRequestType.Reg_Reg:
                    Reg(data, result);
                    break;

                case DataRequestType.Login_Login:
                    await Login(data, result);
                    break;

                case DataRequestType.Login_GetFindPasswordVerifyCode:
                    ForgetPassword(data, result);
                    break;

                case DataRequestType.Login_UpdatePassword:
                    UpdatePassword(data, result);
                    break;

                case DataRequestType.Room_UpdateRoomSettings:
                    UpdateRoomSettings(data, result);
                    break;

                case DataRequestType.Room_GetRoomPlayerCount:
                    GetRoomPlayerCount(data, result);
                    break;

                case DataRequestType.Room_UpdateRoomMaster:
                    await UpdateRoomMaster(data, result);
                    break;

                case DataRequestType.UserCenter_UpdateUser:
                    UpdateUser(data, result);
                    break;

                case DataRequestType.UserCenter_UpdatePassword:
                    UpdatePassword(data, result);
                    break;

                case DataRequestType.UserCenter_DailySignIn:
                    DailySignIn(result);
                    break;

                case DataRequestType.Inventory_GetStore:
                    GetStore(data, result);
                    break;

                case DataRequestType.Inventory_GetMarket:
                    GetMarket(data, result);
                    break;

                case DataRequestType.Inventory_StoreBuy:
                    StoreBuy(data, result);
                    break;

                case DataRequestType.Inventory_MarketBuy:
                    MarketBuy(data, result);
                    break;

                case DataRequestType.Inventory_UpdateInventory:
                    UpdateInventory(data, result);
                    break;

                case DataRequestType.Inventory_Use:
                    Use(data, result);
                    break;

                case DataRequestType.Inventory_StoreSell:
                    StoreSell(data, result);
                    break;

                case DataRequestType.Inventory_MarketSell:
                    MarketSell(data, result);
                    break;

                case DataRequestType.Inventory_MarketDelist:
                    MarketDelist(data, result);
                    break;

                case DataRequestType.Inventory_UpdateMarketPrice:
                    UpdateMarketPrice(data, result);
                    break;

                case DataRequestType.Inventory_GetOffer:
                    GetOffer(data, result);
                    break;

                case DataRequestType.Inventory_MakeOffer:
                    MakeOffer(data, result);
                    break;

                case DataRequestType.Inventory_ReviseOffer:
                    ReviseOffer(data, result);
                    break;

                case DataRequestType.Inventory_RespondOffer:
                    RespondOffer(data, result);
                    break;

                default:
                    break;
            }

            return result;
        }

        #region RunTime

        /// <summary>
        /// 退出登录
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void LogOut(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "";
            Guid key = Guid.Empty;
            if (requestData.Count >= 1)
            {
                key = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "key");

                GeneralEventArgs eventArgs = new();
                FunGameSystem.ServerPluginLoader?.OnBeforeLogoutEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeLogoutEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    msg = DataRequestService.GetPluginCancelString(DataRequestType.RunTime_Logout, eventArgs);
                    ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else if (Server.IsLoginKey(key))
                {
                    // 从玩家列表移除
                    Server.RemoveUser();
                    Server.GetUsersCount();
                    msg = "你已成功退出登录！ ";
                }
                else eventArgs.Success = false;

                FunGameSystem.ServerPluginLoader?.OnBeforeLogoutEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeLogoutEvent(this, eventArgs);
            }
            resultData.Add("msg", msg);
            resultData.Add("key", key);
        }

        #endregion

        #region Main

        /// <summary>
        /// 获取公告
        /// </summary>
        /// <param name="resultData"></param>
        private static void GetServerNotice(Dictionary<string, object> resultData)
        {
            resultData.Add("notice", Config.ServerNotice);
        }

        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void CreateRoom(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            Room room = General.HallInstance;
            if (requestData.Count >= 3)
            {
                RoomType type = DataRequest.GetDictionaryJsonObject<RoomType>(requestData, "roomType");
                string gameModule = DataRequest.GetDictionaryJsonObject<string>(requestData, "moduleServer") ?? "";
                string gameMap = DataRequest.GetDictionaryJsonObject<string>(requestData, "map") ?? "";
                bool isRank = DataRequest.GetDictionaryJsonObject<bool>(requestData, "isRank");
                User user = DataRequest.GetDictionaryJsonObject<User>(requestData, "master") ?? Factory.GetUser();
                string password = DataRequest.GetDictionaryJsonObject<string>(requestData, "password") ?? "";
                int maxusers = DataRequest.GetDictionaryJsonObject<int>(requestData, "maxUsers");

                RoomEventArgs eventArgs = new(type, password);
                FunGameSystem.ServerPluginLoader?.OnBeforeCreateRoomEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeCreateRoomEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Main_CreateRoom, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                    resultData.Add("room", room);
                    return;
                }
                else
                {
                    ServerHelper.WriteLine("[CreateRoom] " + RoomSet.GetTypeString(type) + " (" + string.Join(", ", [gameModule, gameMap]) + ")", InvokeMessageType.DataRequest);
                    if (gameModule == "" || gameMap == "" || FunGameSystem.GameModuleLoader is null || !FunGameSystem.GameModuleLoader.ModuleServers.ContainsKey(gameModule) || !FunGameSystem.GameModuleLoader.Maps.ContainsKey(gameMap))
                    {
                        ServerHelper.WriteLine("缺少对应的模组或地图，无法创建房间。");
                        resultData.Add("room", room);
                        return;
                    }

                    if (user.Id != 0)
                    {
                        string roomid;
                        while (true)
                        {
                            // 防止重复
                            roomid = Verification.CreateVerifyCode(VerifyCodeType.MixVerifyCode, 7).ToUpper();
                            if (FunGameSystem.RoomList.GetRoom(roomid).Roomid == "-1")
                            {
                                break;
                            }
                        }
                        if (roomid != "-1" && SQLHelper != null)
                        {
                            SQLHelper.Execute(RoomQuery.Insert_CreateRoom(SQLHelper, roomid, user.Id, type, gameModule, gameMap, isRank, password, maxusers));
                            if (SQLHelper.Result == SQLResult.Success)
                            {
                                ServerHelper.WriteLine("[CreateRoom] Master: " + user.Username + " RoomID: " + roomid);
                                DataRow? dr = SQLHelper.ExecuteDataRow(RoomQuery.Select_RoomByRoomId(SQLHelper, roomid));
                                if (dr != null)
                                {
                                    room = Factory.GetRoom(dr, user);
                                    FunGameSystem.RoomList.AddRoom(room);
                                }
                            }
                        }
                    }
                }

                eventArgs.Success = room.Roomid != "-1";
                FunGameSystem.ServerPluginLoader?.OnAfterCreateRoomEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterCreateRoomEvent(this, eventArgs);
            }
            resultData.Add("room", room);
        }

        /// <summary>
        /// 更新房间列表
        /// </summary>
        /// <param name="resultData"></param>
        private static void UpdateRoom(Dictionary<string, object> resultData)
        {
            resultData.Add("rooms", FunGameSystem.RoomList.ListRoom); // 传RoomList
        }

        /// <summary>
        /// 退出房间，并更新房主
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private async Task QuitRoom(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            if (requestData.Count >= 2)
            {
                string roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
                bool isMaster = DataRequest.GetDictionaryJsonObject<bool>(requestData, "isMaster");

                if (roomid != "-1" && FunGameSystem.RoomList.Exists(roomid))
                {
                    Room room = FunGameSystem.RoomList[roomid];
                    RoomEventArgs eventArgs = new(room);
                    FunGameSystem.ServerPluginLoader?.OnBeforeQuitRoomEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnBeforeQuitRoomEvent(this, eventArgs);
                    if (eventArgs.Cancel)
                    {
                        ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Main_QuitRoom, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                    }
                    else
                    {
                        result = await Server.QuitRoom(roomid, isMaster);
                    }

                    eventArgs.Success = result;
                    FunGameSystem.ServerPluginLoader?.OnAfterQuitRoomEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnAfterQuitRoomEvent(this, eventArgs);
                }
            }
            resultData.Add("result", result);
        }

        /// <summary>
        /// 进入房间
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private async Task IntoRoom(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            if (requestData.Count >= 1)
            {
                string roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";

                if (roomid != "-1")
                {
                    Room room = FunGameSystem.RoomList[roomid];
                    RoomEventArgs eventArgs = new(room);
                    FunGameSystem.ServerPluginLoader?.OnBeforeIntoRoomEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnBeforeIntoRoomEvent(this, eventArgs);

                    if (eventArgs.Cancel)
                    {
                        ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Main_IntoRoom, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                    }
                    else if (SQLHelper != null)
                    {
                        SQLHelper.ExecuteDataSet(RoomQuery.Select_RoomByRoomId(SQLHelper, roomid));
                        if (SQLHelper.Success)
                        {
                            FunGameSystem.RoomList.IntoRoom(roomid, Server.User);
                            Server.InRoom = room;
                            Server.User.OnlineState = OnlineState.InRoom;
                            await Server.SendClients(Server.Listener.ClientList.Where(c => c != null && roomid == c.InRoom.Roomid && c.User.Id != 0),
                                SocketMessageType.Chat, Server.User.Username, DateTimeUtility.GetNowShortTime() + " [ " + Server.User.Username + " ] 进入了房间。");
                            result = true;
                        }
                        else
                        {
                            FunGameSystem.RoomList.RemoveRoom(roomid);
                        }
                    }

                    eventArgs.Success = result;
                    FunGameSystem.ServerPluginLoader?.OnAfterIntoRoomEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnAfterIntoRoomEvent(this, eventArgs);
                }
            }
            resultData.Add("result", result);
        }

        /// <summary>
        /// 匹配房间
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MatchRoom(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = true;
            if (requestData.Count >= 1)
            {
                bool isCancel = DataRequest.GetDictionaryJsonObject<bool>(requestData, "isCancel");
                if (!isCancel)
                {
                    GeneralEventArgs eventArgs = new();
                    FunGameSystem.ServerPluginLoader?.OnBeforeStartMatchEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnBeforeStartMatchEvent(this, eventArgs);

                    if (eventArgs.Cancel)
                    {
                        ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Main_MatchRoom, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                    }
                    else
                    {
                        ServerHelper.WriteLine("[MatchRoom] Start", InvokeMessageType.DataRequest);
                        RoomType type = DataRequest.GetDictionaryJsonObject<RoomType>(requestData, "roomType");
                        User user = DataRequest.GetDictionaryJsonObject<User>(requestData, "matcher") ?? Factory.GetUser();
                        StartMatching(type, user);
                    }

                    eventArgs.Success = !eventArgs.Cancel;
                    FunGameSystem.ServerPluginLoader?.OnAfterStartMatchEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnAfterStartMatchEvent(this, eventArgs);
                }
                else
                {
                    // 取消匹配
                    ServerHelper.WriteLine("[MatchRoom] Cancel", InvokeMessageType.DataRequest);
                    StopMatching();
                }
            }
            resultData.Add("result", result);
        }

        /// <summary>
        /// 设置已准备状态
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void SetReady(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            string roomid = "-1";
            if (requestData.Count >= 1)
            {
                roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
                User user = Server.User;

                if (roomid != "-1" && user.Id != 0 && user.Id != FunGameSystem.RoomList.GetRoomMaster(roomid).Id && !FunGameSystem.RoomList.GetReadyUserList(roomid).Contains(user))
                {
                    FunGameSystem.RoomList.SetReady(roomid, user);
                    result = true;
                }
            }
            resultData.Add("result", result);
            resultData.Add("ready", FunGameSystem.RoomList.GetReadyUserList(roomid));
            resultData.Add("notready", FunGameSystem.RoomList.GetNotReadyUserList(roomid));
        }

        /// <summary>
        /// 取消已准备状态
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void CancelReady(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            string roomid = "-1";
            if (requestData.Count >= 1)
            {
                roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
                User user = Server.User;

                if (roomid != "-1" && user.Id != 0 && user.Id != FunGameSystem.RoomList.GetRoomMaster(roomid).Id && FunGameSystem.RoomList.GetReadyUserList(roomid).Contains(user))
                {
                    FunGameSystem.RoomList.CancelReady(roomid, user);
                    result = true;
                }
            }
            resultData.Add("result", result);
            resultData.Add("ready", FunGameSystem.RoomList.GetReadyUserList(roomid));
            resultData.Add("notready", FunGameSystem.RoomList.GetNotReadyUserList(roomid));
        }

        /// <summary>
        /// 发送聊天消息
        /// </summary>
        /// <param name="requestData"></param>
        private async Task Chat(Dictionary<string, object> requestData)
        {
            if (requestData.Count >= 1)
            {
                string msg = DataRequest.GetDictionaryJsonObject<string>(requestData, "msg") ?? "";

                SendTalkEventArgs eventArgs = new(msg);
                FunGameSystem.ServerPluginLoader?.OnBeforeSendTalkEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeSendTalkEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Main_Chat, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else if (msg.Trim() != "")
                {
                    await Server.SendClients(Server.Listener.ClientList.Where(c => c != null && Server.InRoom.Roomid == c.InRoom.Roomid && c.User.Id != 0),
                        SocketMessageType.Chat, Server.User.Username, msg);
                }

                eventArgs.Success = !eventArgs.Cancel;
                FunGameSystem.ServerPluginLoader?.OnAfterSendTalkEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterSendTalkEvent(this, eventArgs);
            }
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void StartGame(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            if (requestData.Count >= 2)
            {
                string roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
                bool isMaster = DataRequest.GetDictionaryJsonObject<bool>(requestData, "isMaster");

                if (roomid != "-1")
                {
                    GeneralEventArgs eventArgs = new(FunGameSystem.RoomList[roomid], isMaster);
                    FunGameSystem.ServerPluginLoader?.OnBeforeStartGameEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnBeforeStartGameEvent(this, eventArgs);

                    if (eventArgs.Cancel)
                    {
                        ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Main_StartGame, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                    }
                    else
                    {
                        if (isMaster)
                        {
                            string[] usernames = [.. FunGameSystem.RoomList.GetNotReadyUserList(roomid).Select(user => user.Username)];
                            if (usernames.Length > 0)
                            {
                                if (_isReadyCheckCD[0] == false)
                                {
                                    // 提醒玩家准备
                                    Server.SendSystemMessage(ShowMessageType.None, "还有玩家尚未准备，无法开始游戏。", "", 0, Server.User.Username);
                                    Server.SendSystemMessage(ShowMessageType.Tip, "房主即将开始游戏，请准备！", "请准备就绪", 10, usernames);
                                    _isReadyCheckCD[0] = true;
                                    TaskUtility.RunTimer(() =>
                                    {
                                        _isReadyCheckCD[0] = false;
                                    }, 15000);
                                }
                                else
                                {
                                    Server.SendSystemMessage(ShowMessageType.None, "还有玩家尚未准备，无法开始游戏。15秒内只能发送一次准备提醒。", "", 0, Server.User.Username);
                                }
                            }
                            else
                            {
                                List<User> users = FunGameSystem.RoomList.GetUsers(roomid);
                                if (users.Count < 2)
                                {
                                    Server.SendSystemMessage(ShowMessageType.None, "玩家数量不足，无法开始游戏。", "", 0, Server.User.Username);
                                }
                                else
                                {
                                    usernames = [.. users.Select(user => user.Username)];
                                    Server.SendSystemMessage(ShowMessageType.None, "所有玩家均已准备，游戏将在10秒后开始。", "", 0, usernames);
                                    StartGame(roomid, users, usernames);
                                    result = true;
                                }
                            }
                        }
                        else if (_isReadyCheckCD[1] == false)
                        {
                            // 提醒房主开始游戏
                            Server.SendSystemMessage(ShowMessageType.None, "已提醒房主立即开始游戏。", "", 0, Server.User.Username);
                            Server.SendSystemMessage(ShowMessageType.Tip, "房间中的玩家已请求你立即开始游戏。", "请求开始", 10, FunGameSystem.RoomList[roomid].RoomMaster.Username);
                            _isReadyCheckCD[1] = true;
                            TaskUtility.RunTimer(() =>
                            {
                                _isReadyCheckCD[1] = false;
                            }, 15000);
                        }
                        else
                        {
                            Server.SendSystemMessage(ShowMessageType.None, "15秒内只能发送一次提醒，请稍后再试。", "", 0, Server.User.Username);
                        }
                    }

                    eventArgs.Success = result;
                    FunGameSystem.ServerPluginLoader?.OnAfterStartGameEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnAfterStartGameEvent(this, eventArgs);
                }
            }
            resultData.Add("result", result);
        }

        private void StartGame(string roomid, List<User> users, params string[] usernames)
        {
            Room room = General.HallInstance;
            if (roomid != "-1")
            {
                room = FunGameSystem.RoomList[roomid];
            }
            if (room.Roomid == "-1") return;
            // 启动服务器
            TaskUtility.NewTask(() =>
            {
                if (FunGameSystem.GameModuleLoader != null && FunGameSystem.GameModuleLoader.ModuleServers.ContainsKey(room.GameModule))
                {
                    room.RoomState = RoomState.Gaming;
                    Server.NowGamingServer = FunGameSystem.GameModuleLoader.GetServerMode(room.GameModule);
                    Server.User.OnlineState = OnlineState.Gaming;
                    Dictionary<string, IServerModel> all = Server.Listener.UserList.Cast<IServerModel>().ToDictionary(k => k.User.Username, v => v);
                    // 给其他玩家赋值模组服务器
                    foreach (IServerModel model in all.Values.Where(s => s.User.Username != Server.User.Username))
                    {
                        model.NowGamingServer = Server.NowGamingServer;
                        model.User.OnlineState = OnlineState.Gaming;
                    }
                    GamingObject obj = new(room, users, Server, all);
                    if (Server.NowGamingServer.StartServer(obj))
                    {
                        Server.NowGamingServer.GamingObjects.TryAdd(room.Roomid, obj);
                        foreach (IServerModel serverTask in Server.Listener.UserList.Where(model => usernames.Contains(model.User.Username)))
                        {
                            if (serverTask != null && serverTask.Socket != null)
                            {
                                FunGameSystem.RoomList.CancelReady(roomid, serverTask.User);
                                serverTask.Send(SocketMessageType.StartGame, room, users);
                            }
                        }
                    }
                }
            });
        }

        #endregion

        #region Reg

        /// <summary>
        /// 接收并验证注册验证码
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void Reg(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "";
            RegInvokeType returnType = RegInvokeType.None;
            bool success = false;
            if (requestData.Count >= 4)
            {
                string username = DataRequest.GetDictionaryJsonObject<string>(requestData, "username") ?? "";
                string password = DataRequest.GetDictionaryJsonObject<string>(requestData, "password") ?? "";
                string email = DataRequest.GetDictionaryJsonObject<string>(requestData, "email") ?? "";
                string verifycode = DataRequest.GetDictionaryJsonObject<string>(requestData, "verifycode") ?? "";
                (msg, returnType, success) = DataRequestService.Reg(Server, username, password, email, verifycode, Server.Socket?.ClientIP ?? "");
            }
            else
            {
                ServerHelper.WriteLine("客户端提供的参数不足。", InvokeMessageType.DataRequest, LogLevel.Warning);
            }
            resultData.Add("msg", msg);
            resultData.Add("type", returnType);
            resultData.Add("success", success);
        }

        #endregion

        #region Login

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private async Task Login(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "";
            User user = Factory.GetUser();

            string username = "";
            string password = "";
            string autokey = "";
            Guid key = Guid.Empty;
            if (requestData.Count >= 4)
            {
                username = DataRequest.GetDictionaryJsonObject<string>(requestData, "username") ?? "";
                password = DataRequest.GetDictionaryJsonObject<string>(requestData, "password") ?? "";
                autokey = DataRequest.GetDictionaryJsonObject<string>(requestData, "autokey") ?? "";
                key = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "key");
            }
            else
            {
                ServerHelper.WriteLine("客户端提供的参数不足。", InvokeMessageType.DataRequest, LogLevel.Warning);
            }

            // CheckLogin的情况
            if (key != Guid.Empty)
            {
                if (Server.IsLoginKey(key))
                {
                    await Server.CheckLogin();
                    user = Server.User;
                }
                else
                {
                    msg = "客户端发送了错误的秘钥，不允许本次登录。";
                    ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                }
            }
            else
            {
                // 进行预登录
                (bool success, DataSet dsUser, msg, key) = DataRequestService.PreLogin(this, username, password, autokey);
                if (success)
                {
                    Server.PreLogin(dsUser, key);
                    resultData.Add("key", key);
                }
            }

            resultData.Add("msg", msg);
            resultData.Add("user", user);
        }

        /// <summary>
        /// 接收并验证找回密码时的验证码
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void ForgetPassword(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法找回您的密码，请稍后再试。"; // 返回的验证信息
            if (requestData.Count >= 3)
            {
                string username = DataRequest.GetDictionaryJsonObject<string>(requestData, "username") ?? "";
                string email = DataRequest.GetDictionaryJsonObject<string>(requestData, "email") ?? "";
                string verifycode = DataRequest.GetDictionaryJsonObject<string>(requestData, "forgetVerifyCode") ?? "";

                // 客户端发来了验证码就进行验证，没有发就生成
                if (verifycode.Trim() != "")
                {
                    // 先检查验证码
                    if (SQLHelper != null)
                    {
                        DataRow? dr = SQLHelper.ExecuteDataRow(ForgetVerifyCodes.Select_ForgetVerifyCode(SQLHelper, username, email, verifycode));
                        if (dr != null)
                        {
                            // 检查验证码是否过期
                            if (!DateTime.TryParse(dr[ForgetVerifyCodes.Column_SendTime].ToString(), out DateTime SendTime))
                            {
                                SendTime = General.DefaultTime;
                            }
                            if ((DateTime.Now - SendTime).TotalMinutes >= 10)
                            {
                                ServerHelper.WriteLine(Server.GetClientName() + " 验证码已过期");
                                msg = "此验证码已过期，请重新找回密码。";
                                SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(SQLHelper, username, email));
                            }
                            else
                            {
                                // 检查验证码是否正确
                                if (verifycode.Equals(dr[ForgetVerifyCodes.Column_ForgetVerifyCode]))
                                {
                                    ServerHelper.WriteLine("[ForgerPassword] Username: " + username + " Email: " + email);
                                    SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(SQLHelper, username, email));
                                    msg = "";
                                }
                                else msg = "验证码不正确，请重新输入！";
                            }
                        }
                        else msg = "验证码不正确，请重新输入！";
                    }
                }
                else
                {
                    // 检查账号和邮箱是否匹配
                    if (SQLHelper != null)
                    {
                        SQLHelper.ExecuteDataSet(UserQuery.Select_CheckEmailWithUsername(SQLHelper, username, email));
                        if (SQLHelper.Result != SQLResult.Success)
                        {
                            msg = "此邮箱未绑定此账号，请重试！";
                        }
                        else
                        {
                            // 检查验证码是否发送过和是否过期
                            DataRow? dr = SQLHelper.ExecuteDataRow(ForgetVerifyCodes.Select_HasSentForgetVerifyCode(SQLHelper, username, email));
                            if (dr is null || (DateTime.TryParse(dr[ForgetVerifyCodes.Column_SendTime].ToString(), out DateTime SendTime) && (DateTime.Now - SendTime).TotalMinutes >= 10))
                            {
                                // 发送验证码，需要先删除之前过期的验证码
                                SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(SQLHelper, username, email));
                                string forgetVerify = Verification.CreateVerifyCode(VerifyCodeType.NumberVerifyCode, 6);
                                SQLHelper.Execute(ForgetVerifyCodes.Insert_ForgetVerifyCode(SQLHelper, username, email, forgetVerify));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    if (MailSender != null)
                                    {
                                        // 发送验证码
                                        string ServerName = Config.ServerName;
                                        string Subject = $"[{ServerName}] 找回密码验证码";
                                        string Body = $"亲爱的 {username}， <br/>    您正在找回 [{ServerName}] 账号的密码，您的验证码是 {forgetVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.LongDateOnly)}";
                                        string[] To = [email];
                                        if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                                        {
                                            ServerHelper.WriteLine(Server.GetClientName() + $" 已向{email}发送验证码：{forgetVerify}");
                                            msg = "";
                                        }
                                        else
                                        {
                                            ServerHelper.WriteLine(Server.GetClientName() + " 无法发送验证码");
                                            ServerHelper.WriteLine(MailSender.ErrorMsg);
                                        }
                                    }
                                    else // 不使用MailSender的情况
                                    {
                                        ServerHelper.WriteLine(Server.GetClientName() + $" 验证码为：{forgetVerify}，但因 SMTP 服务未开启，请服务器管理员告知此用户");
                                        msg = "";
                                    }
                                }
                            }
                            else
                            {
                                // 发送过验证码且验证码没有过期
                                string ForgetVerifyCode = (string)dr[ForgetVerifyCodes.Column_ForgetVerifyCode];
                                ServerHelper.WriteLine(Server.GetClientName() + $" 十分钟内已向{email}发送过验证码：{ForgetVerifyCode}");
                                msg = "";
                            }
                        }
                    }
                }
            }
            resultData.Add("msg", msg);
        }

        #endregion

        #region Room

        /// <summary>
        /// 更新房间设置
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdateRoomSettings(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            string msg = "";
            string roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
            RoomType newType = DataRequest.GetDictionaryJsonObject<RoomType>(requestData, "type");
            string newPassword = DataRequest.GetDictionaryJsonObject<string>(requestData, "password") ?? "";
            int newMaxUsers = DataRequest.GetDictionaryJsonObject<int>(requestData, "maxUsers");
            string newModule = DataRequest.GetDictionaryJsonObject<string>(requestData, "module") ?? "";
            string newMap = DataRequest.GetDictionaryJsonObject<string>(requestData, "map") ?? "";
            User user = Server.User;
            if (roomid != "-1" && FunGameSystem.RoomList.Exists(roomid))
            {
                Room room = FunGameSystem.RoomList[roomid];
                RoomEventArgs eventArgs = new(room);
                FunGameSystem.ServerPluginLoader?.OnBeforeChangeRoomSettingEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeChangeRoomSettingEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    msg = DataRequestService.GetPluginCancelString(DataRequestType.Room_UpdateRoomSettings, eventArgs);
                    ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else
                {
                    if (user.Id != room.RoomMaster.Id)
                    {
                        msg = "更新失败，只有房主才可以更新房间设置。";
                    }
                    else
                    {
                        result = true;
                        ServerHelper.WriteLine("[UpdateRoomSettings] User: " + user.Username + " RoomID: " + room.Roomid);
                        if (room.RoomState == RoomState.Created)
                        {
                            room.RoomType = newType;
                            room.Password = newPassword;
                            room.MaxUsers = newMaxUsers;
                            room.GameModule = newModule;
                            room.GameMap = newMap;
                        }
                        else
                        {
                            msg = "更新失败，只能在房间状态稳定时更新其设置。";
                        }
                    }
                }

                eventArgs.Success = result;
                FunGameSystem.ServerPluginLoader?.OnAfterChangeRoomSettingEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterChangeRoomSettingEvent(this, eventArgs);
            }
            resultData.Add("result", result);
            resultData.Add("msg", msg);
            resultData.Add("room", roomid);
        }

        /// <summary>
        /// 获取房间内玩家数量
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private static void GetRoomPlayerCount(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string roomid = "-1";
            if (requestData.Count >= 1)
            {
                roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
            }
            resultData.Add("count", FunGameSystem.RoomList.GetUserCount(roomid));
        }

        /// <summary>
        /// 更新房主
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        /// <returns></returns>
        private async Task UpdateRoomMaster(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;

            if (requestData.Count >= 2)
            {
                string roomid = DataRequest.GetDictionaryJsonObject<string>(requestData, "roomid") ?? "-1";
                User newMaster = DataRequest.GetDictionaryJsonObject<User>(requestData, "newMaster") ?? Factory.GetUser();

                if (roomid != "-1" && FunGameSystem.RoomList.Exists(roomid) && newMaster.Id != 0)
                {
                    Room room = FunGameSystem.RoomList[roomid];
                    RoomEventArgs eventArgs = new(room);
                    FunGameSystem.ServerPluginLoader?.OnBeforeChangeRoomSettingEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnBeforeChangeRoomSettingEvent(this, eventArgs);

                    if (eventArgs.Cancel)
                    {
                        ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Room_UpdateRoomSettings, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                    }
                    else
                    {
                        User oldMaster = room.RoomMaster;
                        room.RoomMaster = newMaster;
                        result = true;

                        if (SQLHelper != null)
                        {
                            SQLHelper.UpdateRoomMaster(roomid, newMaster.Id);
                            if (SQLHelper.Result == SQLResult.Success)
                            {
                                await Server.SendClients(Server.Listener.ClientList.Where(c => c != null && c.InRoom.Roomid == roomid), SocketMessageType.UpdateRoomMaster, room);
                                ServerHelper.WriteLine($"[UpdateRoomMaster] RoomID: {roomid} 房主变更: {oldMaster.Username} -> {newMaster.Username}");
                            }
                        }
                    }

                    eventArgs.Success = result;
                    FunGameSystem.ServerPluginLoader?.OnAfterChangeRoomSettingEvent(this, eventArgs);
                    FunGameSystem.WebAPIPluginLoader?.OnAfterChangeRoomSettingEvent(this, eventArgs);
                }
            }
            else
            {
                ServerHelper.WriteLine("客户端提供的参数不足。", InvokeMessageType.DataRequest, LogLevel.Warning);
            }

            // 返回结果
            resultData.Add("result", result);
        }

        /// <summary>
        /// 开始匹配
        /// </summary>
        /// <param name="type"></param>
        /// <param name="user"></param>
        private void StartMatching(RoomType type, User user)
        {
            _isMatching = true;
            if (user.OnlineState == OnlineState.Online) user.OnlineState = OnlineState.Matching;
            ServerHelper.WriteLine(Server.GetClientName() + " 开始匹配。类型：" + RoomSet.GetTypeString(type));
            TaskUtility.NewTask(async () =>
            {
                if (_isMatching)
                {
                    Room room = await MatchingRoom(type, user);
                    if (_isMatching && Server.Socket != null)
                    {
                        await Server.Send(SocketMessageType.MatchRoom, room);
                    }
                    _isMatching = false;
                }
            }).OnError(e =>
            {
                ServerHelper.Error(e);
                _isMatching = false;
            });
        }

        /// <summary>
        /// 终止匹配
        /// </summary>
        private void StopMatching()
        {
            if (_isMatching)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " 取消了匹配。");
                if (Server.User.OnlineState == OnlineState.Matching) Server.User.OnlineState = OnlineState.Online;
                _isMatching = false;
            }
        }

        /// <summary>
        /// 匹配线程
        /// </summary>
        /// <param name="roomtype"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<Room> MatchingRoom(RoomType roomtype, User user)
        {
            int i = 1; // Elo扩大系数
            double time = 0; // 已经匹配的时间
            double expandInterval = 10; // 扩大匹配范围的间隔时间
            double maxTime = 50; // 最大匹配时间
            bool isRefreshRoom = false; // 是否刷新房间列表

            // 匹配房间类型（如果是All，则匹配所有房间）
            List<Room> targets;
            if (roomtype == RoomType.All)
            {
                targets = [.. FunGameSystem.RoomList.ListRoom.Where(r => r.RoomState == RoomState.Created || r.RoomState == RoomState.Matching)];
            }
            else
            {
                targets = [.. FunGameSystem.RoomList.ListRoom.Where(r => (r.RoomState == RoomState.Created || r.RoomState == RoomState.Matching) && r.RoomType == roomtype)];
            }

            while (_isMatching)
            {
                if (isRefreshRoom)
                {
                    isRefreshRoom = false;
                    if (roomtype == RoomType.All)
                    {
                        targets = [.. FunGameSystem.RoomList.ListRoom.Where(r => r.RoomState == RoomState.Created || r.RoomState == RoomState.Matching)];
                    }
                    else
                    {
                        targets = [.. FunGameSystem.RoomList.ListRoom.Where(r => (r.RoomState == RoomState.Created || r.RoomState == RoomState.Matching) && r.RoomType == roomtype)];
                    }
                }

                // 如果匹配停止，则退出
                if (!_isMatching) break;

                foreach (Room room in targets)
                {
                    // 获取当前房间的玩家列表
                    List<User> players = FunGameSystem.RoomList.GetUsers(room.Roomid);
                    if (players.Count > 0)
                    {
                        // 计算房间平均Elo
                        double avgElo = players.Sum(u => u.Statistics.EloStats.TryGetValue(0, out double value) ? value : 0) / players.Count;
                        double userElo = user.Statistics.EloStats.TryGetValue(0, out double userValue) ? userValue : 0;

                        // 匹配Elo范围，随着时间增加，范围逐渐扩大
                        if (userElo >= avgElo - (300 * i) && userElo <= avgElo + (300 * i))
                        {
                            // 找到匹配的房间，立即返回
                            return room;
                        }
                    }
                }

                // 如果匹配停止，则退出
                if (!_isMatching) break;

                // 检查是否已经过了10秒，扩大匹配范围
                if (time >= expandInterval * i)
                {
                    i++;
                    // 刷新房间列表
                    isRefreshRoom = true;
                }
                // 达到最大匹配时间后不再匹配Elo，直接返回第一个房间
                if (time >= maxTime)
                {
                    return targets.FirstOrDefault() ?? General.HallInstance;
                }

                await Task.Delay(100);
                time += 0.1;
            }

            return General.HallInstance;
        }

        #endregion

        #region UserCenter

        /// <summary>
        /// 更新用户（全部数据）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdateUser(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法更新用户数据，请稍后再试。";
            if (SQLHelper != null && requestData.Count > 0)
            {
                User user = DataRequest.GetDictionaryJsonObject<User>(requestData, "user") ?? Factory.GetUser();

                GeneralEventArgs eventArgs = new(user);
                FunGameSystem.ServerPluginLoader?.OnBeforeChangeProfileEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeChangeProfileEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    msg = DataRequestService.GetPluginCancelString(DataRequestType.UserCenter_UpdateUser, eventArgs);
                    ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else
                {
                    SQLHelper.UpdateUser(user);
                    if (SQLHelper.Success)
                    {
                        msg = "";
                    }
                }

                eventArgs.Success = msg == "";
                FunGameSystem.ServerPluginLoader?.OnAfterChangeProfileEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterChangeProfileEvent(this, eventArgs);
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 更新用户的密码
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdatePassword(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法更新您的密码，请稍后再试。";
            if (requestData.Count >= 2)
            {
                string username = DataRequest.GetDictionaryJsonObject<string>(requestData, "username") ?? "";
                string password = DataRequest.GetDictionaryJsonObject<string>(requestData, "password") ?? "";

                GeneralEventArgs eventArgs = new(username, password);
                FunGameSystem.ServerPluginLoader?.OnBeforeChangeAccountSettingEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeChangeAccountSettingEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    msg = DataRequestService.GetPluginCancelString(_lastRequest, eventArgs);
                    ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else if (username.Trim() != "" && password.Trim() != "")
                {
                    FunGameSystem.UpdateUserKey(username);
                    password = password.Encrypt(FunGameSystem.GetUserKey(username));
                    SQLHelper?.UpdatePassword(username, password);
                    if (SQLHelper?.Success ?? false)
                    {
                        // 更新成功返回空值
                        msg = "";
                    }
                }

                eventArgs.Success = msg == "";
                FunGameSystem.ServerPluginLoader?.OnAfterChangeAccountSettingEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterChangeAccountSettingEvent(this, eventArgs);
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 每日签到
        /// </summary>
        /// <param name="resultData"></param>
        private void DailySignIn(Dictionary<string, object> resultData)
        {
            GeneralEventArgs eventArgs = new();
            FunGameSystem.ServerPluginLoader?.OnBeforeSignInEvent(this, eventArgs);
            FunGameSystem.WebAPIPluginLoader?.OnBeforeSignInEvent(this, eventArgs);

            if (eventArgs.Cancel)
            {
                ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.UserCenter_DailySignIn, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
            }
            else if (SQLHelper != null)
            {
                long userId = Server.User.Id;
                if (userId != 0)
                {
                    DataRow? dr = SQLHelper.ExecuteDataRow(UserSignIns.Select_GetUserSignIn(SQLHelper, userId));
                    if (dr != null)
                    {
                        int days = Convert.ToInt32(dr[UserSignIns.Column_Days]) + 1;
                        bool isSigned = Convert.ToInt32(dr[UserSignIns.Column_IsSigned]) != 0;
                        if (dr[UserSignIns.Column_LastTime] != DBNull.Value && DateTime.TryParseExact(dr[UserSignIns.Column_LastTime].ToString(), General.GeneralDateTimeFormat, null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                        {
                            if (isSigned)
                            {
                                resultData.Add("msg", "今天已经签到过了，请明天再来。");
                                return;
                            }
                            if ((DateTime.Now - dt).TotalDays > 1)
                            {
                                days = 1;
                            }
                        }
                        SQLHelper.Execute(UserSignIns.Update_UserSignIn(SQLHelper, userId, days));
                        if (SQLHelper.Success)
                        {
                            eventArgs.Success = true;
                            FunGameSystem.ServerPluginLoader?.OnAfterSignInEvent(this, eventArgs);
                            FunGameSystem.WebAPIPluginLoader?.OnAfterSignInEvent(this, eventArgs);

                            resultData.Add("msg", $"签到成功！你已经连续签到 {days} 天！");
                            return;
                        }
                    }
                }
            }

            eventArgs.Success = false;
            FunGameSystem.ServerPluginLoader?.OnAfterSignInEvent(this, eventArgs);
            FunGameSystem.WebAPIPluginLoader?.OnAfterSignInEvent(this, eventArgs);

            resultData.Add("msg", "签到失败！");
        }

        #endregion

        #region Inventory

        /// <summary>
        /// 获取商店信息
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetStore(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            List<Store> stores = [];

            if (requestData.Count > 0)
            {
                long[] ids = DataRequest.GetDictionaryJsonObject<long[]>(requestData, "ids") ?? [];

                GeneralEventArgs eventArgs = new(ids);
                FunGameSystem.ServerPluginLoader?.OnBeforeOpenStoreEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeOpenStoreEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Inventory_GetStore, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else stores = SQLHelper?.GetStoresWithGoods(ids) ?? [];

                eventArgs.Success = stores.Count > 0;
                FunGameSystem.ServerPluginLoader?.OnAfterOpenStoreEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterOpenStoreEvent(this, eventArgs);
            }

            resultData.Add("result", stores.Count > 0);
            resultData.Add("stores", stores);
        }

        /// <summary>
        /// 获取市场信息
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetMarket(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            List<MarketItem> markets = [];

            if (requestData.Count > 0)
            {
                long[] users = DataRequest.GetDictionaryJsonObject<long[]>(requestData, "users") ?? [];
                MarketItemState state = DataRequest.GetDictionaryJsonObject<MarketItemState>(requestData, "state");

                if (users.Length > 0)
                {
                    foreach (long userid in users)
                    {
                        markets.AddRange(SQLHelper?.GetAllMarketsItem(userid, state) ?? []);
                    }
                }
                else
                {
                    markets = SQLHelper?.GetAllMarketsItem(0, state) ?? [];
                }

                long[] items = DataRequest.GetDictionaryJsonObject<long[]>(requestData, "items") ?? [];
                if (items.Length > 0)
                {
                    markets = [.. markets.Where(i => items.Contains(i.Id))];
                }
            }

            resultData.Add("result", markets.Count > 0);
            resultData.Add("markets", markets);
        }

        /// <summary>
        /// 购买物品（商店）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void StoreBuy(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = true;
            double totalCost = 0;
            List<string> buyResult = [];
            if (SQLHelper != null && requestData.Count > 0)
            {
                long storeid = DataRequest.GetDictionaryJsonObject<long>(requestData, "storeid");
                long userid = DataRequest.GetDictionaryJsonObject<long>(requestData, "userid");
                string currency = DataRequest.GetDictionaryJsonObject<string>(requestData, "currency") ?? "";
                Dictionary<long, int> counts = DataRequest.GetDictionaryJsonObject<Dictionary<long, int>>(requestData, "counts") ?? [];
                bool ignore = DataRequest.GetDictionaryJsonObject<bool>(requestData, "ignore");

                GeneralEventArgs eventArgs = new(DataRequestType.Inventory_StoreBuy, storeid, userid, currency, counts, ignore);
                FunGameSystem.ServerPluginLoader?.OnBeforeBuyItemEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeBuyItemEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Inventory_StoreBuy, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else
                {
                    Store? store = SQLHelper.GetStore(storeid);
                    User? user = SQLHelper.GetUserById(userid, true);
                    if (store != null && user != null)
                    {
                        try
                        {
                            SQLHelper.NewTransaction();

                            foreach (long goodsId in counts.Keys)
                            {
                                Goods goods = store.Goods[goodsId];
                                int count = counts[goodsId];
                                if (goods.Stock != -1 && count > goods.Stock)
                                {
                                    result = false;
                                    buyResult.Add($"购买失败，原因：库存不足，当前库存为：{goods.Stock}，购买数量：{count}。");
                                    continue;
                                }
                                if (goods.GetPrice(currency, out double price))
                                {
                                    bool subResult = true;
                                    bool useCredits = true;
                                    double totalPrice = price * count;
                                    if (currency == General.GameplayEquilibriumConstant.InGameCurrency && user.Inventory.Credits >= totalPrice)
                                    {
                                        user.Inventory.Credits -= totalPrice;
                                    }
                                    else
                                    {
                                        subResult = false;
                                        buyResult.Add($"购买失败，原因：需要花费 {totalPrice} {General.GameplayEquilibriumConstant.InGameCurrency}，但是您只有 {user.Inventory.Credits} {General.GameplayEquilibriumConstant.InGameCurrency}。");
                                    }
                                    if (currency == General.GameplayEquilibriumConstant.InGameMaterial && user.Inventory.Materials >= totalPrice)
                                    {
                                        user.Inventory.Materials -= totalPrice;
                                        useCredits = false;
                                    }
                                    else
                                    {
                                        subResult = false;
                                        buyResult.Add($"购买失败，原因：需要花费 {totalPrice} {General.GameplayEquilibriumConstant.InGameMaterial}，但是您只有 {user.Inventory.Materials} {General.GameplayEquilibriumConstant.InGameMaterial}。");
                                    }
                                    if (subResult)
                                    {
                                        if (goods.Stock != -1) goods.Stock -= count;
                                        totalCost += totalPrice;
                                        ProcessStoreBuy(goods, useCredits, price, count, user);
                                        buyResult.Add($"成功消费：{totalPrice} {currency}，购买了 {count} 个 {goods.Name}。");
                                    }
                                    else
                                    {
                                        result = false;
                                    }
                                }
                            }

                            if (result || (!result && ignore))
                            {
                                SQLHelper.UpdateInventory(user.Inventory);
                            }

                            if (SQLHelper.Success)
                            {
                                SQLHelper.Commit();
                            }
                            else
                            {
                                SQLHelper.Rollback();
                            }
                        }
                        catch (Exception e)
                        {
                            SQLHelper.Rollback();
                            ServerHelper.Error(e);
                            buyResult.Add("暂时无法处理此购买，请稍后再试。");
                        }
                    }
                }

                eventArgs.Success = result;
                FunGameSystem.ServerPluginLoader?.OnAfterBuyItemEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterBuyItemEvent(this, eventArgs);
            }
            resultData.Add("result", result);
            resultData.Add("msg", string.Join("\r\n", buyResult));
        }

        /// <summary>
        /// 处理商店购买
        /// </summary>
        /// <param name="goods"></param>
        /// <param name="useCredits"></param>
        /// <param name="price"></param>
        /// <param name="count"></param>
        /// <param name="user"></param>
        private static void ProcessStoreBuy(Goods goods, bool useCredits, double price, int count, User user)
        {
            foreach (Item item in goods.Items)
            {
                for (int i = 0; i < count; i++)
                {
                    Item newItem = item.Copy();
                    newItem.IsTradable = false;
                    newItem.NextTradableTime = DateTimeUtility.GetTradableTime();
                    newItem.Price = useCredits ? Calculation.Round2Digits(price * 0.35) : Calculation.Round2Digits(price * 7);
                    newItem.User = user;
                    newItem.EntityState = EntityState.Added;
                    user.Inventory.Items.Add(newItem);
                }
            }
        }

        /// <summary>
        /// 购买物品（市场）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MarketBuy(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            string msg = "无法购买此物品，请稍后再试。";
            if (SQLHelper != null && requestData.Count > 0)
            {
                Guid itemGuid = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "itemGuid");
                long userid = DataRequest.GetDictionaryJsonObject<long>(requestData, "userid");
                double price = DataRequest.GetDictionaryJsonObject<double>(requestData, "price");

                GeneralEventArgs eventArgs = new(DataRequestType.Inventory_MarketBuy, itemGuid, userid, price);
                FunGameSystem.ServerPluginLoader?.OnBeforeBuyItemEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeBuyItemEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Inventory_MarketBuy, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else
                {
                    MarketItem? marketItem = SQLHelper.GetMarketItem(itemGuid);
                    if (marketItem != null)
                    {
                        try
                        {
                            User? buyer = SQLHelper.GetUserById(userid, true);
                            User? itemUser = SQLHelper.GetUserById(marketItem.User, true);
                            if (itemUser != null && buyer != null && itemUser.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                            {
                                if (buyer.Inventory.Credits >= price)
                                {
                                    buyer.Inventory.Credits -= price;
                                    double fee = Calculation.Round2Digits(price * 0.15);
                                    itemUser.Inventory.Credits += price - fee;
                                    result = true;
                                }
                                else
                                {
                                    msg = $"购买失败，原因：需要花费 {price} {General.GameplayEquilibriumConstant.InGameCurrency}，但是您只有 {buyer.Inventory.Credits} {General.GameplayEquilibriumConstant.InGameCurrency}。";
                                }

                                if (result)
                                {
                                    SQLHelper.NewTransaction();

                                    try
                                    {
                                        item.EntityState = EntityState.Deleted;
                                        SQLHelper.DeleteMarketItem(itemGuid);

                                        Item newItem = item.Copy();
                                        newItem.IsTradable = false;
                                        newItem.NextTradableTime = DateTimeUtility.GetTradableTime();
                                        newItem.User = buyer;
                                        newItem.EntityState = EntityState.Added;
                                        buyer.Inventory.Items.Add(newItem);

                                        SQLHelper.UpdateInventory(itemUser.Inventory);
                                        SQLHelper.UpdateInventory(buyer.Inventory);
                                    }
                                    catch
                                    {
                                        result = false;
                                    }

                                    if (result)
                                    {
                                        msg = $"成功消费：{price} {General.GameplayEquilibriumConstant.InGameCurrency}，购买了 {itemUser.Username} 出售的 {item.Name}。";
                                        SQLHelper.Commit();
                                    }
                                    else
                                    {
                                        SQLHelper.Rollback();
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            SQLHelper.Rollback();
                            ServerHelper.Error(e);
                            msg = "暂时无法处理此购买，请稍后再试。";
                        }
                    }
                    else
                    {
                        msg = "目标物品不存在。";
                    }
                }

                eventArgs.Success = result;
                FunGameSystem.ServerPluginLoader?.OnAfterBuyItemEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterBuyItemEvent(this, eventArgs);
            }
            resultData.Add("result", result);
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 更新库存
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdateInventory(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法更新库存数据，请稍后再试。";
            if (SQLHelper != null && requestData.Count > 0)
            {
                Inventory inventory = DataRequest.GetDictionaryJsonObject<Inventory>(requestData, "inventory") ?? Factory.GetInventory();
                bool fullUpdate = DataRequest.GetDictionaryJsonObject<bool>(requestData, "fullUpdate");
                SQLHelper.UpdateInventory(inventory, fullUpdate);
                if (SQLHelper.Success)
                {
                    msg = "";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 使用物品
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void Use(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            string msg = "无法使用此物品，请稍后再试。";
            if (SQLHelper != null && requestData.Count > 0)
            {
                Guid itemGuid = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "itemGuid");
                long userid = DataRequest.GetDictionaryJsonObject<long>(requestData, "userid");
                Character[] targets = DataRequest.GetDictionaryJsonObject<Character[]>(requestData, "targets") ?? [];
                int useCount = DataRequest.GetDictionaryJsonObject<int>(requestData, "useCount");

                GeneralEventArgs eventArgs = new(itemGuid, userid, targets, useCount);
                FunGameSystem.ServerPluginLoader?.OnBeforeUseItemEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnBeforeUseItemEvent(this, eventArgs);

                if (eventArgs.Cancel)
                {
                    ServerHelper.WriteLine(DataRequestService.GetPluginCancelString(DataRequestType.Inventory_Use, eventArgs), InvokeMessageType.DataRequest, LogLevel.Warning);
                }
                else
                {
                    User? user = SQLHelper.GetUserById(userid, true);
                    if (user != null && user.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                    {
                        // 暂定标准实现是传这个参数，作用目标
                        Dictionary<string, object> args = new()
                        {
                            { "targets", targets }
                        };
                        bool used = item.UseItem(user, useCount, args);
                        if (used)
                        {
                            SQLHelper.UpdateInventory(user.Inventory);
                            result = true;
                        }
                    }
                    if (result)
                    {
                        msg = "";
                    }
                }

                eventArgs.Success = result;
                FunGameSystem.ServerPluginLoader?.OnAfterUseItemEvent(this, eventArgs);
                FunGameSystem.WebAPIPluginLoader?.OnAfterUseItemEvent(this, eventArgs);
            }
            resultData.Add("result", result);
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 出售物品（商店）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void StoreSell(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            bool result = false;
            string msg = "无法出售此物品，请稍后再试。";
            if (SQLHelper != null && requestData.Count > 0)
            {
                Guid itemGuid = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "itemGuid");
                long userid = DataRequest.GetDictionaryJsonObject<long>(requestData, "userid");
                User? user = SQLHelper.GetUserById(userid, true);
                if (user != null && user.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                {
                    if (!item.IsSellable)
                    {
                        msg = $"此物品无法出售{(item.NextSellableTime != DateTime.MinValue ? $"，此物品将在 {item.NextSellableTime.ToString(General.GeneralDateTimeFormatChinese)} 后可出售" : "")}。";
                    }
                    else
                    {
                        double reward = item.Price;
                        user.Inventory.Credits += reward;
                        item.EntityState = EntityState.Deleted;
                        SQLHelper.UpdateInventory(user.Inventory);
                        result = true;
                    }
                }
                if (result)
                {
                    msg = "";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 出售物品（市场）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MarketSell(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法上架此物品，请稍后再试。";
            if (requestData.Count > 0)
            {
                Guid itemGuid = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "itemGuid");
                long userid = DataRequest.GetDictionaryJsonObject<long>(requestData, "userid");
                double price = DataRequest.GetDictionaryJsonObject<double>(requestData, "price");
                int stock = DataRequest.GetDictionaryJsonObject<int>(requestData, "stock");
                User? user = SQLHelper?.GetUserById(userid, true);
                if (user != null && user.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                {
                    if (!item.IsSellable)
                    {
                        msg = $"此物品无法出售{(item.NextSellableTime != DateTime.MinValue ? $"，此物品将在 {item.NextSellableTime.ToString(General.GeneralDateTimeFormatChinese)} 后可出售" : "")}。";
                    }
                    else
                    {
                        SQLHelper?.AddMarketItem(itemGuid, userid, price, stock);
                        if (SQLHelper?.Success ?? false)
                        {
                            msg = "";
                        }
                    }
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 下架市场物品
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MarketDelist(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法下架市场物品，请稍后再试。";
            if (requestData.Count > 0)
            {
                long userid = DataRequest.GetDictionaryJsonObject<long>(requestData, "userid");
                if (userid != 0)
                {
                    SQLHelper?.DeleteMarketItemByUserId(userid);
                }
                else
                {
                    Guid itemGuid = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "itemGuid");
                    if (itemGuid != Guid.Empty) SQLHelper?.DeleteMarketItem(itemGuid);
                }
                if (SQLHelper?.Success ?? false)
                {
                    msg = "";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 更新市场价格
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdateMarketPrice(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法更新市场价格，请稍后再试。";
            if (requestData.Count > 0)
            {
                Guid itemGuid = DataRequest.GetDictionaryJsonObject<Guid>(requestData, "itemGuid");
                double price = DataRequest.GetDictionaryJsonObject<double>(requestData, "price");
                SQLHelper?.UpdateMarketItemPrice(itemGuid, price);
                if (SQLHelper?.Success ?? false)
                {
                    msg = "";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 获取交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法获取报价，请稍后再试。";
            if (SQLHelper != null && requestData.Count >= 1)
            {
                long offerId = DataRequest.GetDictionaryJsonObject<long>(requestData, "id");
                bool apiQuery = DataRequest.GetDictionaryJsonObject<bool>(requestData, "apiQuery");
                Offer? offer = SQLHelper.GetOffer(offerId);
                if (offer != null)
                {
                    // 检查当前用户是否有权限查看（报价创建者或接收者）允许管理员使用 API 查询报价
                    long userId = Server.User.Id;
                    if ((apiQuery && Server.User.IsAdmin) || offer.Offeror == userId || offer.Offeree == userId)
                    {
                        resultData.Add("offer", offer);
                        msg = "";
                    }
                    else
                    {
                        msg = "您无权查看此报价。";
                    }
                }
                else
                {
                    msg = "报价不存在。";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 创建交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MakeOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法创建报价，请稍后再试。";
            if (SQLHelper != null && requestData.Count >= 1)
            {
                long offeree = DataRequest.GetDictionaryJsonObject<long>(requestData, "offeree");
                long offeror = Server.User.Id;
                if (offeror != 0 && offeree != 0 && offeror != offeree)
                {
                    SQLHelper.AddOffer(offeror, offeree);
                    if (SQLHelper.Success)
                    {
                        long offerId = SQLHelper.LastInsertId;
                        Offer? offer = SQLHelper.GetOffer(offerId);
                        if (offer != null)
                        {
                            resultData.Add("offer", offer);
                            msg = "";
                        }
                    }
                }
                else
                {
                    msg = "无效的用户ID。";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 修改交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void ReviseOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法修改报价，请稍后再试。";
            if (SQLHelper != null && requestData.Count >= 3)
            {
                long offerId = DataRequest.GetDictionaryJsonObject<long>(requestData, "id");
                OfferActionType action = DataRequest.GetDictionaryJsonObject<OfferActionType>(requestData, "action");
                List<Guid> offerorItems = DataRequest.GetDictionaryJsonObject<List<Guid>>(requestData, "offerorItems") ?? [];
                List<Guid> offereeItems = DataRequest.GetDictionaryJsonObject<List<Guid>>(requestData, "offereeItems") ?? [];
                long userId = Server.User.Id;

                Offer? offer = SQLHelper.GetOffer(offerId);
                if (offer != null && (offer.Offeror == userId || offer.Offeree == userId))
                {
                    try
                    {
                        SQLHelper.NewTransaction();

                        bool isOfferor = offer.Offeror == userId;
                        bool canProceed = false;

                        // 根据 action 处理状态
                        switch (action)
                        {
                            case OfferActionType.OfferorRevise:
                                if (isOfferor && (offer.Status == OfferState.Created || offer.Status == OfferState.Negotiating))
                                {
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.PendingOfferorConfirmation);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许发起方修改。";
                                break;

                            case OfferActionType.OfferorConfirm:
                                if (isOfferor && offer.Status == OfferState.PendingOfferorConfirmation)
                                {
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.OfferorConfirmed);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许发起方确认。";
                                break;

                            case OfferActionType.OfferorSend:
                                if (isOfferor && offer.Status == OfferState.OfferorConfirmed)
                                {
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.Sent);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许发起方发送。";
                                break;

                            case OfferActionType.OfferorCancel:
                                if (isOfferor && offer.Status != OfferState.Completed && offer.Status != OfferState.Rejected && offer.Status != OfferState.Cancelled && offer.Status != OfferState.Expired)
                                {
                                    SQLHelper.DeleteOfferItemsBackupByOfferId(offerId);
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.Cancelled);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许发起方取消。";
                                break;

                            case OfferActionType.OfferorAccept:
                                if (isOfferor && offer.Status == OfferState.Negotiating)
                                {
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.NegotiationAccepted);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许发起方同意。";
                                break;

                            case OfferActionType.OffereeRevise:
                                // 接收方修改报价
                                if (!isOfferor && offer.NegotiatedTimes >= 3)
                                {
                                    msg = "当前协商次数已达上限（3次），不允许接收方修改。";
                                }
                                else if (!isOfferor && (offer.Status == OfferState.Sent || offer.Status == OfferState.NegotiationAccepted))
                                {
                                    // 备份
                                    SQLHelper.BackupOfferItem(offer);
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.PendingOffereeConfirmation);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许接收方修改。";
                                break;

                            case OfferActionType.OffereeConfirm:
                                if (!isOfferor && offer.Status == OfferState.PendingOffereeConfirmation)
                                {
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.OffereeConfirmed);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许接收方确认。";
                                break;

                            case OfferActionType.OffereeSend:
                                if (!isOfferor && (offer.Status == OfferState.OffereeConfirmed))
                                {
                                    if (offer.NegotiatedTimes < 3)
                                    {
                                        SQLHelper.UpdateOfferStatus(offerId, OfferState.Negotiating);
                                        SQLHelper.UpdateOfferNegotiatedTimes(offerId, offer.NegotiatedTimes + 1);
                                        canProceed = true;
                                    }
                                    else msg = "当前协商次数已达上限（3次），不允许接收方发送。";
                                }
                                else msg = "当前状态不允许接收方修改。";
                                break;

                            default:
                                msg = "无效的操作类型。";
                                break;
                        }

                        if (canProceed)
                        {
                            // 更新物品，同时对物品进行检查
                            User? offeree = SQLHelper.GetUserById(offer.Offeree, true);
                            User? offeror = SQLHelper.GetUserById(offer.Offeror, true);
                            if (offeree != null && offeror != null)
                            {
                                SQLHelper.DeleteOfferItemsByOfferId(offerId);
                                foreach (Guid itemGuid in offerorItems)
                                {
                                    if (offeror.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                                    {
                                        if (!item.IsTradable)
                                        {
                                            msg = $"此物品无法交易{(item.NextTradableTime != DateTime.MinValue ? $"，此物品将在 {item.NextTradableTime.ToString(General.GeneralDateTimeFormatChinese)} 后可交易" : "")}。";
                                            break;
                                        }
                                        else
                                        {
                                            SQLHelper.AddOfferItem(offerId, offer.Offeror, item.Guid);
                                        }
                                    }
                                }
                                foreach (Guid itemGuid in offereeItems)
                                {
                                    if (offeree.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                                    {
                                        if (!item.IsTradable)
                                        {
                                            msg = $"此物品无法交易{(item.NextTradableTime != DateTime.MinValue ? $"，此物品将在 {item.NextTradableTime.ToString(General.GeneralDateTimeFormatChinese)} 后可交易" : "")}。";
                                            break;
                                        }
                                        else
                                        {
                                            SQLHelper.AddOfferItem(offerId, offer.Offeree, item.Guid);
                                        }
                                    }
                                }

                                if (msg == "")
                                {
                                    offer = SQLHelper.GetOffer(offerId);
                                    if (offer != null)
                                    {
                                        SQLHelper.Commit();
                                        resultData.Add("offer", offer);
                                    }
                                }
                            }
                        }

                        if (msg != "")
                        {
                            SQLHelper.Rollback();
                        }
                    }
                    catch (Exception e)
                    {
                        SQLHelper.Rollback();
                        ServerHelper.Error(e);
                        msg = "修改报价时发生错误，请稍后再试。";
                    }
                }
                else
                {
                    msg = "报价不存在或您无权修改。";
                }
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 回应交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void RespondOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            string msg = "无法回应报价，请稍后再试。";
            if (SQLHelper != null && requestData.Count >= 2) // 只需要 offerId 和 action
            {
                long offerId = DataRequest.GetDictionaryJsonObject<long>(requestData, "id");
                OfferActionType action = DataRequest.GetDictionaryJsonObject<OfferActionType>(requestData, "action");
                long userId = Server.User.Id;

                Offer? offer = SQLHelper.GetOffer(offerId);
                if (offer != null && offer.Offeree == userId)
                {
                    bool canProceed = false;
                    bool isNegotiating = false;

                    try
                    {
                        SQLHelper.NewTransaction();

                        // 根据 action 处理状态
                        switch (action)
                        {
                            case OfferActionType.OffereeAccept:
                                if (offer.Status == OfferState.Sent || offer.Status == OfferState.Negotiating || offer.Status == OfferState.NegotiationAccepted)
                                {
                                    if (offer.Status == OfferState.Negotiating)
                                    {
                                        isNegotiating = true;
                                    }
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.Completed);
                                    SQLHelper.UpdateOfferFinishTime(offerId, DateTime.Now);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许接受。";
                                break;

                            case OfferActionType.OffereeReject:
                                if (offer.Status == OfferState.Sent || offer.Status == OfferState.Negotiating || offer.Status == OfferState.NegotiationAccepted)
                                {
                                    SQLHelper.UpdateOfferStatus(offerId, OfferState.Rejected);
                                    SQLHelper.UpdateOfferFinishTime(offerId, DateTime.Now);
                                    canProceed = true;
                                }
                                else msg = "当前状态不允许拒绝。";
                                break;

                            default:
                                msg = "无效的操作类型。";
                                break;
                        }

                        if (canProceed)
                        {
                            offer = SQLHelper.GetOffer(offerId, isNegotiating);
                            if (offer != null)
                            {
                                if (offer.Status == OfferState.Completed)
                                {
                                    User? offeree = SQLHelper.GetUserById(offer.Offeree, true);
                                    User? offeror = SQLHelper.GetUserById(offer.Offeror, true);
                                    if (offeree != null && offeror != null)
                                    {
                                        foreach (Guid itemGuid in offer.OffereeItems)
                                        {
                                            if (offeree.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                                            {
                                                item.EntityState = EntityState.Deleted;

                                                Item newItem = item.Copy();
                                                newItem.User = offeror;
                                                newItem.IsSellable = false;
                                                newItem.IsTradable = false;
                                                newItem.NextSellableTime = DateTimeUtility.GetTradableTime();
                                                newItem.NextTradableTime = DateTimeUtility.GetTradableTime();
                                                newItem.EntityState = EntityState.Added;
                                                offeror.Inventory.Items.Add(newItem);
                                            }
                                        }
                                        foreach (Guid itemGuid in offer.OfferorItems)
                                        {
                                            if (offeror.Inventory.Items.FirstOrDefault(i => i.Guid == itemGuid) is Item item)
                                            {
                                                item.EntityState = EntityState.Deleted;

                                                Item newItem = item.Copy();
                                                newItem.User = offeree;
                                                newItem.IsSellable = false;
                                                newItem.IsTradable = false;
                                                newItem.NextSellableTime = DateTimeUtility.GetTradableTime();
                                                newItem.NextTradableTime = DateTimeUtility.GetTradableTime();
                                                newItem.EntityState = EntityState.Added;
                                                offeree.Inventory.Items.Add(newItem);
                                            }
                                        }
                                        SQLHelper.UpdateInventory(offeror.Inventory);
                                        SQLHelper.UpdateInventory(offeree.Inventory);
                                        SQLHelper.Commit();
                                        resultData.Add("offer", offer);
                                        msg = "";
                                    }
                                }
                            }
                        }

                        if (msg != "")
                        {
                            SQLHelper.Rollback();
                        }
                    }
                    catch (Exception e)
                    {
                        SQLHelper.Rollback();
                        ServerHelper.Error(e);
                        msg = "回应报价时发生错误，请稍后再试。";
                    }
                }
                else
                {
                    msg = "报价不存在或您无权回应。";
                }
            }
            resultData.Add("msg", msg);
        }

        #endregion
    }
}
