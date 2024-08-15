using System.Collections;
using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Controller
{
    public class DataRequestController
    {
        public ServerModel Server { get; }
        public MySQLHelper SQLHelper => Server.SQLHelper ?? throw new MySQLConfigException();
        public MailSender? MailSender => Server.MailSender;
        public Authenticator Authenticator { get; }
        public DataRequestType LastRequest => _LastRequest;

        private string ForgetVerify = "";
        private string RegVerify = "";
        private DataRequestType _LastRequest = DataRequestType.UnKnown;
        private readonly bool[] isReadyCheckCD = [false, false];

        public DataRequestController(ServerModel server)
        {
            Server = server;
            Authenticator = new(Server, SQLHelper, MailSender);
        }

        public Hashtable GetResultData(DataRequestType type, Hashtable data)
        {
            Hashtable result = [];
            _LastRequest = type;

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
                    IntoRoom(data, result);
                    break;

                case DataRequestType.Main_QuitRoom:
                    QuitRoom(data, result);
                    break;

                case DataRequestType.Main_MatchRoom:
                    MatchRoom(data, result);
                    break;

                case DataRequestType.Main_Chat:
                    Chat(data);
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

                case DataRequestType.Reg_GetRegVerifyCode:
                    Reg(data, result);
                    break;

                case DataRequestType.Login_Login:
                    Login(data, result);
                    break;

                case DataRequestType.Login_GetFindPasswordVerifyCode:
                    ForgetPassword(data, result);
                    break;

                case DataRequestType.Login_UpdatePassword:
                    UpdatePassword(data, result);
                    break;

                case DataRequestType.Room_GetRoomSettings:
                    break;

                case DataRequestType.Room_GetRoomPlayerCount:
                    GetRoomPlayerCount(data, result);
                    break;

                case DataRequestType.Room_UpdateRoomMaster:
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
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void LogOut(Hashtable RequestData, Hashtable ResultData)
        {
            string msg = "";
            Guid key = Guid.Empty;
            if (RequestData.Count >= 1)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                key = DataRequest.GetHashtableJsonObject<Guid>(RequestData, "key");
                if (Server.IsLoginKey(key))
                {
                    Server.LogOut();
                    msg = "你已成功退出登录！ ";
                }
            }
            ResultData.Add("msg", msg);
            ResultData.Add("key", key);
        }

        #endregion

        #region Main

        /// <summary>
        /// 获取公告
        /// </summary>
        /// <param name="ResultData"></param>
        private void GetServerNotice(Hashtable ResultData)
        {
            ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
            ResultData.Add("notice", Config.ServerNotice);
        }

        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void CreateRoom(Hashtable RequestData, Hashtable ResultData)
        {
            Room room = General.HallInstance;
            if (RequestData.Count >= 3)
            {
                RoomType type = DataRequest.GetHashtableJsonObject<RoomType>(RequestData, "roomtype");
                string GameModule = DataRequest.GetHashtableJsonObject<string>(RequestData, "GameModule") ?? "";
                string gamemap = DataRequest.GetHashtableJsonObject<string>(RequestData, "gamemap") ?? "";
                bool isrank = DataRequest.GetHashtableJsonObject<bool>(RequestData, "isrank");
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest) + " : " + RoomSet.GetTypeString(type) + " (" + string.Join(", ", [GameModule, gamemap]) + ")", InvokeMessageType.DataRequest);
                if (GameModule == "" || gamemap == "")
                {
                    ServerHelper.WriteLine("缺少对应的模组或地图，无法创建房间。");
                    ResultData.Add("room", room);
                    return;
                }
                User user = DataRequest.GetHashtableJsonObject<User>(RequestData, "master") ?? Factory.GetUser();
                string password = DataRequest.GetHashtableJsonObject<string>(RequestData, "password") ?? "";

                if (user.Id != 0)
                {
                    string roomid = Verification.CreateVerifyCode(VerifyCodeType.MixVerifyCode, 7).ToUpper();
                    SQLHelper.Execute(RoomQuery.Insert_CreateRoom(roomid, user.Id, type, GameModule, gamemap, isrank, password ?? ""));
                    if (SQLHelper.Result == SQLResult.Success)
                    {
                        ServerHelper.WriteLine("[CreateRoom] Master: " + user.Username + " RoomID: " + roomid);
                        SQLHelper.ExecuteDataSet(RoomQuery.Select_IsExistRoom(roomid));
                        if (SQLHelper.Result == SQLResult.Success && SQLHelper.DataSet.Tables[0].Rows.Count > 0)
                        {
                            room = Factory.GetRoom(SQLHelper.DataSet.Tables[0].Rows[0], user);
                            Config.RoomList.AddRoom(room);
                        }
                    }
                }
            }
            ResultData.Add("room", room);
        }

        /// <summary>
        /// 更新房间列表
        /// </summary>
        /// <param name="ResultData"></param>
        private void UpdateRoom(Hashtable ResultData)
        {
            ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
            ResultData.Add("rooms", Config.RoomList.ListRoom); // 传RoomList
        }

        /// <summary>
        /// 退出房间，并更新房主
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void QuitRoom(Hashtable RequestData, Hashtable ResultData)
        {
            bool result = false;
            if (RequestData.Count >= 2)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string roomid = DataRequest.GetHashtableJsonObject<string>(RequestData, "roomid") ?? "-1";
                bool isMaster = DataRequest.GetHashtableJsonObject<bool>(RequestData, "isMaster");

                if (roomid != "-1" && Config.RoomList.IsExist(roomid))
                {
                    result = Server.QuitRoom(roomid, isMaster);
                }
            }
            ResultData.Add("result", result);
        }

        /// <summary>
        /// 进入房间
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void IntoRoom(Hashtable RequestData, Hashtable ResultData)
        {
            bool result = false;
            if (RequestData.Count >= 1)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string roomid = DataRequest.GetHashtableJsonObject<string>(RequestData, "roomid") ?? "-1";

                if (roomid != "-1")
                {
                    SQLHelper.ExecuteDataSet(RoomQuery.Select_IsExistRoom(roomid));
                    if (SQLHelper.Success)
                    {
                        Config.RoomList.IntoRoom(roomid, Server.User);
                        Server.IntoRoom(roomid);
                        result = true;
                    }
                    else
                    {
                        Config.RoomList.RemoveRoom(roomid);
                    }
                }
            }
            ResultData.Add("result", result);
        }

        /// <summary>
        /// 匹配房间
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void MatchRoom(Hashtable RequestData, Hashtable ResultData)
        {
            bool result = true;
            if (RequestData.Count >= 1)
            {
                bool iscancel = DataRequest.GetHashtableJsonObject<bool>(RequestData, "iscancel");
                if (!iscancel)
                {
                    ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest) + " : Start", InvokeMessageType.DataRequest);
                    RoomType type = DataRequest.GetHashtableJsonObject<RoomType>(RequestData, "roomtype");
                    User user = DataRequest.GetHashtableJsonObject<User>(RequestData, "matcher") ?? Factory.GetUser();
                    Server.StartMatching(type, user);
                }
                else
                {
                    // 取消匹配
                    ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest) + " : Cancel", InvokeMessageType.DataRequest);
                    Server.StopMatching();
                }
            }
            ResultData.Add("result", result);
        }

        /// <summary>
        /// 设置已准备状态
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void SetReady(Hashtable RequestData, Hashtable ResultData)
        {
            bool result = false;
            string roomid = "-1";
            if (RequestData.Count >= 1)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                roomid = DataRequest.GetHashtableJsonObject<string>(RequestData, "roomid") ?? "-1";
                User user = Server.User;

                if (roomid != "-1" && user.Id != 0 && !Config.RoomList.GetReadyPlayerList(roomid).Contains(user))
                {
                    Config.RoomList.SetReady(roomid, user);
                    result = true;
                }
            }
            ResultData.Add("result", result);
            ResultData.Add("ready", Config.RoomList.GetReadyPlayerList(roomid));
            ResultData.Add("notready", Config.RoomList.GetNotReadyPlayerList(roomid));
        }

        /// <summary>
        /// 取消已准备状态
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void CancelReady(Hashtable RequestData, Hashtable ResultData)
        {
            bool result = false;
            string roomid = "-1";
            if (RequestData.Count >= 1)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                roomid = DataRequest.GetHashtableJsonObject<string>(RequestData, "roomid") ?? "-1";
                User user = Server.User;

                if (roomid != "-1" && user.Id != 0 && Config.RoomList.GetReadyPlayerList(roomid).Contains(user))
                {
                    Config.RoomList.CancelReady(roomid, user);
                    result = true;
                }
            }
            ResultData.Add("result", result);
            ResultData.Add("ready", Config.RoomList.GetReadyPlayerList(roomid));
            ResultData.Add("notready", Config.RoomList.GetNotReadyPlayerList(roomid));
        }

        /// <summary>
        /// 发送聊天消息
        /// </summary>
        /// <param name="RequestData"></param>
        private void Chat(Hashtable RequestData)
        {
            if (RequestData.Count >= 1)
            {
                string msg = DataRequest.GetHashtableJsonObject<string>(RequestData, "msg") ?? "";
                if (msg.Trim() != "") Server.Chat(msg);
            }
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void StartGame(Hashtable RequestData, Hashtable ResultData)
        {
            bool result = false;
            if (RequestData.Count >= 2)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string roomid = DataRequest.GetHashtableJsonObject<string>(RequestData, "roomid") ?? "-1";
                bool isMaster = DataRequest.GetHashtableJsonObject<bool>(RequestData, "isMaster");

                if (roomid != "-1")
                {
                    if (isMaster)
                    {
                        string[] usernames = Config.RoomList.GetNotReadyPlayerList(roomid).Select(user => user.Username).ToArray();
                        if (usernames.Length > 0)
                        {
                            if (isReadyCheckCD[0] == false)
                            {
                                // 提醒玩家准备
                                Server.SendSystemMessage(ShowMessageType.None, "还有玩家尚未准备，无法开始游戏。", "", 0, Server.User.Username);
                                Server.SendSystemMessage(ShowMessageType.Tip, "房主即将开始游戏，请准备！", "请准备就绪", 10, usernames);
                                isReadyCheckCD[0] = true;
                                TaskUtility.RunTimer(() =>
                                {
                                    isReadyCheckCD[0] = false;
                                }, 15000);
                            }
                            else
                            {
                                Server.SendSystemMessage(ShowMessageType.None, "还有玩家尚未准备，无法开始游戏。15秒内只能发送一次准备提醒。", "", 0, Server.User.Username);
                            }
                        }
                        else
                        {
                            List<User> users = Config.RoomList.GetPlayerList(roomid);
                            if (users.Count < 2)
                            {
                                Server.SendSystemMessage(ShowMessageType.None, "玩家数量不足，无法开始游戏。", "", 0, Server.User.Username);
                            }
                            else
                            {
                                usernames = users.Select(user => user.Username).ToArray();
                                Server.SendSystemMessage(ShowMessageType.None, "所有玩家均已准备，游戏将在10秒后开始。", "", 0, usernames);
                                Server.StartGame(roomid, users, usernames);
                                result = true;
                            }
                        }
                    }
                    else if (isReadyCheckCD[1] == false)
                    {
                        // 提醒房主开始游戏
                        Server.SendSystemMessage(ShowMessageType.None, "已提醒房主立即开始游戏。", "", 0, Server.User.Username);
                        Server.SendSystemMessage(ShowMessageType.Tip, "房间中的玩家已请求你立即开始游戏。", "请求开始", 10, Config.RoomList[roomid].RoomMaster.Username);
                        isReadyCheckCD[1] = true;
                        TaskUtility.RunTimer(() =>
                        {
                            isReadyCheckCD[1] = false;
                        }, 15000);
                    }
                    else
                    {
                        Server.SendSystemMessage(ShowMessageType.None, "15秒内只能发送一次提醒，请稍后再试。", "", 0, Server.User.Username);
                    }
                }
            }
            ResultData.Add("result", result);
        }

        #endregion

        #region Reg

        /// <summary>
        /// 接收并验证注册验证码
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void Reg(Hashtable RequestData, Hashtable ResultData)
        {
            string msg = "";
            RegInvokeType returnType = RegInvokeType.None;
            bool success = false;
            if (RequestData.Count >= 4)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string username = DataRequest.GetHashtableJsonObject<string>(RequestData, "username") ?? "";
                string password = DataRequest.GetHashtableJsonObject<string>(RequestData, "password") ?? "";
                string email = DataRequest.GetHashtableJsonObject<string>(RequestData, "email") ?? "";
                string verifycode = DataRequest.GetHashtableJsonObject<string>(RequestData, "verifycode") ?? "";

                // 如果没发验证码，就生成验证码
                if (verifycode.Trim() == "")
                {
                    // 先检查账号是否重复
                    SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(username));
                    if (SQLHelper.Result == SQLResult.Success)
                    {
                        ServerHelper.WriteLine(Server.GetClientName() + " 账号已被注册");
                        msg = "此账号名已被使用！";
                        returnType = RegInvokeType.DuplicateUserName;
                    }
                    else
                    {
                        // 检查邮箱是否重复
                        SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistEmail(email));
                        if (SQLHelper.Result == SQLResult.Success)
                        {
                            ServerHelper.WriteLine(Server.GetClientName() + " 邮箱已被注册");
                            msg = "此邮箱已被注册！";
                            returnType = RegInvokeType.DuplicateEmail;
                        }
                        else
                        {
                            // 检查验证码是否发送过
                            SQLHelper.ExecuteDataSet(RegVerifyCodes.Select_HasSentRegVerifyCode(username, email));
                            if (SQLHelper.Result == SQLResult.Success)
                            {
                                DateTime RegTime = (DateTime)SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime];
                                string RegVerifyCode = (string)SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode];
                                if ((DateTime.Now - RegTime).TotalMinutes < 10)
                                {
                                    ServerHelper.WriteLine(Server.GetClientName() + $" 十分钟内已向{email}发送过验证码：{RegVerifyCode}");
                                }
                                returnType = RegInvokeType.InputVerifyCode;
                            }
                            else
                            {
                                // 发送验证码，需要先删除之前过期的验证码
                                SQLHelper.NewTransaction();
                                SQLHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(username, email));
                                RegVerify = Verification.CreateVerifyCode(VerifyCodeType.NumberVerifyCode, 6);
                                SQLHelper.Execute(RegVerifyCodes.Insert_RegVerifyCode(username, email, RegVerify));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    SQLHelper.Commit();
                                    if (MailSender != null)
                                    {
                                        // 发送验证码
                                        string ServerName = Config.ServerName;
                                        string Subject = $"[{ServerName}] FunGame 注册验证码";
                                        string Body = $"亲爱的 {username}， <br/>    感谢您注册[{ServerName}]，您的验证码是 {RegVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.LongDateOnly)}";
                                        string[] To = [email];
                                        if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                                        {
                                            ServerHelper.WriteLine(Server.GetClientName() + $" 已向{email}发送验证码：{RegVerify}");
                                        }
                                        else
                                        {
                                            ServerHelper.WriteLine(Server.GetClientName() + " 无法发送验证码", InvokeMessageType.Error);
                                            ServerHelper.WriteLine(MailSender.ErrorMsg, InvokeMessageType.Error);
                                        }
                                    }
                                    else // 不使用MailSender的情况
                                    {
                                        ServerHelper.WriteLine(Server.GetClientName() + $" 验证码为：{RegVerify}，请服务器管理员告知此用户");
                                    }
                                    returnType = RegInvokeType.InputVerifyCode;
                                }
                                else SQLHelper.Rollback();
                            }
                        }
                    }
                }
                else
                {
                    // 先检查验证码
                    SQLHelper.ExecuteDataSet(RegVerifyCodes.Select_RegVerifyCode(username, email, verifycode));
                    if (SQLHelper.Result == SQLResult.Success)
                    {
                        // 检查验证码是否过期
                        DateTime RegTime = (DateTime)SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime];
                        if ((DateTime.Now - RegTime).TotalMinutes >= 10)
                        {
                            ServerHelper.WriteLine(Server.GetClientName() + " 验证码已过期");
                            msg = "此验证码已过期，请重新注册。";
                            SQLHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(username, email));
                        }
                        else
                        {
                            // 注册
                            if (RegVerify.Equals(SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode]))
                            {
                                SQLHelper.NewTransaction();
                                ServerHelper.WriteLine("[Reg] UserName: " + username + " Email: " + email);
                                SQLHelper.Execute(UserQuery.Insert_Register(username, password, email, Server.Socket?.ClientIP ?? ""));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    success = true;
                                    msg = "注册成功！请牢记您的账号与密码！";
                                    SQLHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(username, email));
                                    SQLHelper.Commit();
                                }
                                else
                                {
                                    SQLHelper.Rollback();
                                    msg = "服务器无法处理您的注册，注册失败！";
                                }
                            }
                            else msg = "验证码不正确，请重新输入！";
                        }
                    }
                    else if (SQLHelper.Result == SQLResult.NotFound) msg = "验证码不正确，请重新输入！";
                    else msg = "服务器无法处理您的注册，注册失败！";
                }
            }
            ResultData.Add("msg", msg);
            ResultData.Add("type", returnType);
            ResultData.Add("success", success);
        }

        #endregion

        #region Login

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void Login(Hashtable RequestData, Hashtable ResultData)
        {
            string msg = "";
            User user = Factory.GetUser();
            if (RequestData.Count >= 4)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string username = DataRequest.GetHashtableJsonObject<string>(RequestData, "username") ?? "";
                string password = DataRequest.GetHashtableJsonObject<string>(RequestData, "password") ?? "";
                string autokey = DataRequest.GetHashtableJsonObject<string>(RequestData, "autokey") ?? "";
                Guid key = DataRequest.GetHashtableJsonObject<Guid>(RequestData, "key");

                // CheckLogin的情况
                if (key != Guid.Empty)
                {
                    if (Server.IsLoginKey(key))
                    {
                        Server.CheckLogin();
                        user = Server.User;
                    }
                    else ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登录。");
                }
                else
                {
                    // 验证登录
                    if (username != null && password != null)
                    {
                        ServerHelper.WriteLine("[" + DataRequestSet.GetTypeString(DataRequestType.Login_Login) + "] UserName: " + username);
                        SQLHelper.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(username, password));
                        if (SQLHelper.Result == SQLResult.Success)
                        {
                            DataSet DsUser = SQLHelper.DataSet;
                            if (autokey.Trim() != "")
                            {
                                SQLHelper.ExecuteDataSet(UserQuery.Select_CheckAutoKey(username, autokey));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    ServerHelper.WriteLine("[" + DataRequestSet.GetTypeString(DataRequestType.Login_Login) + "] AutoKey: 已确认");
                                }
                                else
                                {
                                    msg = "AutoKey不正确，拒绝自动登录！";
                                    ServerHelper.WriteLine("[" + DataRequestSet.GetTypeString(DataRequestType.Login_Login) + "] " + msg);
                                }
                            }
                            key = Guid.NewGuid();
                            Server.PreLogin(DsUser, username, key);
                            ResultData.Add("key", key);
                        }
                        else
                        {
                            msg = "用户名或密码不正确。";
                            ServerHelper.WriteLine(msg);
                        }
                    }
                }
            }
            ResultData.Add("msg", msg);
            ResultData.Add("user", user);
        }

        /// <summary>
        /// 接收并验证找回密码时的验证码
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void ForgetPassword(Hashtable RequestData, Hashtable ResultData)
        {
            string msg = "无法找回您的密码，请稍后再试。"; // 返回的验证信息
            if (RequestData.Count >= 3)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string username = DataRequest.GetHashtableJsonObject<string>(RequestData, ForgetVerifyCodes.Column_Username) ?? "";
                string email = DataRequest.GetHashtableJsonObject<string>(RequestData, ForgetVerifyCodes.Column_Email) ?? "";
                string verifycode = DataRequest.GetHashtableJsonObject<string>(RequestData, ForgetVerifyCodes.Column_ForgetVerifyCode) ?? "";

                // 客户端发来了验证码就进行验证，没有发就生成
                if (verifycode.Trim() != "")
                {
                    // 先检查验证码
                    SQLHelper.ExecuteDataSet(ForgetVerifyCodes.Select_ForgetVerifyCode(username, email, verifycode));
                    if (SQLHelper.Result == SQLResult.Success)
                    {
                        // 检查验证码是否过期
                        DateTime SendTime = (DateTime)SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_SendTime];
                        if ((DateTime.Now - SendTime).TotalMinutes >= 10)
                        {
                            ServerHelper.WriteLine(Server.GetClientName() + " 验证码已过期");
                            msg = "此验证码已过期，请重新找回密码。";
                            SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(username, email));
                        }
                        else
                        {
                            // 检查验证码是否正确
                            if (ForgetVerify.Equals(SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_ForgetVerifyCode]))
                            {
                                ServerHelper.WriteLine("[ForgerPassword] UserName: " + username + " Email: " + email);
                                SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(username, email));
                                msg = "";
                            }
                            else msg = "验证码不正确，请重新输入！";
                        }
                    }
                    else msg = "验证码不正确，请重新输入！";
                }
                else
                {
                    // 检查账号和邮箱是否匹配
                    SQLHelper.ExecuteDataSet(UserQuery.Select_CheckEmailWithUsername(username, email));
                    if (SQLHelper.Result != SQLResult.Success)
                    {
                        msg = "此邮箱未绑定此账号，请重试！";
                    }
                    else
                    {
                        // 检查验证码是否发送过和是否过期
                        SQLHelper.ExecuteDataSet(ForgetVerifyCodes.Select_HasSentForgetVerifyCode(username, email));
                        if (SQLHelper.Result != SQLResult.Success || (DateTime.Now - ((DateTime)SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_SendTime])).TotalMinutes >= 10)
                        {
                            // 发送验证码，需要先删除之前过期的验证码
                            SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(username, email));
                            ForgetVerify = Verification.CreateVerifyCode(VerifyCodeType.NumberVerifyCode, 6);
                            SQLHelper.Execute(ForgetVerifyCodes.Insert_ForgetVerifyCode(username, email, ForgetVerify));
                            if (SQLHelper.Result == SQLResult.Success)
                            {
                                if (MailSender != null)
                                {
                                    // 发送验证码
                                    string ServerName = Config.ServerName;
                                    string Subject = $"[{ServerName}] FunGame 找回密码验证码";
                                    string Body = $"亲爱的 {username}， <br/>    您正在找回[{ServerName}]账号的密码，您的验证码是 {ForgetVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.LongDateOnly)}";
                                    string[] To = [email];
                                    if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                                    {
                                        ServerHelper.WriteLine(Server.GetClientName() + $" 已向{email}发送验证码：{ForgetVerify}");
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
                                    ServerHelper.WriteLine(Server.GetClientName() + $" 验证码为：{ForgetVerify}，请服务器管理员告知此用户");
                                    msg = "";
                                }
                            }
                        }
                        else
                        {
                            // 发送过验证码且验证码没有过期
                            string ForgetVerifyCode = (string)SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_ForgetVerifyCode];
                            ServerHelper.WriteLine(Server.GetClientName() + $" 十分钟内已向{email}发送过验证码：{ForgetVerifyCode}");
                            msg = "";
                        }
                    }
                }
            }
            ResultData.Add("msg", msg);
        }

        /// <summary>
        /// 更新用户的密码
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void UpdatePassword(Hashtable RequestData, Hashtable ResultData)
        {
            string msg = "无法更新您的密码，请稍后再试。";
            if (RequestData.Count >= 2)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                string username = DataRequest.GetHashtableJsonObject<string>(RequestData, UserQuery.Column_Username) ?? "";
                string password = DataRequest.GetHashtableJsonObject<string>(RequestData, UserQuery.Column_Password) ?? "";
                if (username.Trim() != "" && password.Trim() != "")
                {
                    Server.SQLHelper?.Execute(UserQuery.Update_Password(username, password));
                    if (SQLHelper.Success)
                    {
                        // 更新成功返回空值
                        msg = "";
                    }
                }
            }
            ResultData.Add("msg", msg);
        }

        #endregion

        #region Room

        /// <summary>
        /// 获取房间内玩家数量
        /// </summary>
        /// <param name="RequestData"></param>
        /// <param name="ResultData"></param>
        private void GetRoomPlayerCount(Hashtable RequestData, Hashtable ResultData)
        {
            string roomid = "-1";
            if (RequestData.Count >= 1)
            {
                ServerHelper.WriteLine(Server.GetClientName() + " -> " + DataRequestSet.GetTypeString(_LastRequest), InvokeMessageType.DataRequest);
                roomid = DataRequest.GetHashtableJsonObject<string>(RequestData, "roomid") ?? "-1";
            }
            ResultData.Add("count", Config.RoomList.GetPlayerCount(roomid));
        }

        #endregion
    }
}
