using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Addon;
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

                case DataRequestType.Room_GetRoomSettings:
                    GetRoomSettings(data, result);
                    break;

                case DataRequestType.Room_GetRoomPlayerCount:
                    GetRoomPlayerCount(data, result);
                    break;

                case DataRequestType.Room_UpdateRoomMaster:
                    await UpdateRoomMaster(data, result);
                    break;

                case DataRequestType.UserCenter_GetUserProfile:
                    GetUserProfile(data, result);
                    break;

                case DataRequestType.UserCenter_GetUserStatistics:
                    GetUserStatistics(data, result);
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

                case DataRequestType.Inventory_GetInventory:
                    GetInventory(data, result);
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
                if (Server.IsLoginKey(key))
                {
                    // 从玩家列表移除
                    Server.RemoveUser();
                    Server.GetUsersCount();
                    msg = "你已成功退出登录！ ";
                }
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
                RoomType type = DataRequest.GetDictionaryJsonObject<RoomType>(requestData, "roomtype");
                string gamemodule = DataRequest.GetDictionaryJsonObject<string>(requestData, "gamemoduleserver") ?? "";
                string gamemap = DataRequest.GetDictionaryJsonObject<string>(requestData, "gamemap") ?? "";
                bool isrank = DataRequest.GetDictionaryJsonObject<bool>(requestData, "isrank");
                ServerHelper.WriteLine("[CreateRoom] " + RoomSet.GetTypeString(type) + " (" + string.Join(", ", [gamemodule, gamemap]) + ")", InvokeMessageType.DataRequest);
                if (gamemodule == "" || gamemap == "" || FunGameSystem.GameModuleLoader is null || !FunGameSystem.GameModuleLoader.ModuleServers.ContainsKey(gamemodule) || !FunGameSystem.GameModuleLoader.Maps.ContainsKey(gamemap))
                {
                    ServerHelper.WriteLine("缺少对应的模组或地图，无法创建房间。");
                    resultData.Add("room", room);
                    return;
                }
                User user = DataRequest.GetDictionaryJsonObject<User>(requestData, "master") ?? Factory.GetUser();
                string password = DataRequest.GetDictionaryJsonObject<string>(requestData, "password") ?? "";
                int maxusers = DataRequest.GetDictionaryJsonObject<int>(requestData, "maxusers");

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
                        SQLHelper.Execute(RoomQuery.Insert_CreateRoom(SQLHelper, roomid, user.Id, type, gamemodule, gamemap, isrank, password, maxusers));
                        if (SQLHelper.Result == SQLResult.Success)
                        {
                            ServerHelper.WriteLine("[CreateRoom] Master: " + user.Username + " RoomID: " + roomid);
                            DataRow? dr = SQLHelper.ExecuteDataRow(RoomQuery.Select_IsExistRoom(SQLHelper, roomid));
                            if (dr != null)
                            {
                                room = Factory.GetRoom(dr, user);
                                FunGameSystem.RoomList.AddRoom(room);
                            }
                        }
                    }
                }
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

                if (roomid != "-1" && FunGameSystem.RoomList.IsExist(roomid))
                {
                    result = await Server.QuitRoom(roomid, isMaster);
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
                    if (SQLHelper != null)
                    {
                        SQLHelper.ExecuteDataSet(RoomQuery.Select_IsExistRoom(SQLHelper, roomid));
                        if (SQLHelper.Success)
                        {
                            FunGameSystem.RoomList.IntoRoom(roomid, Server.User);
                            Server.InRoom = FunGameSystem.RoomList[roomid];
                            await Server.SendClients(Server.Listener.ClientList.Where(c => c != null && roomid == c.InRoom.Roomid && c.User.Id != 0),
                                SocketMessageType.Chat, Server.User.Username, DateTimeUtility.GetNowShortTime() + " [ " + Server.User.Username + " ] 进入了房间。");
                            result = true;
                        }
                        else
                        {
                            FunGameSystem.RoomList.RemoveRoom(roomid);
                        }
                    }
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
                bool iscancel = DataRequest.GetDictionaryJsonObject<bool>(requestData, "iscancel");
                if (!iscancel)
                {
                    ServerHelper.WriteLine("[MatchRoom] Start", InvokeMessageType.DataRequest);
                    RoomType type = DataRequest.GetDictionaryJsonObject<RoomType>(requestData, "roomtype");
                    User user = DataRequest.GetDictionaryJsonObject<User>(requestData, "matcher") ?? Factory.GetUser();
                    StartMatching(type, user);
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
                if (msg.Trim() != "")
                {
                    await Server.SendClients(Server.Listener.ClientList.Where(c => c != null && Server.InRoom.Roomid == c.InRoom.Roomid && c.User.Id != 0),
                        SocketMessageType.Chat, Server.User.Username, msg);
                }
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
                    Server.NowGamingServer = FunGameSystem.GameModuleLoader.GetServerMode(room.GameModule);
                    Dictionary<string, IServerModel> all = Server.Listener.UserList.Cast<IServerModel>().ToDictionary(k => k.User.Username, v => v);
                    // 给其他玩家赋值模组服务器
                    foreach (IServerModel model in all.Values.Where(s => s.User.Username != Server.User.Username))
                    {
                        model.NowGamingServer = Server.NowGamingServer;
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
                string username = DataRequest.GetDictionaryJsonObject<string>(requestData, ForgetVerifyCodes.Column_Username) ?? "";
                string email = DataRequest.GetDictionaryJsonObject<string>(requestData, ForgetVerifyCodes.Column_Email) ?? "";
                string verifycode = DataRequest.GetDictionaryJsonObject<string>(requestData, ForgetVerifyCodes.Column_ForgetVerifyCode) ?? "";

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
        /// 获取房间设置
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private static void GetRoomSettings(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
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

                if (roomid != "-1" && FunGameSystem.RoomList.IsExist(roomid) && newMaster.Id != 0)
                {
                    Room room = FunGameSystem.RoomList[roomid];
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
        /// 获取用户资料信息
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetUserProfile(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 获取用户统计数据
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetUserStatistics(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 更新用户（全部数据）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdateUser(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
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
                string username = DataRequest.GetDictionaryJsonObject<string>(requestData, UserQuery.Column_Username) ?? "";
                string password = DataRequest.GetDictionaryJsonObject<string>(requestData, UserQuery.Column_Password) ?? "";
                if (username.Trim() != "" && password.Trim() != "")
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
            }
            resultData.Add("msg", msg);
        }

        /// <summary>
        /// 每日签到
        /// </summary>
        /// <param name="resultData"></param>
        private void DailySignIn(Dictionary<string, object> resultData)
        {
            if (SQLHelper != null)
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
                            resultData.Add("msg", $"签到成功！你已经连续签到 {days} 天！");
                            return;
                        }
                    }
                }
            }
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
            // TODO
        }

        /// <summary>
        /// 获取市场信息
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetMarket(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 购买物品（商店）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void StoreBuy(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 购买物品（市场）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MarketBuy(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 获取库存信息
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetInventory(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 使用物品
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void Use(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 出售物品（商店）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void StoreSell(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 出售物品（市场）
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MarketSell(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 更新市场价格
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void UpdateMarketPrice(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 获取交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void GetOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 创建交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void MakeOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 修改交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void ReviseOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        /// <summary>
        /// 回应交易报价
        /// </summary>
        /// <param name="requestData"></param>
        /// <param name="resultData"></param>
        private void RespondOffer(Dictionary<string, object> requestData, Dictionary<string, object> resultData)
        {
            // TODO
        }

        #endregion
    }
}
