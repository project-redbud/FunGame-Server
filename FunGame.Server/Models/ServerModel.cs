using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public class ServerModel<T> : IServerModel where T : ISocketMessageProcessor
    {
        /**
         * Public
         */
        public bool Running => _running;
        public ISocketMessageProcessor Socket { get; }
        public ISocketListener<T> Listener { get; }
        public DataRequestController<T> DataRequestController { get; }
        public Guid Token => Socket?.Token ?? Guid.Empty;
        public string ClientName => _clientName;
        public User User { get; set; } = General.UnknownUserInstance;
        public Room InRoom { get; set; } = General.HallInstance;
        public SQLHelper? SQLHelper => _sqlHelper;
        public MailSender? MailSender => _mailer;
        public bool IsDebugMode { get; }
        public GameModuleServer? NowGamingServer { get; set; } = null;

        /**
         * protected
         */
        protected bool _running = true;
        protected int _failedTimes = 0; // 超过一定次数断开连接
        protected string _clientName = "";
        protected SQLHelper? _sqlHelper = null;
        protected MailSender? _mailer = null;
        protected string _username = "";
        protected long _loginTime = 0;
        protected long _logoutTime = 0;

        public ServerModel(ISocketListener<T> server, ISocketMessageProcessor socket, bool isDebugMode)
        {
            Listener = server;
            Socket = socket;
            DataRequestController = new(this);
            IsDebugMode = isDebugMode;
            _sqlHelper = Factory.OpenFactory.GetSQLHelper();
            _mailer = SmtpHelper.GetMailSender();
        }

        public virtual async Task<bool> SocketMessageHandler(ISocketMessageProcessor socket, SocketObject obj)
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
                return await HeartBeat();
            }

            if (type == SocketMessageType.EndGame)
            {
                NowGamingServer = null;
                return true;
            }

            if (type == SocketMessageType.DataRequest)
            {
                return await DataRequestHandler(obj);
            }

            if (type == SocketMessageType.GamingRequest)
            {
                return await GamingRequestHandler(obj);
            }

            if (type == SocketMessageType.Gaming)
            {
                return await GamingMessageHandler(obj);
            }

            switch (type)
            {
                case SocketMessageType.Disconnect:
                    ServerHelper.WriteLine("[" + SocketSet.GetTypeString(SocketMessageType.DataRequest) + "] " + GetClientName() + " -> Disconnect", InvokeMessageType.Core);
                    msg = "你已成功断开与服务器的连接: " + Config.ServerName + "。 ";
                    break;
            }

            return await Send(type, msg);
        }

        public async Task<bool> HeartBeat()
        {
            bool result = await Send(SocketMessageType.HeartBeat);
            if (!result)
            {
                ServerHelper.WriteLine("[ " + _username + " ] " + nameof(HeartBeat) + ": " + result, InvokeMessageType.Error);
            }
            return result;
        }

        protected async Task<bool> DataRequestHandler(SocketObject obj)
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

                        result = await DataRequestController.GetResultData(type, data);
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                        SQLHelper.Rollback();
                        return await Send(SocketMessageType.DataRequest, type, requestID, result);
                    }
                }

                bool sendResult = await Send(SocketMessageType.DataRequest, type, requestID, result);
                if (!sendResult)
                {
                    ServerHelper.WriteLine("[ " + _username + " ] " + nameof(DataRequestHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + _username + " ] " + nameof(DataRequestHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        protected async Task<bool> GamingRequestHandler(SocketObject obj)
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

                        result = await NowGamingServer.GamingMessageHandler(_username, type, data);
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                        return await Send(SocketMessageType.GamingRequest, type, requestID, result);
                    }
                }

                bool sendResult = await Send(SocketMessageType.GamingRequest, type, requestID, result);
                if (!sendResult)
                {
                    ServerHelper.WriteLine("[ " + _username + " ] " + nameof(GamingRequestHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + _username + " ] " + nameof(GamingRequestHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        protected async Task<bool> GamingMessageHandler(SocketObject obj)
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

                        result = await NowGamingServer.GamingMessageHandler(_username, type, data);
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                        return await Send(SocketMessageType.Gaming, type, result);
                    }
                }

                bool sendResult = await Send(SocketMessageType.Gaming, type, result);
                if (!sendResult)
                {
                    ServerHelper.WriteLine("[ " + _username + " ] " + nameof(GamingMessageHandler) + ": " + sendResult, InvokeMessageType.Error);
                }
                return sendResult;
            }

            ServerHelper.WriteLine("[ " + _username + " ] " + nameof(GamingMessageHandler) + ": " + false, InvokeMessageType.Error);
            return false;
        }

        public virtual async Task<bool> Send(SocketMessageType type, params object[] objs)
        {
            // 发送消息给客户端
            try
            {
                if (await Socket.SendAsync(type, objs) == SocketResult.Success)
                {
                    switch (type)
                    {
                        case SocketMessageType.ForceLogout:
                            RemoveUser();
                            break;
                        case SocketMessageType.Disconnect:
                            RemoveUser();
                            await Close();
                            break;
                        case SocketMessageType.Chat:
                            return true;
                    }
                    if (objs.Length > 0 && objs[0] is string str && str != "")
                    {
                        ServerHelper.WriteLine("[" + SocketSet.GetTypeString(type) + "] " + GetClientName() + " <- " + str, InvokeMessageType.Core);
                    }
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

        public async Task SendClients(IEnumerable<IServerModel> clients, SocketMessageType type, params object[] objs)
        {
            // 发送消息给多个客户端
            try
            {
                foreach (IServerModel client in clients)
                {
                    if (client.Socket != null)
                    {
                        await client.Socket.SendAsync(type, objs);
                    }
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        public async Task Start()
        {
            await CreateStreamReader();
        }

        public void SetClientName(string ClientName)
        {
            _clientName = ClientName;
            // 添加客户端到列表中
            Listener.ClientList.Add(_clientName, this);
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
                    serverTask.Send(SocketMessageType.System, showtype, msg, title, autoclose);
                }
            }
        }

        public async Task<bool> QuitRoom(string roomid, bool isMaster)
        {
            bool result;

            Config.RoomList.CancelReady(roomid, User);
            Config.RoomList.QuitRoom(roomid, User);
            Room Room = Config.RoomList[roomid] ?? General.HallInstance;
            // 是否是房主
            if (isMaster)
            {
                List<User> users = [.. Config.RoomList[roomid].UserAndIsReady.Keys];
                if (users.Count > 0) // 如果此时房间还有人，更新房主
                {
                    User NewMaster = users[0];
                    Room.RoomMaster = NewMaster;
                    SQLHelper?.Execute(RoomQuery.Update_QuitRoom(SQLHelper, roomid, User.Id, NewMaster.Id));
                    this.InRoom = General.HallInstance;
                    await UpdateRoomMaster(Room, true);
                    result = true;
                }
                else // 没人了就解散房间
                {
                    Config.RoomList.RemoveRoom(roomid);
                    SQLHelper?.Execute(RoomQuery.Delete_QuitRoom(SQLHelper, roomid, User.Id));
                    this.InRoom = General.HallInstance;
                    ServerHelper.WriteLine("[ " + GetClientName() + " ] 解散了房间 " + roomid);
                    result = true;
                }
            }
            // 不是房主直接退出房间
            else
            {
                this.InRoom = General.HallInstance;
                await UpdateRoomMaster(Room);
                result = true;
            }

            return result;
        }

        public async Task UpdateRoomMaster(Room room, bool isUpdateRoomMaster = false)
        {
            foreach (IServerModel Client in Listener.ClientList.Where(c => c != null && c.User.Id != 0 && room.Roomid == c.InRoom?.Roomid))
            {
                await Client.Send(SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 离开了房间。");
                if (isUpdateRoomMaster && room.RoomMaster?.Id != 0 && room.Roomid != "-1")
                {
                    await Client.Send(SocketMessageType.UpdateRoomMaster, room);
                }
            }
        }

        public async Task Kick(string msg)
        {
            await QuitRoom(InRoom.Roomid, InRoom.RoomMaster.Id == User.Id);
            RemoveUser();
            InRoom = General.HallInstance;
            await Send(SocketMessageType.Disconnect, msg);
            await Close();
        }

        public async Task ForceLogOut(string msg)
        {
            await QuitRoom(InRoom.Roomid, InRoom.RoomMaster.Id == User.Id);
            InRoom = General.HallInstance;
            await Send(SocketMessageType.ForceLogout, msg);
        }

        public async Task ForceLogOutDuplicateLogonUser()
        {
            if (User.Id != 0)
            {
                string user = User.Username;
                if (Listener.UserList.ContainsKey(user))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + user + " 重复登录！");
                    await ForceLogOut("您的账号在别处登录，已强制下线。");
                }
            }
        }

        public bool AddUser()
        {
            if (User.Id != 0 && this != null)
            {
                Listener.UserList.Add(User.Username, this);
                _username = User.Username;
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已添加");
                // 更新最后登录时间、IP地址
                _loginTime = DateTime.Now.Ticks;
                SQLHelper?.Execute(UserQuery.Update_CheckLogin(SQLHelper, _username, Socket?.ClientIP.Split(':')[0] ?? "127.0.0.1"));
                return true;
            }
            return false;
        }

        public bool RemoveUser()
        {
            if (User.Id != 0 && this != null)
            {
                _logoutTime = DateTime.Now.Ticks;
                int TotalMinutes = Convert.ToInt32((new DateTime(_logoutTime) - new DateTime(_loginTime)).TotalMinutes);
                SQLHelper?.Execute(UserQuery.Update_GameTime(SQLHelper, User.Username, TotalMinutes));
                if (SQLHelper != null && SQLHelper.Result == SQLResult.Success)
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 本次已游玩" + TotalMinutes + "分钟");
                }
                else ServerHelper.WriteLine("OnlinePlayers: 无法更新玩家 " + User.Username + " 的游戏时长");
                if (Listener.UserList.Remove(User.Username))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已移除");
                    User = General.UnknownUserInstance;
                    return true;
                }
                else ServerHelper.WriteLine("OnlinePlayers: 移除玩家 " + User.Username + " 失败");
            }
            return false;
        }

        public void GetUsersCount()
        {
            ServerHelper.WriteLine($"{Listener.Name} 的目前在线客户端数量: {Listener.ClientList.Count}（已登录的玩家数量：{Listener.UserList.Count}）");
        }

        protected virtual async Task<bool> Read(ISocketMessageProcessor socket)
        {
            // 接收客户端消息
            try
            {
                SocketObject[] objs = await socket.ReceiveAsync();

                if (objs.Length == 0)
                {
                    ServerHelper.WriteLine(GetClientName() + " 发送了空信息。");
                    return false;
                }

                foreach (SocketObject obj in objs)
                {
                    await SocketMessageHandler(socket, obj);
                }

                return true;
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(GetClientName() + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        protected async Task CreateStreamReader()
        {
            CancellationTokenSource cts = new();
            Task sqlPolling = Task.Run(async () =>
            {
                await Task.Delay(30);
                ServerHelper.WriteLine("Creating: SQLPolling -> " + GetClientName() + " ...OK");
                while (Running)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        break;
                    }
                    // 每两小时触发一次SQL服务器的心跳查询，防止SQL服务器掉线
                    try
                    {
                        await Task.Delay(2 * 1000 * 3600);
                        SQLHelper?.ExecuteDataSet(ServerLoginLogs.Select_GetLastLoginTime());
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                    }
                }
            }, cts.Token);
            await Task.Delay(20);
            ServerHelper.WriteLine("Creating: StreamReader -> " + GetClientName() + " ...OK");
            while (Running)
            {
                if (Socket != null)
                {
                    if (!await Read(Socket))
                    {
                        _failedTimes++;
                        if (_failedTimes >= Config.MaxConnectionFaileds)
                        {
                            RemoveUser();
                            await Close();
                            ServerHelper.WriteLine(GetClientName() + " Error -> Too Many Faileds.");
                            ServerHelper.WriteLine(GetClientName() + " Close -> StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (_failedTimes - 1 >= 0) _failedTimes--;
                }
                else
                {
                    RemoveUser();
                    await Close();
                    ServerHelper.WriteLine(GetClientName() + " Error -> Socket is Closed.");
                    ServerHelper.WriteLine(GetClientName() + " Close -> StringStream is Closed.");
                    break;
                }
            }
            cts.Cancel();
            await sqlPolling;
            cts.Dispose();
        }

        protected async Task Close()
        {
            try
            {
                await Socket.CloseAsync();
                _running = false;
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
