using System.Data;
using System.Net.Sockets;
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
    public abstract class BaseServerModel<T> : IServerModel where T : ISocketMessageProcessor
    {
        /**
         * Public
         */
        public bool Running => _Running;
        public abstract ISocketMessageProcessor Socket { get; }
        public abstract ISocketListener<T> Listener { get; }
        public Guid Token => Socket?.Token ?? Guid.Empty;
        public string ClientName => _ClientName;
        public User User => _User;
        public Room InRoom { get; set; } = General.HallInstance;
        public SQLHelper? SQLHelper => _SQLHelper;
        public MailSender? MailSender => _MailSender;
        public abstract DataRequestController<T> DataRequestController { get; }
        public abstract bool IsDebugMode { get; }
        public GameModuleServer? NowGamingServer { get; set; } = null;

        /**
         * protected
         */
        protected bool _Running = false;
        protected User _User = General.UnknownUserInstance;
        protected string _ClientName = "";
        protected SQLHelper? _SQLHelper = null;
        protected MailSender? _MailSender = null;

        protected Guid CheckLoginKey = Guid.Empty;
        protected int FailedTimes = 0; // 超过一定次数断开连接
        protected string Username = "";
        protected DataSet DsUser = new();
        protected long LoginTime;
        protected long LogoutTime;
        protected bool IsMatching;

        public bool SocketMessageHandler(ISocketMessageProcessor socket, SocketObject obj)
        {
            // 读取收到的消息
            SocketMessageType type = obj.SocketType;
            Guid token = obj.Token;
            string msg = "";

            // 验证Token
            if (type != SocketMessageType.HeartBeat && token != socket.Token)
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
                return DataRequestHandler(socket, obj);
            }

            if (type == SocketMessageType.GamingRequest)
            {
                return GamingRequestHandler(socket, obj);
            }

            if (type == SocketMessageType.Gaming)
            {
                return GamingMessageHandler(socket, obj);
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

        public bool HeartBeat(ISocketMessageProcessor socket)
        {
            bool result = Send(socket, SocketMessageType.HeartBeat, "");
            if (!result)
            {
                ServerHelper.WriteLine("[ " + Username + " ] " + nameof(HeartBeat) + ": " + result, InvokeMessageType.Error);
            }
            return result;
        }

        protected bool DataRequestHandler(ISocketMessageProcessor socket, SocketObject obj)
        {
            if (SQLHelper != null)
            {
                Dictionary<string, object> result = [];
                Guid requestID = Guid.Empty;
                DataRequestType type = DataRequestType.UnKnown;

                if (obj.Parameters.Length > 0)
                {
                    try
                    {
                        type = obj.GetParam<DataRequestType>(0);
                        requestID = obj.GetParam<Guid>(1);
                        Dictionary<string, object> data = obj.GetParam<Dictionary<string, object>>(2) ?? [];

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
                    ServerHelper.WriteLine("[ " + Username + " ] " + nameof(DataRequestHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + Username + " ] " + nameof(DataRequestHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        protected bool GamingRequestHandler(ISocketMessageProcessor socket, SocketObject obj)
        {
            if (NowGamingServer != null)
            {
                Dictionary<string, object> result = [];
                Guid requestID = Guid.Empty;
                GamingType type = GamingType.None;

                if (obj.Parameters.Length > 0)
                {
                    try
                    {
                        type = obj.GetParam<GamingType>(0);
                        requestID = obj.GetParam<Guid>(1);
                        Dictionary<string, object> data = obj.GetParam<Dictionary<string, object>>(2) ?? [];

                        result = NowGamingServer.GamingMessageHandler(Username, type, data);
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
                    ServerHelper.WriteLine("[ " + Username + " ] " + nameof(GamingRequestHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + Username + " ] " + nameof(GamingRequestHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        protected bool GamingMessageHandler(ISocketMessageProcessor socket, SocketObject obj)
        {
            if (NowGamingServer != null)
            {
                Dictionary<string, object> result = [];
                GamingType type = GamingType.None;

                if (obj.Parameters.Length > 0)
                {
                    try
                    {
                        type = obj.GetParam<GamingType>(0);
                        Dictionary<string, object> data = obj.GetParam<Dictionary<string, object>>(1) ?? [];

                        result = NowGamingServer.GamingMessageHandler(Username, type, data);
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
                    ServerHelper.WriteLine("[ " + Username + " ] " + nameof(GamingMessageHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + Username + " ] " + nameof(GamingMessageHandler) + ": " + false, InvokeMessageType.Error);
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
            Listener.ClientList.Add(_ClientName, this);
            Config.OnlinePlayerCount++;
            GetUsersCount();
        }

        public string GetClientName()
        {
            return ServerHelper.MakeClientName(ClientName, User);
        }

        public void SendSystemMessage(ShowMessageType showtype, string msg, string title, int autoclose, params string[] usernames)
        {
            foreach (IServerModel serverTask in Listener.UserList.Where(model => usernames.Length > 0 && usernames.Contains(model.User.Username)))
            {
                if (serverTask != null && serverTask.Socket != null)
                {
                    serverTask.Send(serverTask.Socket, SocketMessageType.System, showtype, msg, title, autoclose);
                }
            }
        }

        public void PreLogin(DataSet dsuser, string username, Guid checkloginkey)
        {
            DsUser = dsuser;
            Username = username;
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
            SQLHelper?.Execute(UserQuery.Update_CheckLogin(Username, Socket?.ClientIP.Split(':')[0] ?? "127.0.0.1"));
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
            IServerModel serverTask = Listener.UserList[username == "" ? Username : username];
            if (serverTask.Socket != null)
            {
                serverTask.InRoom = General.HallInstance;
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
                    this.InRoom = General.HallInstance;
                    UpdateRoomMaster(Room, true);
                    result = true;
                }
                else // 没人了就解散房间
                {
                    Config.RoomList.RemoveRoom(roomid);
                    SQLHelper?.Execute(RoomQuery.Delete_QuitRoom(roomid, User.Id));
                    this.InRoom = General.HallInstance;
                    ServerHelper.WriteLine("[ " + GetClientName() + " ] 解散了房间 " + roomid);
                    result = true;
                }
            }
            // 不是房主直接退出房间
            else
            {
                this.InRoom = General.HallInstance;
                UpdateRoomMaster(Room);
                result = true;
            }

            return result;
        }

        public void Kick(string msg, string clientname = "")
        {
            // 将客户端踢出服务器
            IServerModel serverTask = Listener.ClientList[clientname == "" ? ClientName : clientname];
            if (serverTask.Socket != null)
            {
                serverTask.InRoom = General.HallInstance;
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
            foreach (IServerModel Client in Listener.ClientList)
            {
                if (InRoom.Roomid == Client.InRoom?.Roomid)
                {
                    if (Client != null && User.Id != 0)
                    {
                        Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + msg);
                    }
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
                    Dictionary<string, IServerModel> all = Listener.UserList.Cast<IServerModel>().ToDictionary(k => k.User.Username, v => v);
                    // 给其他玩家赋值模组服务器
                    foreach (IServerModel model in all.Values.Where(s => s.User.Username != User.Username))
                    {
                        model.NowGamingServer = NowGamingServer;
                    }
                    if (NowGamingServer.StartServer(room.GameModule, room, users, this, all))
                    {
                        foreach (IServerModel serverTask in Listener.UserList.Where(model => usernames.Contains(model.User.Username)))
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
            InRoom = Config.RoomList[roomid];
            foreach (IServerModel Client in Listener.ClientList.Where(c => c != null && c.Socket != null && roomid == c.InRoom?.Roomid))
            {
                if (User.Id != 0)
                {
                    Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 进入了房间。");
                }
            }
        }

        public void UpdateRoomMaster(Room Room, bool bolIsUpdateRoomMaster = false)
        {
            foreach (IServerModel Client in Listener.ClientList.Where(c => c != null && c.Socket != null && Room.Roomid == c.InRoom?.Roomid))
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

        protected async Task<Room> MatchingRoom(RoomType roomtype, User user)
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

        protected void KickUser()
        {
            if (User.Id != 0)
            {
                string user = User.Username;
                if (Listener.UserList.ContainsKey(user))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + user + " 重复登录！");
                    ForceLogOut("您的账号在别处登录，已强制下线。");
                }
            }
        }

        protected bool AddUser()
        {
            if (User.Id != 0 && this != null)
            {
                Listener.UserList.Add(User.Username, this);
                Username = User.Username;
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已添加");
                return true;
            }
            return false;
        }

        protected bool RemoveUser()
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
                if (Listener.UserList.Remove(User.Username))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已移除");
                    _User = General.UnknownUserInstance;
                    return true;
                }
                else ServerHelper.WriteLine("OnlinePlayers: 移除玩家 " + User.Username + " 失败");
            }
            return false;
        }

        protected void GetUsersCount()
        {
            ServerHelper.WriteLine($"目前在线客户端数量: {Listener.ClientList.Count}（已登录的玩家数量：{Listener.UserList.Count}）");
        }

        protected virtual async Task CreateStreamReader()
        {
            await Task.Delay(100);
        }

        protected async Task CreatePeriodicalQuerier()
        {
            await Task.Delay(20);
            ServerHelper.WriteLine("Creating: PeriodicalQuerier -> " + GetClientName() + " ...OK");
            while (Running)
            {
                // 每两小时触发一次SQL服务器的心跳查询，防止SQL服务器掉线
                try
                {
                    await Task.Delay(2 * 1000 * 3600);
                    SQLHelper?.ExecuteDataSet(UserQuery.Select_IsExistUsername(Username));
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

        protected void Close()
        {
            try
            {
                SQLHelper?.Close();
                _SQLHelper = null;
                MailSender?.Dispose();
                _MailSender = null;
                Socket.Close();
                _Running = false;
                Listener.ClientList.Remove(ClientName);
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
