using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.Exception;
using Milimoe.FunGame.Core.Library.Server;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;
using System.Data;

namespace Milimoe.FunGame.Server.Model
{
    public class ServerModel : BaseModel
    {
        /**
         * Public
         */
        public new bool Running = false;
        public new ClientSocket? Socket = null;
        public new Task? Task = null;
        public new string ClientName = "";

        /**
         * Private
         */
        private User? User = null;
        private Guid CheckLoginKey = Guid.Empty;
        private string UserName = "";
        private string Password = "";
        private int FailedTimes = 0; // 超过一定次数断开连接
        private readonly MySQLHelper SQLHelper;

        public ServerModel(ClientSocket socket, bool running)
        {
            Socket = socket;
            Running = running;
            SQLHelper = new(SocketHelper.MakeClientName(socket.ClientIP));
        }

        public override bool Read(ClientSocket socket)
        {
            // 接收客户端消息
            try
            {
                object[] objs = socket.Receive();
                SocketMessageType type = (SocketMessageType)objs[0];
                Guid token = (Guid)objs[1];
                object[] args = (object[])objs[2];
                string msg = "";

                // 如果不等于这些Type，就不会输出一行记录。这些Type有特定的输出。
                SocketMessageType ignoreType = SocketMessageType.HeartBeat | SocketMessageType.Login;
                if ((type & ignoreType) == 0)
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
                                SQLHelper.Script = $"{SQLConstant.Select_Users} {SQLConstant.Command_Where} Username = '{username}' And Password = '{password}'";
                                SQLHelper.ExecuteDataSet(out SQLResult result);
                                if (result == SQLResult.Success && SQLHelper.UpdateRows > 0)
                                {
                                    DataRow UserRow = SQLHelper.DataSet.Tables[0].Rows[0];
                                    if (autokey != null && autokey.Trim() != "")
                                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] AutoKey: 已确认");
                                    UserName = username;
                                    Password = password;
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
                            Guid checkloginkey = NetworkUtility.ConvertJsonObject<Guid>(args[0]);
                            if (CheckLoginKey.Equals(checkloginkey))
                            {
                                // 创建User对象
                                User = Factory.New<User>(UserName, Password);
                                // 检查有没有重复登录的情况
                                KickUser();
                                // 添加至玩家列表
                                AddUser();
                                GetUserCount();
                                return Send(socket, type, UserName, Password);
                            }
                            ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登录。");
                        }
                        return Send(socket, type, CheckLoginKey.ToString());

                    case SocketMessageType.Logout:
                        Guid checklogoutkey = Guid.Empty;
                        if (args != null && args.Length > 0)
                        {
                            checklogoutkey = NetworkUtility.ConvertJsonObject<Guid>(args[0]);
                            if (CheckLoginKey.Equals(checklogoutkey))
                            {
                                // 从玩家列表移除
                                RemoveUser();
                                GetUserCount();
                                CheckLoginKey = Guid.Empty;
                                msg = "你已成功退出登录！ ";
                                return  Send(socket, type, checklogoutkey, msg);
                            }
                        }
                        ServerHelper.WriteLine("客户端发送了错误的秘钥，不允许本次登出。");
                        return Send(socket, type, checklogoutkey);

                    case SocketMessageType.Disconnect:
                        msg = "你已成功断开与服务器的连接: " + Config.SERVER_NAME + "。 ";
                        GetUserCount();
                        break;

                    case SocketMessageType.HeartBeat:
                        msg = "";
                        break;

                    case SocketMessageType.IntoRoom:
                        msg = "-1";
                        if (args != null && args.Length > 0) msg = NetworkUtility.ConvertJsonObject<string>(args[0])!;
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

        public override bool Send(ClientSocket socket, SocketMessageType type, params object[] objs)
        {
            // 发送消息给客户端
            try
            {
                if (socket.Send(type, objs) == SocketResult.Success)
                {
                    // Logout和Disconnect需要移除User与其线程
                    if (type == SocketMessageType.Logout)
                    {
                        RemoveUser();
                    }
                    if (type == SocketMessageType.Disconnect)
                    {
                        RemoveUser();
                        Close();
                    }
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

        public override void Start()
        {
            Task StreamReader = Task.Factory.StartNew(() =>
            {
                CreateStreamReader();
            });
        }

        private void KickUser()
        {
            if (User != null && Config.OnlinePlayers.ContainsKey(User.Username))
            {
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 重复登录！");
                Config.OnlinePlayers.TryGetValue(User.Username, out ServerModel? serverTask);
                serverTask?.Send(serverTask.Socket!, SocketMessageType.Logout, serverTask.CheckLoginKey, "您的账号在别处登录，已强制下线。");
            }
        }

        private bool AddUser()
        {
            if (User != null)
            {
                if (this != null)
                {
                    Config.OnlinePlayers.AddOrUpdate(User.Username, this, (key, value) => value);
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已添加");
                    return true;
                }
            }
            return false;
        }

        private bool RemoveUser()
        {
            if (this != null && User != null)
            {
                if (Config.OnlinePlayers.TryRemove(User.Username, out _))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已移除");
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
                            ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " Error -> Too Many Faileds.");
                            ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " Close -> StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (FailedTimes - 1 >= 0) FailedTimes--;
                }
                else
                {
                    RemoveUser();
                    GetUserCount();
                    ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " Error -> Socket is Closed.");
                    ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " Close -> StringStream is Closed.");
                    break;
                }
            }
        }

        private void Close()
        {
            try
            {
                SQLHelper.Close();
                if (Socket != null)
                {
                    Socket.Close();
                    Socket = null;
                }
                Running = false;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

    }
}
