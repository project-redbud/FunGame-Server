using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System;
using FunGameServer.Sockets;
using System.Net.WebSockets;
using FunGameServer.Models.Config;
using FunGameServer.Utils;
using FunGame.Core.Api.Model.Enum;

Console.Title = Config.SERVER_NAME;
Console.WriteLine(FunGameEnums.GetInfo(Config.FunGameType));

bool Running = true;
Socket? ServerSocket = null;

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
                if (ServerSocket == null)
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
            if (!Config.DefaultINIHelper.ExistINIFile())
            {
                ServerHelper.WriteLine("未检测到配置文件，将自动创建配置文件 . . .");
                Config.DefaultINIHelper.InitServerConfigs();
                ServerHelper.WriteLine("配置文件FunGame.ini创建成功，请修改该配置文件，然后重启服务器。");
                ServerHelper.WriteLine("请输入 help 来获取帮助，输入 quit 关闭服务器。");
                return;
            }
            else
            {
                ServerHelper.GetServerSettings();
                Console.Title = Config.SERVER_NAME + " - FunGame Server Port: " + Config.SERVER_PORT;
            }

            Config.DefaultDataHelper.Close();

            // 连接MySQL服务器
            if (!Config.DefaultDataHelper.Connect())
            {
                Running = false;
                throw new Exception("服务器遇到问题需要关闭，请重新启动服务器！");
            }

            // 创建IP地址终结点对象
            IPEndPoint ip = new(IPAddress.Any, Config.SERVER_PORT);

            // 创建TCP Socket对象并绑定终结点
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.Bind(ip);

            // 开始监听连接
            ServerSocket.Listen(Config.MAX_PLAYERS);
            ServerHelper.WriteLine("Listen -> " + Config.SERVER_PORT);
            ServerHelper.WriteLine("服务器启动成功，开始监听 . . .");

            if (Config.SERVER_NOTICE != "")
                ServerHelper.WriteLine("\n\n********** 服务器公告 **********\n\n" + Config.SERVER_NOTICE + "\n");
            else
                ServerHelper.WriteLine("无法读取服务器公告");

            while (Running)
            {
                Socket socket;
                try
                {
                    socket = ServerSocket.Accept();
                    IPEndPoint? clientIP = (IPEndPoint?)socket.RemoteEndPoint;
                    string clientIPaddress = (clientIP != null) ? clientIP.ToString() : "Unknown";
                    ServerHelper.WriteLine("客户端" + clientIPaddress + "连接 . . .");
                    if (Read(socket) && Send(socket))
                    {
                        ClientSocket cs = new ClientSocket(socket, Running);
                        Task t = Task.Factory.StartNew(() =>
                        {
                            cs.Start();
                        });
                        cs.Task = t;
                    }
                    else
                        ServerHelper.WriteLine("客户端" + clientIPaddress + "连接失败。");
                }
                catch (Exception e)
                {
                    ServerHelper.WriteLine("客户端断开连接！\n" + e.StackTrace);
                }
            }
        }
        catch (Exception e)
        {
            if (e.Message.Equals("服务器遇到问题需要关闭，请重新启动服务器！"))
            {
                if (ServerSocket != null)
                {
                    ServerSocket.Close();
                    ServerSocket = null;
                }
            }
            ServerHelper.Error(e);
        }
        finally
        {
            if (ServerSocket != null)
            {
                ServerSocket.Close();
                ServerSocket = null;
            }
        }

    });
}

bool Read(Socket socket)
{
    // 接收客户端消息
    byte[] buffer = new byte[2048];
    int length = socket.Receive(buffer);
    if (length > 0)
    {
        string msg = Config.DEFAULT_ENCODING.GetString(buffer, 0, length);
        string typestring = EnumHelper.GetSocketTypeName(SocketHelper.GetType(msg));
        msg = SocketHelper.GetMessage(msg);
        if (typestring != SocketMessageType.Unknown.ToString())
        {
            ServerHelper.WriteLine("[ 客户端（" + typestring + "）] -> " + msg);
            return true;
        }
        ServerHelper.WriteLine("客户端发送了不符合FunGame规定的字符，拒绝连接。");
        return false;
    }
    else
        ServerHelper.WriteLine("客户端没有回应。");
    return false;
}

bool Send(Socket socket)
{
    // 发送消息给客户端
    string msg = Config.SERVER_NAME + ";" + Config.SERVER_NOTICE;
    byte[] buffer = new byte[2048];
    buffer = Config.DEFAULT_ENCODING.GetBytes(SocketHelper.MakeMessage((int)SocketMessageType.GetNotice, msg));
    if (socket.Send(buffer) > 0)
    {
        ServerHelper.WriteLine("[ 客户端 ] <- " + "已确认连接");
        return true;
    }
    else
        ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。");
    return false;
}