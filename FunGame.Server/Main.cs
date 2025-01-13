using Milimoe.FunGame;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

Console.Title = Config.ServerName;
Console.WriteLine(FunGameInfo.GetInfo(Config.FunGameType));

bool Running = true;
SocketListener? SocketListener = null;
HTTPListener? WebSocketListener = null;

StartServer();

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // 防止程序立即退出
    CloseServer();
    Environment.Exit(0); // 退出程序
};

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
                CloseServer();
                break;
            case OrderDictionary.Restart:
                if (SocketListener is null || WebSocketListener is null)
                {
                    ServerHelper.WriteLine("重启服务器");
                    StartServer();
                }
                else ServerHelper.WriteLine("服务器正在运行，拒绝重启！");
                break;
            default:
                if (SocketListener != null)
                {
                    await ConsoleModel.Order(SocketListener, order);
                }
                else
                {
                    await ConsoleModel.Order(WebSocketListener, order);
                }
                break;
        }
    }
}

ServerHelper.WriteLine("服务器已关闭，按任意键退出程序。");
Console.ReadKey();

void StartServer()
{
    TaskUtility.NewTask(async () =>
    {
        try
        {
            ServerHelper.WriteLine("正在读取配置文件并初始化服务 . . .");

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

            // 初始化命令菜单
            ServerHelper.InitOrderList();

            // 初始化SQLHelper
            FunGameSystem.InitSQLHelper();

            // 初始化MailSender
            FunGameSystem.InitMailSender();

            // 读取游戏模组
            if (!FunGameSystem.GetGameModuleList())
            {
                ServerHelper.WriteLine("服务器似乎未安装任何游戏模组，请检查是否正确安装它们。");
            }

            // 读取Server插件
            FunGameSystem.GetServerPlugins();

            ServerHelper.WriteLine("请输入 help 来获取帮助，按下 Ctrl+C 关闭服务器。");

            ServerHelper.PrintFunGameTitle();

            // 使用Socket还是WebSocket
            bool useWebSocket = Config.UseWebSocket;

            if (!useWebSocket)
            {
                // 创建监听
                SocketListener listener = SocketListener.StartListening(Config.ServerPort, Config.MaxPlayers);
                SocketListener = listener;

                // 开始监听连接
                listener.BannedList.AddRange(Config.ServerBannedList);
                ServerHelper.WriteLine("Listen -> " + Config.ServerPort);
                ServerHelper.WriteLine("服务器启动成功，开始监听 . . .");

                if (Config.ServerNotice != "")
                    Console.WriteLine("\n\n********** 服务器公告 **********\n\n" + Config.ServerNotice + "\n");
                else
                    Console.WriteLine("无法读取服务器公告");

                ServerHelper.Type();

                while (Running)
                {
                    ServerSocket socket;
                    string clientip = "";
                    try
                    {
                        Guid token = Guid.NewGuid();
                        socket = listener.Accept(token);

                        TaskUtility.NewTask(async () =>
                        {
                            clientip = socket.ClientIP;
                            Config.ConnectingPlayerCount++;
                            bool isConnected = false;
                            bool isDebugMode = false;

                            // 开始处理客户端连接请求
                            SocketObject[] objs = socket.Receive();
                            (isConnected, isDebugMode) = await ConnectController.Connect(listener, socket, token, clientip, objs);
                            if (isConnected)
                            {
                                ServerModel<ServerSocket> ClientModel = new(listener, socket, isDebugMode);
                                ClientModel.SetClientName(clientip);
                                Task t = Task.Run(ClientModel.Start);
                            }
                            else
                            {
                                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 连接失败。", InvokeMessageType.Core);
                            }
                            Config.ConnectingPlayerCount--;
                        }).OnError(e =>
                        {
                            if (--Config.ConnectingPlayerCount < 0) Config.ConnectingPlayerCount = 0;
                            ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 中断连接！", InvokeMessageType.Core);
                            ServerHelper.Error(e);
                        });
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                    }
                }
            }
            else
            {
                if (Config.WebSocketAddress == "*")
                {
                    ServerHelper.WriteLine("WebSocket 监听 * 地址要求权限提升，如果提示拒绝访问请以管理员身份运行服务器。", InvokeMessageType.Warning);
                }

                // 创建监听
                HTTPListener listener = HTTPListener.StartListening(Config.WebSocketAddress, Config.WebSocketPort, Config.WebSocketSubUrl, Config.WebSocketSSL);
                WebSocketListener = listener;

                // 开始监听连接
                listener.BannedList.AddRange(Config.ServerBannedList);
                ServerHelper.WriteLine("Listen -> " + listener.Instance.Prefixes.First());
                ServerHelper.WriteLine("服务器启动成功，开始监听 . . .");

                if (Config.ServerNotice != "")
                    ServerHelper.WriteLine("\n\n********** 服务器公告 **********\n\n" + Config.ServerNotice + "\n");
                else
                    ServerHelper.WriteLine("无法读取服务器公告");

                while (Running)
                {
                    ServerWebSocket socket;
                    string clientip = "";
                    try
                    {
                        Guid token = Guid.NewGuid();
                        socket = await listener.Accept(token);

                        TaskUtility.NewTask(async () =>
                        {
                            clientip = socket.ClientIP;
                            Config.ConnectingPlayerCount++;
                            bool isConnected = false;
                            bool isDebugMode = false;

                            // 开始处理客户端连接请求
                            IEnumerable<SocketObject> objs = [];
                            while (!objs.Any(o => o.SocketType == SocketMessageType.Connect))
                            {
                                objs = objs.Union(await socket.ReceiveAsync());
                            }
                            (isConnected, isDebugMode) = await ConnectController.Connect(listener, socket, token, clientip, objs.Where(o => o.SocketType == SocketMessageType.Connect));
                            if (isConnected)
                            {
                                ServerModel<ServerWebSocket> ClientModel = new(listener, socket, isDebugMode);
                                ClientModel.SetClientName(clientip);
                                Task t = Task.Run(ClientModel.Start);
                            }
                            else
                            {
                                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 连接失败。", InvokeMessageType.Core);
                                await socket.CloseAsync();
                            }
                            Config.ConnectingPlayerCount--;
                        }).OnError(e =>
                        {
                            if (--Config.ConnectingPlayerCount < 0) Config.ConnectingPlayerCount = 0;
                            ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 中断连接！", InvokeMessageType.Core);
                            ServerHelper.Error(e);
                        });
                    }
                    catch (Exception e)
                    {
                        ServerHelper.Error(e);
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (e.Message.Equals(new ServerErrorException().Message))
            {
                if (SocketListener != null)
                {
                    SocketListener.Close();
                    SocketListener = null;
                }
                if (WebSocketListener != null)
                {
                    WebSocketListener.Close();
                    WebSocketListener = null;
                }
            }
            ServerHelper.Error(e);
        }
        finally
        {
            if (SocketListener != null)
            {
                SocketListener.Close();
                SocketListener = null;
            }
            if (WebSocketListener != null)
            {
                WebSocketListener.Close();
                WebSocketListener = null;
            }
        }
    });
}

void CloseServer()
{
    FunGameSystem.CloseServer();
}
