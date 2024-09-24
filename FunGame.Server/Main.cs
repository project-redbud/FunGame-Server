using System.Collections;
using Milimoe.FunGame;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
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
    string order = Console.ReadLine() ?? "";
    ServerHelper.Type();
    if (order != "" && Running)
    {
        order = order.ToLower();
        switch (order)
        {
            case OrderDictionary.Quit:
            case OrderDictionary.Exit:
            case OrderDictionary.Close:
                Running = false;
                break;
            case OrderDictionary.Restart:
                if (ListeningSocket == null)
                {
                    ServerHelper.WriteLine("重启服务器");
                    StartServer();
                }
                else ServerHelper.WriteLine("服务器正在运行，拒绝重启！");
                break;
            default:
                ConsoleModel.Order(ListeningSocket, order);
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

            // 读取游戏模组
            if (!GetGameModuleList())
            {
                ServerHelper.WriteLine("服务器似乎未安装任何游戏模组，请检查是否正确安装它们。");
            }

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

            // 创建全局SQLHelper
            Config.InitSQLHelper();

            // 创建监听
            ListeningSocket = ServerSocket.StartListening();

            // 开始监听连接
            ListeningSocket.BannedList.AddRange(Config.ServerBannedList);
            ServerHelper.WriteLine("Listen -> " + Config.ServerPort);
            ServerHelper.WriteLine("服务器启动成功，开始监听 . . .");

            if (Config.ServerNotice != "")
                ServerHelper.WriteLine("\n\n********** 服务器公告 **********\n\n" + Config.ServerNotice + "\n");
            else
                ServerHelper.WriteLine("无法读取服务器公告");

            while (Running)
            {
                ClientSocket socket;
                string clientip = "";
                try
                {
                    Guid token = Guid.NewGuid();
                    socket = ServerSocket.Accept(token);
                    clientip = socket.ClientIP;
                    Config.ConnectingPlayerCount++;
                    // 开始处理客户端连接请求
                    bool isDebugMode = false;
                    if (Connect(socket, token, clientip, ref isDebugMode))
                    {
                        ServerModel ClientModel = new(ListeningSocket, socket, Running, isDebugMode);
                        Task t = Task.Factory.StartNew(() =>
                        {
                            ClientModel.Start();
                        });
                        ClientModel.SetClientName(clientip);
                    }
                    else
                    {
                        ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 连接失败。", InvokeMessageType.Core);
                    }
                    Config.ConnectingPlayerCount--;
                }
                catch (Exception e)
                {
                    if (--Config.ConnectingPlayerCount < 0) Config.ConnectingPlayerCount = 0;
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 中断连接！", InvokeMessageType.Core);
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

bool GetGameModuleList()
{
    List<string> supported = [];
    // 构建AddonController
    Hashtable delegates = [];
    delegates.Add("WriteLine", new Action<string>(msg => ServerHelper.WriteLine(msg, InvokeMessageType.GameModule)));
    delegates.Add("Error", new Action<Exception>(ServerHelper.Error));
    // 读取modules目录下的模组
    Config.GameModuleLoader = GameModuleLoader.LoadGameModules(Config.FunGameType, delegates);
    foreach (GameModuleServer module in Config.GameModuleLoader.ModuleServers.Values)
    {
        bool check = true;
        // 检查模组是否有相对应的地图
        if (!Config.GameModuleLoader.Maps.ContainsKey(module.DefaultMap))
        {
            ServerHelper.WriteLine("[GameModule] Load Failed: " + module + " 没有找到相对应的地图，加载失败");
            check = false;
        }
        if (check)
        {
            supported.Add(module.Name);
        }
    }
    // 设置全局
    Config.GameModuleSupported = supported.Distinct().ToArray();
    foreach (string modename in Config.GameModuleSupported)
    {
        ServerHelper.WriteLine("[GameModule] Loaded: " + modename);
    }

    return Config.GameModuleSupported.Length > 0;
}

bool Connect(ClientSocket socket, Guid token, string clientip, ref bool isDebugMode)
{
    // 接收客户端消息
    foreach (SocketObject read in socket.Receive())
    {
        if (read.SocketType == SocketMessageType.Connect)
        {
            if (Config.ConnectingPlayerCount + Config.OnlinePlayerCount > Config.MaxPlayers)
            {
                SendRefuseConnect(socket, "服务器可接受的连接数量已上限！");
                ServerHelper.WriteLine("服务器可接受的连接数量已上限！", InvokeMessageType.Core);
                return false;
            }
            ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 正在连接服务器 . . .", InvokeMessageType.Core);
            if (IsIPBanned(ListeningSocket, clientip))
            {
                SendRefuseConnect(socket, "服务器已拒绝黑名单用户连接。");
                ServerHelper.WriteLine("检测到 " + ServerHelper.MakeClientName(clientip) + " 为黑名单用户，已禁止其连接！", InvokeMessageType.Core);
                return false;
            }

            ServerHelper.WriteLine("[" + SocketSet.GetTypeString(read.SocketType) + "] " + ServerHelper.MakeClientName(socket.ClientIP), InvokeMessageType.Core);

            // 读取参数
            // 参数1：客户端的游戏模组列表，没有服务器的需要拒绝
            string[] modes = read.GetParam<string[]>(0) ?? [];
            // 参数2：客户端是否开启了开发者模式，开启开发者模式部分功能不可用
            isDebugMode = read.GetParam<bool>(1);
            if (isDebugMode) ServerHelper.WriteLine("客户端已开启开发者模式");

            string msg = "";
            List<string> ClientDontHave = [];
            string strDontHave = string.Join("\r\n", Config.GameModuleSupported.Where(mode => !modes.Contains(mode)));
            if (strDontHave != "")
            {
                strDontHave = "客户端缺少服务器所需的模组：" + strDontHave;
                ServerHelper.WriteLine(strDontHave, InvokeMessageType.Core);
                msg += strDontHave;
            }

            if (msg == "" && socket.Send(SocketMessageType.Connect, true, msg, token, Config.ServerName, Config.ServerNotice) == SocketResult.Success)
            {
                ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "已确认连接", InvokeMessageType.Core);
                return true;
            }
            else if (msg != "" && socket.Send(SocketMessageType.Connect, false, msg) == SocketResult.Success)
            {
                ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "拒绝连接", InvokeMessageType.Core);
                return false;
            }
            else
            {
                ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。", InvokeMessageType.Core);
                return false;
            }
        }
    }

    SendRefuseConnect(socket, "服务器已拒绝连接。");
    ServerHelper.WriteLine("客户端发送了不符合FunGame规定的字符，拒绝连接。", InvokeMessageType.Core);
    return false;
}

bool SendRefuseConnect(ClientSocket socket, string msg)
{
    // 发送消息给客户端
    msg = "连接被拒绝，如有疑问请联系服务器管理员：" + msg;
    if (socket.Send(SocketMessageType.Connect, false, msg) == SocketResult.Success)
    {
        ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "已拒绝连接", InvokeMessageType.Core);
        return true;
    }
    else
    {
        ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。", InvokeMessageType.Core);
        return false;
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