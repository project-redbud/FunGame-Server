using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public class ServerModel
    {
        /**
         * Public
         */
        public bool Running = false;
        public ClientSocket? Socket = null;
        public Task? Task = null;
        public string ClientName = "";

        /**
         * Private
         */
        private User? User = null;
        private Guid CheckLoginKey = Guid.Empty;
        private string UserName = "";
        private string Password = "";

        public ServerModel(ClientSocket socket, bool running)
        {
            Socket = socket;
            Running = running;
        }

        private int FailedTimes = 0; // 超过一定次数断开连接

        private bool Read(ClientSocket socket)
        {
            // 接收客户端消息
            try
            {
                object[] objs = socket.Receive();
                SocketMessageType type = (SocketMessageType)objs[0];
                object[] args = (object[])objs[1];
                string msg = "";

                // 如果不等于这些Type，就不会输出一行记录。这些Type有特定的输出。
                SocketMessageType ignoreType = SocketMessageType.HeartBeat | SocketMessageType.Login;
                if (type != ignoreType)
                {
                    if (msg.Trim() == "")
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + SocketHelper.MakeClientName(ClientName, User));
                    else
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + SocketHelper.MakeClientName(ClientName, User) + " -> " + msg);
                }

                switch (type)
                {
                    case SocketMessageType.GetNotice:
                        msg = Config.SERVER_NOTICE;
                        break;

                    case SocketMessageType.Login:
                        CheckLoginKey = Guid.Empty;
                        if (args != null)
                        {
                            string? username = "", password = "", autokey = "";
                            if (args.Length > 0) username = NetworkUtility.ConvertJsonObject<string>(args[0]);
                            if (args.Length > 1) password = NetworkUtility.ConvertJsonObject<string>(args[1]);
                            if (args.Length > 2) autokey = NetworkUtility.ConvertJsonObject<string>(args[2]);
                            if (username != null && password != null)
                            {
                                ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] UserName: " + username);
                                if (username == "test" && password == "123456".Encrypt("test"))
                                {
                                    if (autokey != null) ServerHelper.WriteLine(autokey);
                                    UserName = username;
                                    Password = password;
                                    CheckLoginKey = Guid.NewGuid();
                                    return Send(socket, type, CheckLoginKey);
                                }
                            }
                        }
                        return Send(socket, type, CheckLoginKey);

                    case SocketMessageType.CheckLogin:
                        if (args != null && args.Length > 0)
                        {
                            Guid checkloginkey = NetworkUtility.ConvertJsonObject<Guid>(args[0]);
                            if (CheckLoginKey.Equals(checkloginkey))
                            {
                                // 添加至玩家列表
                                User = Factory.New<User>(UserName, Password);
                                AddUser();
                                GetUserCount();
                                return Send(socket, type, UserName, Password);
                            }
                            ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登录。");
                        }
                        return Send(socket, type, CheckLoginKey.ToString());

                    case SocketMessageType.Logout:
                        msg = "你已成功退出登录！ ";
                        GetUserCount();
                        break;

                    case SocketMessageType.Disconnect:
                        msg = "你已成功断开与服务器的连接: " + Config.SERVER_NAME + "。 ";
                        GetUserCount();
                        break;

                    case SocketMessageType.HeartBeat:
                        msg = "";
                        break;
                }
                return Send(socket, type, msg);
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        private bool Send(ClientSocket socket, SocketMessageType type, params object[] objs)
        {
            // 发送消息给客户端
            try
            {
                if (socket.Send(type, objs) == SocketResult.Success)
                {
                    // Logout和Disconnect需要移除User与其线程
                    if (type == SocketMessageType.Logout || type == SocketMessageType.Disconnect)
                        RemoveUser();
                    object obj = objs[0];
                    if (obj.GetType() == typeof(string) && (string)obj != "")
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + SocketHelper.MakeClientName(ClientName, User) + " <- " + obj);
                    return true;
                }
                throw new CanNotSendToClientException();
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " 没有回应。");
                ServerHelper.Error(e);
                return false;
            }
        }

        public void Start()
        {
            Task StreamReader = Task.Factory.StartNew(() =>
            {
                CreateStreamReader();
            });
        }

        private void KickUser()
        {
            if (User != null)
            {
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 重复登录！");
            }
        }

        private bool AddUser()
        {
            if (User != null)
            {
                if (!Config.OnlinePlayers.ContainsKey(User.Username))
                {
                    if (Task != null)
                    {
                        Config.OnlinePlayers.AddOrUpdate(User.Username, Task, (key, value) => value);
                        ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已添加");
                        return true;
                    }
                }
                else
                {
                    KickUser();
                }
            }
            return false;
        }

        private bool RemoveUser()
        {
            if (Task != null && User != null)
            {
                if (Config.OnlinePlayers.TryRemove(User.Username, out Task))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已移除");
                    Task = null;
                    User = null;
                    return true;
                }
                else ServerHelper.WriteLine("OnlinePlayers: 移除玩家 " + User.Username + " 失败");
            }
            return false;
        }

        private void GetUserCount()
        {
            ServerHelper.WriteLine("目前在线玩家数量: " + Config.OnlinePlayers.Count);
        }

        private void CreateStreamReader()
        {
            Thread.Sleep(100);
            ServerHelper.WriteLine("Creating: StreamReader -> " + SocketHelper.MakeClientName(ClientName, User) + " ...OK");
            while (Running)
            {
                if (Socket != null)
                {
                    if (!Read(Socket))
                    {
                        FailedTimes++;
                        if (FailedTimes >= Config.MAX_CONNECTFAILED)
                        {
                            RemoveUser();
                            GetUserCount();
                            ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " ERROR -> Too Many Faileds.");
                            ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " CLOSE -> StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (FailedTimes - 1 >= 0) FailedTimes--;
                }
                else
                {
                    RemoveUser();
                    GetUserCount();
                    ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " ERROR -> Socket is Closed.");
                    ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " CLOSE -> StringStream is Closed.");
                    break;
                }
            }
        }
    }
}
