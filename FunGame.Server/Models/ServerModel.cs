using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Controller;
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
        public ISocketMessageProcessor? Socket => _Socket;
        public string ClientName => _ClientName;
        public User User => _User;
        public Room Room
        {
            get => _Room;
            set => _Room = value;
        }
        public SQLHelper? SQLHelper => _SQLHelper;
        public MailSender? MailSender => _MailSender;
        public bool IsDebugMode { get; } = false;
        public GameModuleServer? NowGamingServer { get; set; } = null;

        /**
         * Private
         */
        private ISocketMessageProcessor? _Socket = null;
        private bool _Running = false;
        private User _User = General.UnknownUserInstance;
        private Room _Room = General.HallInstance;
        private string _ClientName = "";
        public SQLHelper? _SQLHelper = null;
        public MailSender? _MailSender = null;

        private Guid CheckLoginKey = Guid.Empty;
        private int FailedTimes = 0; // 超过一定次数断开连接
        private string UserName = "";
        private DataSet DsUser = new();
        private readonly Guid Token;
        private readonly ServerSocket Server;
        private readonly DataRequestController DataRequestController;
        private long LoginTime;
        private long LogoutTime;
        private bool IsMatching;

        public ServerModel(ServerSocket server, ClientSocket socket, bool running, bool isDebugMode)
        {
            Server = server;
            _Socket = socket;
            _Running = running;
            Token = socket.Token;
            this.IsDebugMode = isDebugMode;
            if (Config.SQLMode == SQLMode.MySQL) _SQLHelper = new MySQLHelper(this);
            else if (Config.SQLMode == SQLMode.SQLite) _SQLHelper = Config.SQLHelper;
            _MailSender = SmtpHelper.GetMailSender();
            DataRequestController = new(this);
        }

        public bool Read(ISocketMessageProcessor socket)
        {
            // 接收客户端消息
            try
            {
                // 禁止GameModuleServer调用
                if ((IServerModel)this is GameModuleServer) throw new NotSupportedException("请勿在GameModuleServer类中调用此方法");

                SocketObject[] SocketObjects = [];
                // 确保 socket 是 ClientSocket
                if (socket is ClientSocket realSocket)
                {
                    SocketObjects = realSocket.Receive();
                }

                if (SocketObjects.Length == 0)
                {
                    ServerHelper.WriteLine(GetClientName() + " 发送了空信息。");
                    return false;
                }

                foreach (SocketObject SocketObject in SocketObjects)
                {
                    SocketMessageType type = SocketObject.SocketType;
                    Guid token = SocketObject.Token;
                    object[] args = SocketObject.Parameters;
                    string msg = "";

                    // 验证Token
                    if (type != SocketMessageType.HeartBeat && token != Token)
                    {
                        ServerHelper.WriteLine(GetClientName() + " 使用了非法方式传输消息，服务器拒绝回应 -> [" + SocketSet.GetTypeString(type) + "]");
                        return false;
                    }

                    if (type == SocketMessageType.HeartBeat)
                    {
                        return HeartBeat(socket);
                    }

                    if (type == SocketMessageType.EndGame)
                    {
                        NowGamingServer = null;
                        return true;
                    }

                    if (type == SocketMessageType.DataRequest)
                    {
                        return DataRequestHandler(socket, SocketObject);
                    }

                    if (type == SocketMessageType.GamingRequest)
                    {
                        return GamingRequestHandler(socket, SocketObject);
                    }

                    if (type == SocketMessageType.Gaming)
                    {
                        return GamingMessageHandler(socket, SocketObject);
                    }

                    switch (type)
                    {
                        case SocketMessageType.Disconnect:
                            ServerHelper.WriteLine("[" + SocketSet.GetTypeString(SocketMessageType.DataRequest) + "] " + GetClientName() + " -> Disconnect", InvokeMessageType.Core);
                            msg = "你已成功断开与服务器的连接: " + Config.ServerName + "。 ";
                            break;
                    }

                    return Send(socket, type, msg);
                }

                throw new SocketWrongInfoException();
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(GetClientName() + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        public bool DataRequestHandler(ISocketMessageProcessor socket, SocketObject SocketObject)
        {
            if (SQLHelper != null)
            {
                Dictionary<string, object> result = [];
                Guid requestID = Guid.Empty;
                DataRequestType type = DataRequestType.UnKnown;

                if (SocketObject.Parameters.Length > 0)
                {
                    try
                    {
                        type = SocketObject.GetParam<DataRequestType>(0);
                        requestID = SocketObject.GetParam<Guid>(1);
                        Dictionary<string, object> data = SocketObject.GetParam<Dictionary<string, object>>(2) ?? [];

                        result = DataRequestController.GetResultData(type, data);
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                        SQLHelper.Rollback();
                        return Send(socket, SocketMessageType.DataRequest, type, requestID, result);
                    }
                }

                bool sendResult = Send(socket, SocketMessageType.DataRequest, type, requestID, result);
                if (!sendResult)
                {
                    ServerHelper.WriteLine("[ " + User.Username + " ] " + nameof(DataRequestHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + User.Username + " ] " + nameof(DataRequestHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        public bool GamingRequestHandler(ISocketMessageProcessor socket, SocketObject SocketObject)
        {
            if (NowGamingServer != null)
            {
                Dictionary<string, object> result = [];
                Guid requestID = Guid.Empty;
                GamingType type = GamingType.None;

                if (SocketObject.Parameters.Length > 0)
                {
                    try
                    {
                        type = SocketObject.GetParam<GamingType>(0);
                        requestID = SocketObject.GetParam<Guid>(1);
                        Dictionary<string, object> data = SocketObject.GetParam<Dictionary<string, object>>(2) ?? [];

                        result = NowGamingServer.GamingMessageHandler(UserName, type, data);
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                        return Send(socket, SocketMessageType.GamingRequest, type, requestID, result);
                    }
                }

                bool sendResult = Send(socket, SocketMessageType.GamingRequest, type, requestID, result);
                if (!sendResult)
                {
                    ServerHelper.WriteLine("[ " + User.Username + " ] " + nameof(GamingRequestHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + User.Username + " ] " + nameof(GamingRequestHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }
        
        public bool GamingMessageHandler(ISocketMessageProcessor socket, SocketObject SocketObject)
        {
            if (NowGamingServer != null)
            {
                Dictionary<string, object> result = [];
                GamingType type = GamingType.None;

                if (SocketObject.Parameters.Length > 0)
                {
                    try
                    {
                        type = SocketObject.GetParam<GamingType>(0);
                        Dictionary<string, object> data = SocketObject.GetParam<Dictionary<string, object>>(1) ?? [];

                        result = NowGamingServer.GamingMessageHandler(UserName, type, data);
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                        return Send(socket, SocketMessageType.Gaming, type, result);
                    }
                }

                bool sendResult = Send(socket, SocketMessageType.Gaming, type, result);
                if (!sendResult)
                {
                    ServerHelper.WriteLine("[ " + User.Username + " ] "+ nameof(GamingMessageHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + User.Username + " ] " + nameof(GamingMessageHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        public bool Send(ISocketMessageProcessor socket, SocketMessageType type, params object[] objs)
        {
            // 发送消息给客户端
            try
            {
                if (socket.Send(type, objs) == SocketResult.Success)
                {
                    switch (type)
                    {
                        case SocketMessageType.ForceLogout:
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
                        ServerHelper.WriteLine("[" + SocketSet.GetTypeString(type) + "] " + GetClientName() + " <- " + obj, InvokeMessageType.Core);
                    return true;
                }
                throw new CanNotSendToClientException();
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(GetClientName() + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        public void Start()
        {
            if ((IServerModel)this is GameModuleServer) throw new NotSupportedException("请勿在GameModuleServer类中调用此方法"); // 禁止GameModuleServer调用
            TaskUtility.NewTask(CreateStreamReader);
            TaskUtility.NewTask(CreatePeriodicalQuerier);
        }

        public void SetClientName(string ClientName)
        {
            _ClientName = ClientName;
            // 添加客户端到列表中
            Server.AddClient(_ClientName, this);
            Config.OnlinePlayerCount++;
            GetUsersCount();
        }

        public string GetClientName()
        {
            return ServerHelper.MakeClientName(ClientName, User);
        }

        public void PreLogin(DataSet dsuser, string username, Guid checkloginkey)
        {
            DsUser = dsuser;
            UserName = username;
            CheckLoginKey = checkloginkey;
        }

        public void CheckLogin()
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
            SQLHelper?.Execute(UserQuery.Update_CheckLogin(UserName, _Socket?.ClientIP.Split(':')[0] ?? "127.0.0.1"));
        }

        public bool IsLoginKey(Guid key)
        {
            return key == CheckLoginKey;
        }

        public void LogOut()
        {
            // 从玩家列表移除
            RemoveUser();
            GetUsersCount();
            CheckLoginKey = Guid.Empty;
        }

        public void ForceLogOut(string msg, string username = "")
        {
            ServerModel serverTask = (ServerModel)Server.GetUser(username == "" ? UserName : username);
            if (serverTask.Socket != null)
            {
                serverTask.Room = General.HallInstance;
                foreach (Room room in Config.RoomList.Cast<Room>())
                {
                    QuitRoom(room.Roomid, room.RoomMaster.Id == User.Id);
                }
                serverTask.Send(serverTask.Socket, SocketMessageType.ForceLogout, msg);
            }
        }

        public bool QuitRoom(string roomid, bool isMaster)
        {
            bool result;

            Config.RoomList.CancelReady(roomid, User);
            Config.RoomList.QuitRoom(roomid, User);
            Room Room = Config.RoomList[roomid] ?? General.HallInstance;
            // 是否是房主
            if (isMaster)
            {
                List<User> users = Config.RoomList.GetPlayerList(roomid);
                if (users.Count > 0) // 如果此时房间还有人，更新房主
                {
                    User NewMaster = users[0];
                    Room.RoomMaster = NewMaster;
                    SQLHelper?.Execute(RoomQuery.Update_QuitRoom(roomid, User.Id, NewMaster.Id));
                    this.Room = General.HallInstance;
                    UpdateRoomMaster(Room, true);
                    result = true;
                }
                else // 没人了就解散房间
                {
                    Config.RoomList.RemoveRoom(roomid);
                    SQLHelper?.Execute(RoomQuery.Delete_QuitRoom(roomid, User.Id));
                    this.Room = General.HallInstance;
                    ServerHelper.WriteLine("[ " + GetClientName() + " ] 解散了房间 " + roomid);
                    result = true;
                }
            }
            // 不是房主直接退出房间
            else
            {
                this.Room = General.HallInstance;
                UpdateRoomMaster(Room);
                result = true;
            }

            return result;
        }

        public void Kick(string msg, string clientname = "")
        {
            // 将客户端踢出服务器
            ServerModel serverTask = (ServerModel)Server.GetClient(clientname == "" ? ClientName : clientname);
            if (serverTask.Socket != null)
            {
                serverTask.Room = General.HallInstance;
                foreach (Room room in Config.RoomList.Cast<Room>())
                {
                    QuitRoom(room.Roomid, room.RoomMaster.Id == User.Id);
                }
                RemoveUser();
                serverTask.Send(serverTask.Socket, SocketMessageType.Disconnect, msg);
            }
            Close();
        }

        public void Chat(string msg)
        {
            ServerHelper.WriteLine(msg);
            foreach (ServerModel Client in Server.ClientList.Cast<ServerModel>())
            {
                if (Room.Roomid == Client.Room.Roomid)
                {
                    if (Client != null && User.Id != 0)
                    {
                        Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + msg);
                    }
                }
            }
        }

        public void SendSystemMessage(ShowMessageType showtype, string msg, string title, int autoclose, params string[] usernames)
        {
            foreach (ServerModel serverTask in Server.UserList.Cast<ServerModel>().Where(model => usernames.Length > 0 && usernames.Contains(model.UserName)))
            {
                if (serverTask != null && serverTask.Socket != null)
                {
                    serverTask.Send(serverTask.Socket, SocketMessageType.System, showtype, msg, title, autoclose);
                }
            }
        }

        public void StartGame(string roomid, List<User> users, params string[] usernames)
        {
            Room room = General.HallInstance;
            if (roomid != "-1")
            {
                room = Config.RoomList[roomid];
            }
            if (room.Roomid == "-1") return;
            // 启动服务器
            TaskUtility.NewTask(() =>
            {
                if (Config.GameModuleLoader != null && Config.GameModuleLoader.ModuleServers.ContainsKey(room.GameModule))
                {
                    NowGamingServer = Config.GameModuleLoader.GetServerMode(room.GameModule);
                    Dictionary<string, IServerModel> all = Server.UserList.Cast<IServerModel>().ToDictionary(k => k.User.Username, v => v);
                    // 给其他玩家赋值模组服务器
                    foreach (IServerModel model in all.Values.Where(s => s.User.Username != User.Username))
                    {
                        model.NowGamingServer = NowGamingServer;
                    }
                    if (NowGamingServer.StartServer(room.GameModule, room, users, this, all))
                    {
                        foreach (ServerModel serverTask in Server.UserList.Cast<ServerModel>().Where(model => usernames.Contains(model.User.Username)))
                        {
                            if (serverTask != null && serverTask.Socket != null)
                            {
                                Config.RoomList.CancelReady(roomid, serverTask.User);
                                serverTask.Send(serverTask.Socket, SocketMessageType.StartGame, room, users);
                            }
                        }
                    }
                }
            });
        }

        public void IntoRoom(string roomid)
        {
            Room = Config.RoomList[roomid];
            foreach (ServerModel Client in Server.ClientList.Cast<ServerModel>().Where(c => c != null && c.Socket != null && roomid == c.Room.Roomid))
            {
                if (User.Id != 0)
                {
                    Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 进入了房间。");
                }
            }
        }

        public void UpdateRoomMaster(Room Room, bool bolIsUpdateRoomMaster = false)
        {
            foreach (ServerModel Client in Server.ClientList.Cast<ServerModel>().Where(c => c != null && c.Socket != null && Room.Roomid == c.Room.Roomid))
            {
                if (User.Id != 0)
                {
                    Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 离开了房间。");
                    if (bolIsUpdateRoomMaster && Room.RoomMaster?.Id != 0 && Room.Roomid != "-1")
                    {
                        Client.Send(Client.Socket!, SocketMessageType.UpdateRoomMaster, Room);
                    }
                }
            }
        }

        public bool HeartBeat(ISocketMessageProcessor socket)
        {
            bool result = Send(socket, SocketMessageType.HeartBeat, "");
            if (!result)
            {
                ServerHelper.WriteLine("[ " + User.Username + " ] " + nameof(HeartBeat) + ": " + result, InvokeMessageType.Error);
            }
            return result;
        }

        public void StartMatching(RoomType type, User user)
        {
            IsMatching = true;
            ServerHelper.WriteLine(GetClientName() + " 开始匹配。类型：" + RoomSet.GetTypeString(type));
            TaskUtility.NewTask(async () =>
            {
                if (IsMatching)
                {
                    Room room = await MatchingRoom(type, user);
                    if (IsMatching && Socket != null)
                    {
                        Send(Socket, SocketMessageType.MatchRoom, room);
                    }
                    IsMatching = false;
                }
            }).OnError(e =>
            {
                ServerHelper.Error(e);
                IsMatching = false;
            });
        }

        public void StopMatching()
        {
            if (IsMatching)
            {
                ServerHelper.WriteLine(GetClientName() + " 取消了匹配。");
                IsMatching = false;
            }
        }

        private async Task<Room> MatchingRoom(RoomType roomtype, User user)
        {
            int i = 1;
            int time = 0;
            while (IsMatching)
            {
                // 先列出符合条件的房间
                List<Room> targets = Config.RoomList.ListRoom.Where(r => r.RoomType == roomtype).ToList();

                // 匹配Elo
                foreach (Room room in targets)
                {
                    // 计算房间平均Elo
                    //List<User> players = Config.RoomList.GetPlayerList(room.Roomid);
                    //if (players.Count > 0)
                    //{
                    //    decimal avgelo = players.Sum(u => u.Statistics.EloStats.ContainsKey(0) ? u.Statistics.EloStats[0] : 0M) / players.Count;
                    //    decimal userelo = user.Statistics.EloStats.ContainsKey(0) ? user.Statistics.EloStats[0] : 0M;
                    //    if (userelo >= avgelo - (300 * i) && userelo <= avgelo + (300 * i))
                    //    {
                    //        return room;
                    //    }
                    //}
                }

                if (!IsMatching) break;

                // 等待10秒
                await Task.Delay(10 * 1000);
                time += 10 * 1000;
                if (time >= 50 * 1000)
                {
                    // 50秒后不再匹配Elo，直接返回第一个房间
                    if (targets.Count > 0)
                    {
                        return targets[0];
                    }
                    break;
                }
                i++;
            }

            return General.HallInstance;
        }

        private void KickUser()
        {
            if (User.Id != 0)
            {
                string user = User.Username;
                if (Server.ContainsUser(user))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + user + " 重复登录！");
                    ForceLogOut("您的账号在别处登录，已强制下线。");
                }
            }
        }

        private bool AddUser()
        {
            if (User.Id != 0 && this != null)
            {
                Server.AddUser(User.Username, this);
                UserName = User.Username;
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
                SQLHelper?.Execute(UserQuery.Update_GameTime(User.Username, TotalMinutes));
                if (SQLHelper != null && SQLHelper.Result == SQLResult.Success)
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
            ServerHelper.WriteLine($"目前在线客户端数量: {Server.ClientCount}（已登录的玩家数量：{Server.UserCount}）");
        }

        private async Task CreateStreamReader()
        {
            await Task.Delay(20);
            ServerHelper.WriteLine("Creating: StreamReader -> " + GetClientName() + " ...OK");
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
                            ServerHelper.WriteLine(GetClientName() + " Error -> Too Many Faileds.");
                            ServerHelper.WriteLine(GetClientName() + " Close -> StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (FailedTimes - 1 >= 0) FailedTimes--;
                }
                else
                {
                    RemoveUser();
                    Close();
                    ServerHelper.WriteLine(GetClientName() + " Error -> Socket is Closed.");
                    ServerHelper.WriteLine(GetClientName() + " Close -> StringStream is Closed.");
                    break;
                }
            }
        }

        private async Task CreatePeriodicalQuerier()
        {
            await Task.Delay(20);
            ServerHelper.WriteLine("Creating: PeriodicalQuerier -> " + GetClientName() + " ...OK");
            while (Running)
            {
                // 每两小时触发一次SQL服务器的心跳查询，防止SQL服务器掉线
                try
                {
                    await Task.Delay(2 * 1000 * 3600);
                    SQLHelper?.ExecuteDataSet(UserQuery.Select_IsExistUsername(UserName));
                }
                catch (Exception e)
                {
                    ServerHelper.Error(e);
                    RemoveUser();
                    Close();
                    ServerHelper.WriteLine(GetClientName() + " Error -> Socket is Closed.");
                    ServerHelper.WriteLine(GetClientName() + " Close -> StringStream is Closed.");
                }
            }
        }

        private void Close()
        {
            try
            {
                SQLHelper?.Close();
                _SQLHelper = null;
                MailSender?.Dispose();
                _MailSender = null;
                Socket?.Close();
                _Socket = null;
                _Running = false;
                Server.RemoveClient(ClientName);
                Config.OnlinePlayerCount--;
                GetUsersCount();
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }
    }
}
