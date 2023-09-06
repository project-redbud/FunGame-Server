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
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public partial class ServerModel : IServerModel
    {
        /**
         * Public
         */
        public bool Running => _Running;
        public ClientSocket? Socket => _Socket;
        public Task? Task => _Task;
        public string ClientName => _ClientName;
        public User User => _User;
        public Room Room
        {
            get => _Room;
            set => _Room = value;
        }
        public MySQLHelper SQLHelper { get; }
        public MailSender? MailSender { get; }

        /**
         * Private
         */
        private ClientSocket? _Socket = null;
        private bool _Running = false;
        private User _User = General.UnknownUserInstance;
        private Room _Room = General.HallInstance;
        private Task? _Task = null;
        private string _ClientName = "";

        private Guid CheckLoginKey = Guid.Empty;
        private string RegVerify = "";
        private int FailedTimes = 0; // 超过一定次数断开连接
        private string UserName = "";
        private DataSet DsUser = new();
        private readonly Guid Token;
        private readonly ServerSocket Server;
        private readonly ServerController ServerController;
        private readonly DataRequestController DataRequestController;
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
            ServerController = new(this);
            DataRequestController = new(this);
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
                    ServerHelper.WriteLine(GetClientName() + " 使用了非法方式传输消息，服务器拒绝回应 -> [" + ServerSocket.GetTypeString(type) + "] ");
                    return false;
                }

                if (type == SocketMessageType.DataRequest)
                {
                    return DataRequestHandler(socket, SocketObject);
                }

                if (type == SocketMessageType.HeartBeat)
                {
                    return HeartBeat(socket);
                }

                switch (type)
                {
                    //case SocketMessageType.RunTime_Login:
                    //    CheckLoginKey = Guid.Empty;
                    //    if (args != null)
                    //    {
                    //        string? username = "", password = "", autokey = "";
                    //        if (args.Length > 0) username = SocketObject.GetParam<string>(0);
                    //        if (args.Length > 1) password = SocketObject.GetParam<string>(1);
                    //        if (args.Length > 2) autokey = SocketObject.GetParam<string>(2);
                    //        if (username != null && password != null)
                    //        {
                    //            ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] UserName: " + username);
                    //            SQLHelper.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(username, password));
                    //            if (SQLHelper.Result == SQLResult.Success)
                    //            {
                    //                DsUser = SQLHelper.DataSet;
                    //                if (autokey != null && autokey.Trim() != "")
                    //                {
                    //                    SQLHelper.ExecuteDataSet(UserQuery.Select_CheckAutoKey(username, autokey));
                    //                    if (SQLHelper.Result == SQLResult.Success)
                    //                    {
                    //                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] AutoKey: 已确认");
                    //                    }
                    //                    else
                    //                    {
                    //                        msg = "AutoKey不正确，拒绝自动登录！";
                    //                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + msg);
                    //                        return Send(socket, type, CheckLoginKey, msg);
                    //                    }
                    //                }
                    //                UserName = username;
                    //                CheckLoginKey = Guid.NewGuid();
                    //                return Send(socket, type, CheckLoginKey);
                    //            }
                    //            msg = "用户名或密码不正确。";
                    //            ServerHelper.WriteLine(msg);
                    //        }
                    //    }
                    //    return Send(socket, type, CheckLoginKey, msg);

                    //case SocketMessageType.RunTime_CheckLogin:
                    //    if (args != null && args.Length > 0)
                    //    {
                    //        Guid checkloginkey = SocketObject.GetParam<Guid>(0);
                    //        if (CheckLoginKey.Equals(checkloginkey))
                    //        {
                    //            // 创建User对象
                    //            _User = Factory.GetUser(DsUser);
                    //            // 检查有没有重复登录的情况
                    //            KickUser();
                    //            // 添加至玩家列表
                    //            AddUser();
                    //            GetUsersCount();
                    //            // CheckLogin
                    //            LoginTime = DateTime.Now.Ticks;
                    //            SQLHelper.Execute(UserQuery.Update_CheckLogin(UserName, socket.ClientIP.Split(':')[0]));
                    //            return Send(socket, type, _User);
                    //        }
                    //        ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登录。");
                    //    }
                    //    return Send(socket, type, CheckLoginKey.ToString());
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

        public bool DataRequestHandler(ClientSocket socket, SocketObject SocketObject)
        {
            Hashtable result = new();
            DataRequestType type = DataRequestType.UnKnown;

            if (SocketObject.Parameters.Length > 0)
            {
                try
                {
                    type = SocketObject.GetParam<DataRequestType>(0);
                    Hashtable data = SocketObject.GetParam<Hashtable>(1) ?? new();

                    SQLHelper.NewTransaction();
                    result = DataRequestController.GetResultData(type, data);
                    SQLHelper.Commit();
                }
                catch (Exception e)
                {
                    ServerHelper.Error(e);
                    SQLHelper.Rollback();
                    return Send(socket, SocketMessageType.DataRequest, type, result);
                }
            }

            return Send(socket, SocketMessageType.DataRequest, type, result);
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
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + GetClientName() + " <- " + obj);
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
            Task StreamReader = Task.Factory.StartNew(CreateStreamReader);
            Task PeriodicalQuerier = Task.Factory.StartNew(CreatePeriodicalQuerier);
        }

        public void SetTaskAndClientName(Task t, string ClientName)
        {
            _Task = t;
            _ClientName = ClientName;
        }

        public string GetClientName()
        {
            return ServerHelper.MakeClientName(ClientName, User);
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

        public void Chat(string msg)
        {
            ServerHelper.WriteLine(msg);
            foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
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

        public void IntoRoom(string roomid)
        {
            foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
            {
                if (roomid == Client.Room.Roomid)
                {
                    if (Client != null && User.Id != 0)
                    {
                        Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 进入了房间。");
                    }
                }
            }
        }

        public void UpdateRoomMaster(Room Room)
        {
            foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
            {
                if (Room.Roomid == Client.Room.Roomid)
                {
                    if (Client != null && User.Id != 0)
                    {
                        Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, DateTimeUtility.GetNowShortTime() + " [ " + User.Username + " ] 离开了房间。");
                        if (Room.RoomMaster?.Id != 0 && Room.Roomid != "-1")
                        {
                            Client.Send(Client.Socket!, SocketMessageType.UpdateRoomMaster, Room);
                        }
                    }
                }
            }
        }

        public bool HeartBeat(ClientSocket socket)
        {
            return Send(socket, SocketMessageType.HeartBeat, "");
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
        
        private void CreatePeriodicalQuerier()
        {
            Thread.Sleep(100);
            ServerHelper.WriteLine("Creating: PeriodicalQuerier -> " + GetClientName() + " ...OK");
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
    }
}
