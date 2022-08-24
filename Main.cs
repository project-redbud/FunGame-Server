using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System;
using FunGameServer.Sockets;
using System.Net.WebSockets;
using FunGameServer.Models.Config;

bool Running = true;
Socket? ServerSocket = null;

string host = Config.SERVER_IPADRESS;
int port = Config.SERVER_PORT;

try
{
    Task t = Task.Factory.StartNew(() =>
    {
        // 创建IP地址终结点对象
        IPEndPoint ip = new(IPAddress.Parse(host), port);

        // 创建TCP Socket对象并绑定终结点
        ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ServerSocket.Bind(ip);

        // 开始监听连接
        ServerSocket.Listen(Config.MAX_PLAYERS);
        Console.WriteLine("服务器启动成功，正在监听 . . .");

        while (Running)
        {
            Socket socket;
            try
            {
                socket = ServerSocket.Accept();
                IPEndPoint? clientIP = (IPEndPoint?)socket.RemoteEndPoint;
                if (clientIP != null)
                    Console.WriteLine("客户端" + clientIP.ToString() + "连接 . . .");
                else
                    Console.WriteLine("未知地点客户端连接 . . .");
                if (Read(socket) && Send(socket))
                    Task.Factory.StartNew(() =>
                    {
                        new ClientSocket(socket, Running).Start();
                    });
                else
                    if (clientIP != null)
                        Console.WriteLine("客户端" + clientIP.ToString() + "连接失败。");
                    else
                    Console.WriteLine("客户端连接失败。");
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: 客户端断开连接！\n" + e.StackTrace);
            }
        }
        
    });
}
catch (Exception e)
{
    Console.WriteLine(e.StackTrace);
    if (ServerSocket != null)
    {
        ServerSocket.Close();
        ServerSocket = null;
    }
}
finally
{
    while (Running)
    {
        string? order = "";
        order = Console.ReadLine();
        if (order != null && !order.Equals(""))
        {
            switch (order)
            {
                case "quit":
                    Running = false;
                    break;
            }
        }
    }
}

Console.WriteLine("服务器已关闭，按任意键退出程序。");
Console.ReadKey();


bool Read(Socket socket)
{
    // 接收客户端消息
    byte[] buffer = new byte[2048];
    int length = socket.Receive(buffer);
    if (length > 0)
    {
        string msg = Encoding.GetEncoding("unicode").GetString(buffer, 0, length);
        Console.WriteLine("收到来自：客户端（玩家ID） -> " + msg);
        return true;
    }
    else
        Console.WriteLine("客户端没有回应。");
    return false;
}

bool Send(Socket socket)
{
    // 发送消息给客户端
    string msg = ">> 已连接至服务器 -> [ " + host + " ] 连接成功";
    byte[] buffer = new byte[2048];
    buffer = Encoding.GetEncoding("unicode").GetBytes(msg);
    if (socket.Send(buffer) > 0)
    {
        Console.WriteLine("发送给：客户端 <- " + msg);
        return true;
    }
    else
        Console.WriteLine("无法传输数据，与客户端的连接可能丢失。");
    return false;
}

bool IsIP(string ip)
{
    //判断是否为IP
    return Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
}

bool IsEmail(string ip)
{
    //判断是否为Email
    return Regex.IsMatch(ip, @"^(\w)+(\.\w)*@(\w)+((\.\w+)+)$");
}