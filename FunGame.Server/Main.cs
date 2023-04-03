using Milimoe.FunGame;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

Console.Title = Config.ServerName;
Console.WriteLine(FunGameInfo.GetInfo(Config.FunGameType));

bool Running = true;
ServerSocket? ListeningSocket = null;

StartServer();

while (Running)
{
    string? order = "";
    order = Console.ReadLine();
    ServerHelper.Type();
    if (order != null && !order.Equals("") && Running)
    {
        switch (order)
        {
            case OrderDictionary.Quit:
            case OrderDictionary.Exit:
            case OrderDictionary.Close:
                Running = false;
                break;
            case OrderDictionary.Help:
                ServerHelper.WriteLine("Milimoe -> 帮助");
                break;
            case OrderDictionary.Restart:
                if (ListeningSocket == null)
                {
                    ServerHelper.WriteLine("重启服务器");
                    StartServer();
                }
                else
                    ServerHelper.WriteLine("服务器正在运行，拒绝重启！");
                break;
        }
    }
}

ServerHelper.WriteLine("服务器已关闭，按任意键退出程序。");
Console.ReadKey();

void StartServer()
{
    Task t = Task.Factory.StartNew(() =>
    {
        try
        {
            ServerHelper.WriteLine("正在读取配置文件并初始化服务 . . .");
            // 初始化命令菜单
            ServerHelper.InitOrderList();

            // 检查是否存在配置文件
            if (!INIHelper.ExistINIFile())
            {
                ServerHelper.WriteLine("未检测到配置文件，将自动创建配置文件 . . .");
                INIHelper.Init(Config.FunGameType);
                ServerHelper.WriteLine("配置文件FunGame.ini创建成功，请修改该配置文件，然后重启服务器。");
                return;
            }
            else
            {
                ServerHelper.GetServerSettings();
                Console.Title = Config.ServerName + " - FunGame Server Port: " + Config.ServerPort;
            }
            ServerHelper.WriteLine("请输入 help 来获取帮助，输入 quit 关闭服务器。");

            // 测试MySQL服务器连接
            if (TestSQLConnection() != SQLResult.Success)
            {
                Running = false;
                throw new SQLQueryException();
            }
            
            // 创建监听
            ListeningSocket = ServerSocket.StartListening();

            // 开始监听连接
            AddBannedList(ListeningSocket);
            Config.RoomList = new(ListeningSocket);
            ServerHelper.WriteLine("Listen -> " + Config.ServerPort);
            ServerHelper.WriteLine("服务器启动成功，开始监听 . . .");

            if (Config.ServerNotice != "")
                ServerHelper.WriteLine("\n\n********** 服务器公告 **********\n\n" + Config.ServerNotice + "\n");
            else
                ServerHelper.WriteLine("无法读取服务器公告");

            while (Running)
            {
                ClientSocket socket;
                string ClientIPAddress = "";
                try
                {
                    Guid Token = Guid.NewGuid();
                    socket = ListeningSocket.Accept(Token);
                    ClientIPAddress = socket.ClientIP;
                    if (Config.ConnectingPlayersCount + Config.OnlinePlayersCount + 1 > Config.MaxPlayers)
                    {
                        SendRefuseConnect(socket, "服务器可接受的连接数量已上限！");
                        ServerHelper.WriteLine("服务器可接受的连接数量已上限！");
                        continue;
                    }
                    Config.ConnectingPlayersCount++;
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientIPAddress) + " 正在连接服务器 . . .");
                    if (IsIPBanned(ListeningSocket, ClientIPAddress))
                    {
                        SendRefuseConnect(socket, "服务器已拒绝黑名单用户连接。");
                        ServerHelper.WriteLine("检测到 " + ServerHelper.MakeClientName(ClientIPAddress) + " 为黑名单用户，已禁止其连接！");
                        Config.ConnectingPlayersCount--;
                        continue;
                    }
                    if (Read(socket) && Send(socket, Token))
                    {
                        ServerModel ClientModel = new(ListeningSocket, socket, Running);
                        Task t = Task.Factory.StartNew(() =>
                        {
                            ClientModel.Start();
                        });
                        ClientModel.SetTaskAndClientName(t, ClientIPAddress);
                    }
                    else
                        ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientIPAddress) + " 连接失败。");
                    Config.ConnectingPlayersCount--;
                }
                catch (Exception e)
                {
                    if (--Config.ConnectingPlayersCount < 0) Config.ConnectingPlayersCount = 0;
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(ClientIPAddress) + " 断开连接！");
                    ServerHelper.Error(e);
                }
            }
        }
        catch (Exception e)
        {
            if (e.Message.Equals(new ServerErrorException().Message))
            {
                if (ListeningSocket != null)
                {
                    ListeningSocket.Close();
                    ListeningSocket = null;
                }
            }
            ServerHelper.Error(e);
        }
        finally
        {
            if (ListeningSocket != null)
            {
                ListeningSocket.Close();
                ListeningSocket = null;
            }
        }

    });
}

bool Read(ClientSocket socket)
{
    // 接收客户端消息
    SocketObject read = socket.Receive();
    SocketMessageType type = read.SocketType;
    object[] objs = read.Parameters;
    if (type != SocketMessageType.Unknown)
    {
        if (objs[0] != null && objs[0].GetType() == typeof(string) && objs[0].ToString()!.Trim() != "")
            ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + ServerHelper.MakeClientName(socket.ClientIP) + " -> " + objs[0]);
        else
            ServerHelper.WriteLine("[" + ServerSocket.GetTypeString(type) + "] " + ServerHelper.MakeClientName(socket.ClientIP));
        return true;
    }
    ServerHelper.WriteLine("客户端发送了不符合FunGame规定的字符，拒绝连接。");
    return false;
}

bool Send(ClientSocket socket, Guid Token)
{
    // 发送消息给客户端
    string msg = Config.ServerName + ";" + Config.ServerNotice;
    if (socket.Send(SocketMessageType.Connect, msg, Token) == SocketResult.Success)
    {
        ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "已确认连接");
        return true;
    }
    else
        ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。");
    return false;
}

bool SendRefuseConnect(ClientSocket socket, string msg)
{
    // 发送消息给客户端
    msg = $"连接被拒绝，如有疑问请联系服务器管理员：{msg}";
    if (socket.Send(SocketMessageType.Connect, msg) == SocketResult.Success)
    {
        ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "已拒绝连接");
        return true;
    }
    else
        ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。");
    return false;
}

SQLResult TestSQLConnection()
{
    new MySQLHelper(ServerLoginLogs.Insert_ServerLoginLogs(Config.ServerName, Config.ServerKey)).Execute(out SQLResult TestResult);
    return TestResult;
}

void AddBannedList(ServerSocket server)
{
    string[] bans = Config.ServerBannedList.Split(',');
    foreach (string banned in bans)
    {
        server.BannedList.Add(banned.Trim());
    }
}

bool IsIPBanned(ServerSocket server, string ip)
{
    string[] strs = ip.Split(":");
    if (strs.Length == 2 && server.BannedList.Contains(strs[0]))
    {
        return true;
    }
    return false;
}