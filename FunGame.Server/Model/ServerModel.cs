using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.Server;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public class ServerModel : BaseModel
    {
        /**
         * Public
         */
        public override bool Running => _Running;
        public override ClientSocket? Socket => _Socket;
        public override Task? Task => _Task;
        public override string ClientName => _ClientName;
        public override User? User => _User;

        /**
         * Private
         */
        private ClientSocket? _Socket = null;
        private bool _Running = false;
        private User? _User = null;
        private Task? _Task = null;
        private string _ClientName = "";

        private Guid CheckLoginKey = Guid.Empty;
        private string RegVerify = "";
        private int FailedTimes = 0; // 超过一定次数断开连接
        private string UserName = "";
        private string Password = "";
        private string RoomID = ""; 
        private readonly ServerSocket Server;
        private readonly MySQLHelper SQLHelper;
        private readonly MailSender? MailSender;

        public ServerModel(ServerSocket server, ClientSocket socket, bool running)
        {
            Server = server;
            _Socket = socket;
            _Running = running;
            SQLHelper = new(this);
            MailSender = SmtpHelper.GetMailSender();
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
                SocketMessageType[] IgnoreType = new SocketMessageType[] { SocketMessageType.HeartBeat, SocketMessageType.Login, SocketMessageType.IntoRoom,
                    SocketMessageType.Chat};
                if (!IgnoreType.Contains(type))
                {
                    if (msg.Trim() == "")
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + SocketHelper.MakeClientName(ClientName, User));
                    else
                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + SocketHelper.MakeClientName(ClientName, User) + " -> " + msg);
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
                            if (args.Length > 0) username = NetworkUtility.ConvertJsonObject<string>(args[0]);
                            if (args.Length > 1) password = NetworkUtility.ConvertJsonObject<string>(args[1]);
                            if (args.Length > 2) autokey = NetworkUtility.ConvertJsonObject<string>(args[2]);
                            if (username != null && password != null)
                            {
                                ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] UserName: " + username);
                                SQLHelper.Script = UserQuery.Select_Users_LoginQuery(username, password);
                                SQLHelper.ExecuteDataSet(out SQLResult result);
                                if (result == SQLResult.Success)
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
                                _User = Factory.New<User>(UserName, Password);
                                // 检查有没有重复登录的情况
                                KickUser();
                                // 添加至玩家列表
                                AddUser();
                                GetUsersCount();
                                // CheckLogin
                                SQLHelper.Script = UserQuery.Update_CheckLogin(UserName, socket.ClientIP.Split(':')[0]);
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
                        GetUsersCount();
                        break;

                    case SocketMessageType.HeartBeat:
                        msg = "";
                        break;

                    case SocketMessageType.IntoRoom:
                        msg = "-1";
                        if (args != null && args.Length > 0) msg = NetworkUtility.ConvertJsonObject<string>(args[0])!;
                        RoomID = msg;
                        break;

                    case SocketMessageType.Chat:
                        if (args != null && args.Length > 0) msg = NetworkUtility.ConvertJsonObject<string>(args[0])!;
                        ServerHelper.WriteLine(msg);
                        foreach (ServerModel Client in Server.GetUsersList.Cast<ServerModel>())
                        {
                            if (RoomID == Client.RoomID)
                            {
                                if (Client != null && User != null)
                                {
                                    Client.Send(Client.Socket!, SocketMessageType.Chat, User.Username, msg);
                                }
                            }
                        }
                        return true;

                    case SocketMessageType.Reg:
                        if (args != null)
                        {
                            string? username = "", email = "";
                            if (args.Length > 0) username = NetworkUtility.ConvertJsonObject<string>(args[0]);
                            if (args.Length > 1) email = NetworkUtility.ConvertJsonObject<string>(args[1]);
                            if (username != null && email != null)
                            {
                                RegVerify = Verification.CreateVerifyCode(VerifyCodeType.NumberVerifyCode, 6);
                                SQLHelper.Script = RegVerifyCodes.Insert_RegVerifyCodes(username, email, RegVerify);
                                SQLHelper.Execute(out SQLResult result);
                                if (result == SQLResult.Success && MailSender != null)
                                {
                                    string ServerName = Config.ServerName;
                                    string Subject = $"[{ServerName}] FunGame 注册验证码";
                                    string Body = $"亲爱的 {username}， <br/>    感谢您注册[{ServerName}]，您的验证码是 {RegVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.DateOnly)}";
                                    string[] To = new string[] { email };
                                    string[] CC = new string[] { OfficialEmail.SupportEmail };
                                    if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To, CC)) == MailSendResult.Success)
                                    {
                                        ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + $" 已向{email}发送验证码：{RegVerify}");
                                    }
                                    else
                                    {
                                        ServerHelper.WriteLine(SocketHelper.MakeClientName(ClientName, User) + " 无法发送验证码。");
                                        ServerHelper.WriteLine(MailSender.ErrorMsg);
                                    }
                                }
                            }
                        }
                        return true;

                    case SocketMessageType.CheckReg:
                        if (args != null)
                        {
                            string? username = "", password = "", email = "", verifycode = "";
                            if (args.Length > 0) username = NetworkUtility.ConvertJsonObject<string>(args[0]);
                            if (args.Length > 1) password = NetworkUtility.ConvertJsonObject<string>(args[1]);
                            if (args.Length > 2) email = NetworkUtility.ConvertJsonObject<string>(args[2]);
                            if (args.Length > 3) verifycode = NetworkUtility.ConvertJsonObject<string>(args[3]);
                            if (username != null && password != null && email != null && verifycode != null)
                            {
                                // 先检查验证码
                                SQLHelper.Script = RegVerifyCodes.Select_RegVerifyCode(username, email, verifycode);
                                SQLHelper.ExecuteDataSet(out SQLResult result);
                                if (result == SQLResult.Success)
                                {
                                    if (RegVerify.Equals(SQLHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode]))
                                    {
                                        // 注册
                                        ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] UserName: " + username + " Email: " + email);
                                        SQLHelper.Script = UserQuery.Insert_Register(username, password, email);
                                        SQLHelper.Execute(out result);
                                        if (result == SQLResult.Success)
                                        {
                                            msg = "注册成功！请牢记您的账号与密码！";
                                            SQLHelper.Script = RegVerifyCodes.Delete_RegVerifyCode(username, email);
                                            return Send(socket, type, true, msg);
                                        }
                                        else msg = "服务器无法处理您的注册，注册失败！";
                                    }
                                    else msg = "验证码不正确，请重新输入！";
                                }
                                else if (result == SQLResult.NotFound) msg = "验证码不正确，请重新输入！";
                                else msg = "服务器无法处理您的注册，注册失败！";
                            }
                        }
                        else msg = "注册失败！";
                        return Send(socket, type, false, msg);
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

        public void SetTaskAndClientName(Task t, string ClientName)
        {
            _Task = t;
            _ClientName = ClientName;
        }

        private void KickUser()
        {
            if (User != null)
            {
                string user = User.Username;
                if (Server.ContainsUser(user))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + user + " 重复登录！");
                    ServerModel serverTask = (ServerModel)Server.GetUser(user);
                    serverTask?.Send(serverTask.Socket!, SocketMessageType.Logout, serverTask.CheckLoginKey, "您的账号在别处登录，已强制下线。");
                }
            }
        }

        private bool AddUser()
        {
            if (User != null && this != null)
            {
                Server.AddUser(User.Username, this);
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已添加");
                return true;
            }
            return false;
        }

        private bool RemoveUser()
        {
            if (User != null && this != null)
            {
                if (Server.RemoveUser(User.Username))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Username + " 已移除");
                    _User = null;
                    return true;
                }
                else ServerHelper.WriteLine("OnlinePlayers: 移除玩家 " + User.Username + " 失败");
            }
            return false;
        }

        private void GetUsersCount()
        {
            ServerHelper.WriteLine("目前在线玩家数量: " + Server.UsersCount);
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
                        if (FailedTimes >= Config.MaxConnectionFaileds)
                        {
                            RemoveUser();
                            Close();
                            GetUsersCount();
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
                    Close();
                    GetUsersCount();
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
                MailSender?.Dispose();
                Socket?.Close();
                _Socket = null;
                _Running = false;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

    }
}
