using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Models
{
    public class ClientWebSocketServerModel : BaseServerModel<ClientWebSocket>
    {
        public override ISocketMessageProcessor Socket { get; }
        public override ISocketListener<ClientWebSocket> Listener { get; }
        public override DataRequestController<ClientWebSocket> DataRequestController { get; }
        public override bool IsDebugMode { get; }

        public ClientWebSocketServerModel(ISocketListener<ClientWebSocket> server, ClientWebSocket socket, bool isDebugMode)
        {
            Listener = server;
            Socket = socket;
            DataRequestController = new(this);
            IsDebugMode = isDebugMode;
            if (Config.SQLMode == SQLMode.MySQL) _sqlHelper = new MySQLHelper(this);
            else if (Config.SQLMode == SQLMode.SQLite) _sqlHelper = Config.SQLHelper;
            _mailer = SmtpHelper.GetMailSender();
        }

        public override async Task<bool> Read(ISocketMessageProcessor socket)
        {
            // 接收客户端消息
            try
            {
                SocketObject[] objs = [];
                // 确保 socket 是 ClientWebSocket
                if (socket is ClientWebSocket realSocket)
                {
                    objs = await realSocket.ReceiveAsync();
                }

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

        protected override async Task CreateStreamReader()
        {
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
        }
    }
}
