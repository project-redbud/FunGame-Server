using System.Collections;
using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public class ServerModel : IServerModel
    {
        /**
         * Public
         */
        public bool Running => _Running;
        public ClientSocket? Socket => _Socket;
        public Task? Task => _Task;
        public string ClientName => _ClientName;
        public User User => _User;

        /**
         * Private
         */
        private ClientSocket? _Socket = null;
        private bool _Running = false;
        private User _User = General.UnknownUserInstance;
        private Task? _Task = null;
        private string _ClientName = "";

        private Guid CheckLoginKey = Guid.Empty;
        private string RegVerify = "";
        private string ForgetVerify = "";
        private int FailedTimes = 0; // 超过一定次数断开连接
        private string UserName = "";
        private DataSet DsUser = new();
        private string RoomID = ""; 
        private readonly Guid Token;
        private readonly ServerSocket Server;
        private readonly MySQLHelper SQLHelper;
        private readonly MailSender? MailSender;
        private long LoginTime;
        private long LogoutTime;

        public ServerModel(ServerSocket server, ClientSocket socket, bool running)
        {
            Server = server;
            _Socket = socket;
            _Running = running;
            Token = socket.Token;
            SQLHelper = new(this);
            MailSender = SmtpHelper.GetMailSender();
            Config.OnlinePlayersCount++;
            GetUsersCount();
        }

        public bool Read(ClientSocket socket)
        {
            // 接收客户端消息
            try
            {
                SocketObject SocketObject = socket.Receive();
                SocketMessageType type = SocketObject.SocketType;
                Guid token = SocketObject.Token;
                object[] args = SocketObject.Parameters;
                string msg = "";

                // 验证Token
                if (type != SocketMessageType.HeartBeat && token != Token)
                {
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 使用了非法方式传输消息，服务器拒绝回应 -> [" + ServerSocket.GetTypeString(type) + "] ");
                    return false;
                }

                if (type == SocketMessageType.DataRequest)
                {
                    return DataRequestHandler(socket, SocketObject);
                }

                // 如果不等于这些Type，就不会输出一行记录。这些Type有特定的输出。
                SocketMessageType[] IgnoreType = new SocketMessageType[] { SocketMessageType.HeartBeat, SocketMessageType.Login, SocketMessageType.IntoRoom,
                    SocketMessageType.Chat};
                if (!IgnoreType.Contains(type))
                {
                    if (msg.Trim() == "")
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + ServerHelper.MakeClientName(ClientName, User));
                    else
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + ServerHelper.MakeClientName(ClientName, User) + " -> " + msg);
                }

                switch (type)
                {
                    case SocketMessageType.GetNotice:
                        msg = Config.ServerNotice;
                        break;

                    case SocketMessageType.Login:
                        CheckLoginKey = Guid.Empty;
                        if (args != null)
                        {
                            string? username = "", password = "", autokey = "";
                            if (args.Length > 0) username = SocketObject.GetParam<string>(0);
                            if (args.Length > 1) password = SocketObject.GetParam<string>(1);
                            if (args.Length > 2) autokey = SocketObject.GetParam<string>(2);
                            if (username != null && password != null)
                            {
                                ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] UserName: " + username);
                                SQLHelper.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(username, password));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    DsUser = SQLHelper.DataSet;
                                    if (autokey != null && autokey.Trim() != "")
                                    {
                                        SQLHelper.ExecuteDataSet(UserQuery.Select_CheckAutoKey(username, autokey));
                                        if (SQLHelper.Result == SQLResult.Success)
                                        {
                                            ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] AutoKey: 已确认");
                                        }
                                        else
                                        {
                                            msg = "AutoKey不正确，拒绝自动登录！";
                                            ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + msg);
                                            return Send(socket, type, CheckLoginKey, msg);
                                        }
                                    }
                                    UserName = username;
                                    CheckLoginKey = Guid.NewGuid();
                                    return Send(socket, type, CheckLoginKey);
                                }
                                msg = "用户名或密码不正确。";
                                ServerHelper.WriteLine(msg);
                            }
                        }
                        return Send(socket, type, CheckLoginKey, msg);

                    case SocketMessageType.CheckLogin:
                        if (args != null && args.Length > 0)
                        {
                            Guid checkloginkey = SocketObject.GetParam<Guid>(0);
                            if (CheckLoginKey.Equals(checkloginkey))
                            {
                                // 创建User对象
                                _User = Factory.GetUser(DsUser);
                                // 检查有没有重复登录的情况
                                KickUser();
                                // 添加至玩家列表
                                AddUser();
                                GetUsersCount();
                                // CheckLogin
                                LoginTime = DateTime.Now.Ticks;
                                SQLHelper.Execute(UserQuery.Update_CheckLogin(UserName, socket.ClientIP.Split(':')[0]));
                                return Send(socket, type, _User);
                            }
                            ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登录。");
                        }
                        return Send(socket, type, CheckLoginKey.ToString());

                    case SocketMessageType.Logout:
                        Guid checklogoutkey = Guid.Empty;
                        if (args != null && args.Length > 0)
                        {
                            checklogoutkey = SocketObject.GetParam<Guid>(0);
                            if (CheckLoginKey.Equals(checklogoutkey))
                            {
                                // 从玩家列表移除
                                RemoveUser();
                                GetUsersCount();
                                CheckLoginKey = Guid.Empty;
                                msg = "你已成功退出登录！ ";
                                return Send(socket, type, checklogoutkey, msg);
                            }
                        }
                        ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登出。");
                        return Send(socket, type, checklogoutkey);

                    case SocketMessageType.Disconnect:
                        msg = "你已成功断开与服务器的连接: " + Config.ServerName + "。 ";
                        break;

                    case SocketMessageType.HeartBeat:
                        msg = "";
                        break;

                    case SocketMessageType.IntoRoom:
                        msg = "-1";
                        if (args != null && args.Length > 0) msg = SocketObject.GetParam<string>(0)!;
                        RoomID = msg;
                        Config.RoomList.IntoRoom(RoomID, User);
                        if (RoomID != "-1")
                        {
                            // 昭告天下
                            foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
                            {
                                if (RoomID == Client.RoomID)
                                {
                                    if (Client != null && User.Id != 0)
                                    {
                                        Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 进入了房间。");
                                    }
                                }
                            }
                        }
                        break;

                    case SocketMessageType.Chat:
                        if (args != null && args.Length > 0) msg = SocketObject.GetParam<string>(0)!;
                        ServerHelper.WriteLine(msg);
                        foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
                        {
                            if (RoomID == Client.RoomID)
                            {
                                if (Client != null && User.Id != 0)
                                {
                                    Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + msg);
                                }
                            }
                        }
                        return true;

                    case SocketMessageType.Reg:
                        if (args != null)
                        {
                            string? username = "", email = "";
                            if (args.Length > 0) username = SocketObject.GetParam<string>(0);
                            if (args.Length > 1) email = SocketObject.GetParam<string>(1);
                            if (username != null && email != null)
                            {
                                // 先检查账号是否重复
                                SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(username));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 账号已被注册");
                                    return Send(socket, type, RegInvokeType.DuplicateUserName);
                                }
                                // 检查邮箱是否重复
                                SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistEmail(email));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 邮箱已被注册");
                                    return Send(socket, type, RegInvokeType.DuplicateEmail);
                                }
                                // 检查验证码是否发送过
                                SQLHelper.ExecuteDataSet(RegVerifyCodes.Select_HasSentRegVerifyCode(username, email));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    DateTime RegTime = (DateTime)SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime];
                                    string RegVerifyCode = (string)SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode];
                                    if ((DateTime.Now - RegTime).TotalMinutes < 10)
                                    {
                                        ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + $" 十分钟内已向{email}发送过验证码：{RegVerifyCode}");
                                    }
                                    return Send(socket, type, RegInvokeType.InputVerifyCode);
                                }
                                // 发送验证码，需要先删除之前过期的验证码
                                SQLHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(username, email));
                                RegVerify = Verification.CreateVerifyCode(VerifyCodeType.NumberVerifyCode, 6);
                                SQLHelper.Execute(RegVerifyCodes.Insert_RegVerifyCode(username, email, RegVerify));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    if (MailSender != null)
                                    {
                                        // 发送验证码
                                        string ServerName = Config.ServerName;
                                        string Subject = $"[{ServerName}] FunGame 注册验证码";
                                        string Body = $"亲爱的 {username}， <br/>    感谢您注册[{ServerName}]，您的验证码是 {RegVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.DateOnly)}";
                                        string[] To = new string[] { email };
                                        if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                                        {
                                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + $" 已向{email}发送验证码：{RegVerify}");
                                        }
                                        else
                                        {
                                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 无法发送验证码");
                                            ServerHelper.WriteLine(MailSender.ErrorMsg);
                                        }
                                    }
                                    else // 不使用MailSender的情况
                                    {
                                        ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + $" 验证码为：{RegVerify}，请服务器管理员告知此用户");
                                    }
                                    return Send(socket, type, RegInvokeType.InputVerifyCode);
                                }
                            }
                        }
                        return true;

                    case SocketMessageType.CheckReg:
                        if (args != null)
                        {
                            string? username = "", password = "", email = "", verifycode = "";
                            if (args.Length > 0) username = SocketObject.GetParam<string>(0);
                            if (args.Length > 1) password = SocketObject.GetParam<string>(1);
                            if (args.Length > 2) email = SocketObject.GetParam<string>(2);
                            if (args.Length > 3) verifycode = SocketObject.GetParam<string>(3);
                            if (username != null && password != null && email != null && verifycode != null)
                            {
                                // 先检查验证码
                                SQLHelper.ExecuteDataSet(RegVerifyCodes.Select_RegVerifyCode(username, email, verifycode));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    // 检查验证码是否过期
                                    DateTime RegTime = (DateTime)SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime];
                                    if ((DateTime.Now - RegTime).TotalMinutes >= 10)
                                    {
                                        ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 验证码已过期");
                                        msg = "此验证码已过期，请重新注册。";
                                        SQLHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(username, email));
                                        return Send(socket, type, false, msg);
                                    }
                                    // 注册
                                    if (RegVerify.Equals(SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode]))
                                    {
                                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] UserName: " + username + " Email: " + email);
                                        SQLHelper.NewTransaction();
                                        SQLHelper.Execute(UserQuery.Insert_Register(username, password, email, socket.ClientIP));
                                        if (SQLHelper.Result == SQLResult.Success)
                                        {
                                            msg = "注册成功！请牢记您的账号与密码！";
                                            SQLHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(username, email));
                                            SQLHelper.Commit();
                                            return Send(socket, type, true, msg);
                                        }
                                        else
                                        {
                                            msg = "服务器无法处理您的注册，注册失败！";
                                            SQLHelper.Rollback();
                                        }
                                    }
                                    else msg = "验证码不正确，请重新输入！";
                                }
                                else if (SQLHelper.Result == SQLResult.NotFound) msg = "验证码不正确，请重新输入！";
                                else msg = "服务器无法处理您的注册，注册失败！";
                            }
                        }
                        else msg = "注册失败！";
                        return Send(socket, type, false, msg);

                    case SocketMessageType.UpdateRoom:
                        Config.RoomList ??= new();
                        Config.RoomList.Clear();
                        DataSet DsRoomTemp = new(), DsUserTemp = new();
                        DsRoomTemp = SQLHelper.ExecuteDataSet(RoomQuery.Select_Rooms);
                        DsUserTemp = SQLHelper.ExecuteDataSet(UserQuery.Select_Users);
                        List<Room> rooms = Factory.GetRooms(DsRoomTemp, DsUserTemp);
                        Config.RoomList.AddRooms(rooms); // 更新服务器中的房间列表
                        return Send(socket, type, rooms); // 传RoomList

                    case SocketMessageType.CreateRoom:
                        msg = "-1";
                        if (args != null)
                        {
                            string? roomtype_string = "";
                            long userid = 0;
                            string? password = "";
                            if (args.Length > 0) roomtype_string = SocketObject.GetParam<string>(0);
                            if (args.Length > 1) userid = SocketObject.GetParam<long>(1);
                            if (args.Length > 2) password = SocketObject.GetParam<string>(2);
                            if (!string.IsNullOrWhiteSpace(roomtype_string) && userid != 0)
                            {
                                RoomType roomtype = roomtype_string switch
                                {
                                    GameMode.GameMode_Mix => RoomType.Mix,
                                    GameMode.GameMode_Team => RoomType.Team,
                                    GameMode.GameMode_MixHasPass => RoomType.MixHasPass,
                                    GameMode.GameMode_TeamHasPass => RoomType.TeamHasPass,
                                    _ => RoomType.All
                                };
                                string roomid = Verification.CreateVerifyCode(VerifyCodeType.MixVerifyCode, 7).ToUpper();
                                SQLHelper.Execute(RoomQuery.Insert_CreateRoom(roomid, userid, roomtype, password ?? ""));
                                if (SQLHelper.Result == SQLResult.Success)
                                {
                                    msg = roomid;
                                }
                            }
                        }
                        break;

                    case SocketMessageType.QuitRoom:
                        if (args != null)
                        {
                            string? roomid = "";
                            bool isMaster = false;
                            if (args.Length > 0) roomid = SocketObject.GetParam<string>(0);
                            if (args.Length > 1) isMaster = SocketObject.GetParam<bool>(1);
                            if (roomid != null && roomid.Trim() != "")
                            {
                                Config.RoomList.QuitRoom(roomid, User);
                                Room Room = Config.RoomList[roomid] ?? General.HallInstance;
                                User UpdateRoomMaster = General.UnknownUserInstance;
                                DataSet DsUser = new(), DsRoom = new();
                                // 是否是房主
                                if (isMaster)
                                {
                                    List<User> users = GetRoomPlayerList(roomid);
                                    if (users.Count > 0) // 如果此时房间还有人，更新房主
                                    {
                                        UpdateRoomMaster = users[0];
                                        Room.RoomMaster = UpdateRoomMaster;
                                        SQLHelper.Execute(RoomQuery.Update_QuitRoom(roomid, User.Id, UpdateRoomMaster.Id));
                                        DsUser = SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(UpdateRoomMaster.Username));
                                        DsRoom = SQLHelper.ExecuteDataSet(RoomQuery.Select_IsExistRoom(roomid));
                                    }
                                    else // 没人了就解散房间
                                    {
                                        Config.RoomList.RemoveRoom(roomid);
                                        SQLHelper.Execute(RoomQuery.Delete_QuitRoom(roomid, User.Id));
                                        if (SQLHelper.Result == SQLResult.Success)
                                        {
                                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 解散了房间 " + roomid);
                                        }
                                    }
                                }
                                // 昭告天下
                                foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
                                {
                                    if (roomid == Client.RoomID)
                                    {
                                        if (Client != null && User.Id != 0)
                                        {
                                            Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 离开了房间。");
                                            if (UpdateRoomMaster.Id != 0 && Room.Roomid != "-1")
                                            {
                                                Client.Send(Client.Socket!, SocketMessageType.UpdateRoomMaster, User, Room);
                                            }
                                        }
                                    }
                                }
                                RoomID = "-1";
                                return Send(socket, type, true);
                            }
                        }
                        return Send(socket, type, false);

                    case SocketMessageType.MatchRoom:
                        break;

                    case SocketMessageType.ChangeRoomSetting:
                        break;

                    case SocketMessageType.GetRoomPlayerCount:
                        if (args != null)
                        {
                            string? roomid = "-1";
                            if (args.Length > 0) roomid = SocketObject.GetParam<string>(0);
                            if (roomid != null && roomid != "-1")
                            {
                                int count = GetRoomPlayerCount(roomid);
                                return Send(socket, type, count);
                            }
                        }
                        return Send(socket, type, 0);
                }
                return Send(socket, type, msg);
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        public bool Send(ClientSocket socket, SocketMessageType type, params object[] objs)
        {
            // 发送消息给客户端
            try
            {
                if (socket.Send(type, objs) == SocketResult.Success)
                {
                    switch (type)
                    {
                        case SocketMessageType.Logout:
                            RemoveUser();
                            break;
                        case SocketMessageType.Disconnect:
                            RemoveUser();
                            Close();
                            break;
                        case SocketMessageType.Chat:
                            return true;
                    }
                    object obj = objs[0];
                    if (obj.GetType() == typeof(string) && (string)obj != "")
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + ServerHelper.MakeClientName(ClientName, User) + " <- " + obj);
                    return true;
                }
                throw new CanNotSendToClientException();
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        public void Start()
        {
            Task StreamReader = Task.Factory.StartNew(CreateStreamReader);
            Task PeriodicalQuerier = Task.Factory.StartNew(CreatePeriodicalQuerier);
        }

        public void SetTaskAndClientName(Task t, string ClientName)
        {
            _Task = t;
            _ClientName = ClientName;
        }

        private bool DataRequestHandler(ClientSocket socket, SocketObject SocketObject)
        {
            Hashtable RequestData = new();
            Hashtable ResultData = new();
            DataRequestType type = DataRequestType.UnKnown;

            if (SocketObject.Parameters.Length > 0)
            {
                try
                {
                    type = SocketObject.GetParam<DataRequestType>(0);
                    RequestData = SocketObject.GetParam<Hashtable>(1) ?? new();
                    switch (type)
                    {
                        case DataRequestType.UnKnown:
                            break;

                        case DataRequestType.GetFindPasswordVerifyCode:
                            if (RequestData.Count >= 2)
                            {
                                ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(SocketMessageType.DataRequest) + "] " + ServerHelper.MakeClientName(ClientName, User) + " -> ForgetPassword");
                                string username = (string?)(RequestData[ForgetVerifyCodes.Column_Username]) ?? "";
                                string email = (string?)(RequestData[ForgetVerifyCodes.Column_Email]) ?? "";
                                string verifycode = (string?)RequestData[ForgetVerifyCodes.Column_ForgetVerifyCode] ?? "";
                                string msg = ""; // 返回错误信息
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
                                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 验证码已过期");
                                            msg = "此验证码已过期，请重新找回密码。";
                                            SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(username, email));
                                        }
                                        else
                                        {
                                            // 找回密码
                                            if (ForgetVerify.Equals(SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_ForgetVerifyCode]))
                                            {
                                                ServerHelper.WriteLine("[ForgerPassword] UserName: " + username + " Email: " + email);
                                                // TODO. 等更新UpdatePassword
                                                if (true)
                                                {
                                                    //msg = "找回密码！请牢记您的新密码！";
                                                    //SQLHelper.Execute(ForgetVerifyCodes.Delete_ForgetVerifyCode(username, email), out _);
                                                }
                                                //else msg = "服务器无法处理您的注册，注册失败！";
                                            }
                                            else msg = "验证码不正确，请重新输入！";
                                        }
                                    }
                                    else msg = "无法找回您的密码，请稍后再试。";
                                }
                                else
                                {
                                    // 检查验证码是否发送过
                                    SQLHelper.ExecuteDataSet(ForgetVerifyCodes.Select_HasSentForgetVerifyCode(username, email));
                                    if (SQLHelper.Result == SQLResult.Success)
                                    {
                                        DateTime SendTime = (DateTime)SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_SendTime];
                                        string ForgetVerifyCode = (string)SQLHelper.DataSet.Tables[0].Rows[0][ForgetVerifyCodes.Column_ForgetVerifyCode];
                                        if ((DateTime.Now - SendTime).TotalMinutes < 10)
                                        {
                                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + $" 十分钟内已向{email}发送过验证码：{ForgetVerifyCode}");
                                        }
                                        else
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
                                                    string Body = $"亲爱的 {username}， <br/>    您正在找回[{ServerName}]账号的密码，您的验证码是 {ForgetVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.DateOnly)}";
                                                    string[] To = new string[] { email };
                                                    if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                                                    {
                                                        ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + $" 已向{email}发送验证码：{ForgetVerify}");
                                                    }
                                                    else
                                                    {
                                                        ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " 无法发送验证码");
                                                        ServerHelper.WriteLine(MailSender.ErrorMsg);
                                                    }
                                                }
                                                else // 不使用MailSender的情况
                                                {
                                                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + $" 验证码为：{ForgetVerify}，请服务器管理员告知此用户");
                                                }
                                            }
                                            else msg = "无法找回您的密码，请稍后再试。";
                                        }
                                    }
                                    else msg = "无法找回您的密码，请稍后再试。";
                                }
                                ResultData.Add("msg", msg);
                            }
                            else ResultData.Add("msg", "无法找回您的密码，请稍后再试。");
                            break;
                    }
                }
                catch (Exception e)
                {
                    ServerHelper.Error(e);
                    return false;
                }
            }

            return Send(socket, SocketMessageType.DataRequest, type, ResultData);
        }

        private void KickUser()
        {
            if (User.Id != 0)
            {
                string user = User.Username;
                if (Server.ContainsUser(user))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + user + " 重复登录！");
                    ServerModel serverTask = (ServerModel)Server.GetUser(user);
                    serverTask?.Send(serverTask.Socket!, SocketMessageType.ForceLogout, serverTask.CheckLoginKey, "您的账号在别处登录，已强制下线。");
                }
            }
        }

        private bool AddUser()
        {
            if (User.Id != 0 && this != null)
            {
                Server.AddUser(User.Username, this);
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已添加");
                return true;
            }
            return false;
        }

        private bool RemoveUser()
        {
            if (User.Id != 0 && this != null)
            {
                LogoutTime = DateTime.Now.Ticks;
                int TotalMinutes = Convert.ToInt32((new DateTime(LogoutTime) - new DateTime(LoginTime)).TotalMinutes);
                SQLHelper.Execute(UserQuery.Update_GameTime(User.Username, TotalMinutes));
                if (SQLHelper.Result == SQLResult.Success)
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 本次已游玩" + TotalMinutes + "分钟");
                }
                else ServerHelper.WriteLine("OnlinePlayers: 无法更新玩家 " + User.Username + " 的游戏时长");
                if (Server.RemoveUser(User.Username))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已移除");
                    _User = General.UnknownUserInstance;
                    return true;
                }
                else ServerHelper.WriteLine("OnlinePlayers: 移除玩家 " + User.Username + " 失败");
            }
            return false;
        }

        private void GetUsersCount()
        {
            ServerHelper.WriteLine($"目前在线客户端数量: {Config.OnlinePlayersCount}（已登录的玩家数量：{Server.UsersCount}）");
        }

        private void CreateStreamReader()
        {
            Thread.Sleep(100);
            ServerHelper.WriteLine("Creating: StreamReader -> " + ServerHelper.MakeClientName(ClientName, User) + " ...OK");
            while (Running)
            {
                if (Socket != null)
                {
                    if (!Read(Socket))
                    {
                        FailedTimes++;
                        if (FailedTimes >= Config.MaxConnectionFaileds)
                        {
                            RemoveUser();
                            Close();
                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " Error -> Too Many Faileds.");
                            ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " Close -> StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (FailedTimes - 1 >= 0) FailedTimes--;
                }
                else
                {
                    RemoveUser();
                    Close();
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " Error -> Socket is Closed.");
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientName, User) + " Close -> StringStream is Closed.");
                    break;
                }
            }
        }
        
        private void CreatePeriodicalQuerier()
        {
            Thread.Sleep(100);
            ServerHelper.WriteLine("Creating: PeriodicalQuerier -> " + ServerHelper.MakeClientName(ClientName, User) + " ...OK");
            while (Running)
            {
                // 每两小时触发一次SQL服务器的心跳查询，防止SQL服务器掉线
                Thread.Sleep(2 * 1000 * 3600);
                SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(UserName));
            }
        }

        private void Close()
        {
            try
            {
                SQLHelper.Close();
                MailSender?.Dispose();
                Socket?.Close();
                _Socket = null;
                _Running = false;
                Config.OnlinePlayersCount--;
                GetUsersCount();
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        private static int GetRoomPlayerCount(string roomid)
        {
            return Config.RoomList.GetPlayerCount(roomid);
        }

        private static List<User> GetRoomPlayerList(string roomid)
        {
            return Config.RoomList.GetPlayerList(roomid);
        }
    }
}
