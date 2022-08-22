using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System;
using FunGameServer.Sockets;
using System.Net.WebSockets;

bool Running = true;
Socket? ServerSocket = null;

string host = "127.0.0.1";
int port = 22222;

try
{
    Task t = Task.Factory.StartNew(() =>
    {
        // 创建IP地址终结点对象
        IPAddress ip = IPAddress.Parse(host);
        IPEndPoint ipe = new IPEndPoint(ip, port);

        // 创建TCP Socket对象并绑定终结点
        ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ServerSocket.Bind(ipe);

        // 开始监听连接
        ServerSocket.Listen(0);
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
                Task.Factory.StartNew(() =>
                {
                    new ClientSocket(socket, Running).Start();
                });
                // 接收客户端消息
                Receive(socket);
                // 发送给客户端消息
                Send(socket);
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


static void Receive(Socket socket)
{
    byte[] bytes = new byte[1024];
    //从客户端接收消息
    int len = socket.Receive(bytes, bytes.Length, 0);
    //将消息转为字符串
    string recvStr = Encoding.ASCII.GetString(bytes, 0, len);
    Console.WriteLine("接收的客户端消息 ： {0}", recvStr);
}

void Send(Socket socket)
{
    string sendStr = ">> 服务器"  + host + ":" + port + "连接成功";
    Console.WriteLine("发送给客户端消息 ： {0}", sendStr);
    // 将字符串消息转为数组
    byte[] bytes = Encoding.ASCII.GetBytes(sendStr);
    //发送消息给客户端
    socket.Send(bytes, bytes.Length, 0);
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