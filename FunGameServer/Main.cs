﻿using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System;
using FunGameServer.Sockets;
using System.Net.WebSockets;
using FunGameServer.Models.Config;
using FunGameServer.Utils;
using static FunGame.Core.Api.Model.Enum.CommonEnums;

bool Running = true;
Socket? ServerSocket = null;

string hostname = Config.SERVER_NAME;
int port = Config.SERVER_PORT;

Console.Title = Config.CONSOLE_TITLE;

Task t = Task.Factory.StartNew(() =>
{
    try
    {
        // 连接MySQL服务器
        if (!Config.DefaultDataHelper.Connect())
        {
            Running = false;
            throw new Exception("服务器遇到问题需要关闭，请重新启动服务器！");
        }

        // 创建IP地址终结点对象
        IPEndPoint ip = new(IPAddress.Any, port);

        // 创建TCP Socket对象并绑定终结点
        ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        ServerSocket.Bind(ip);

        // 开始监听连接
        ServerSocket.Listen(Config.MAX_PLAYERS);
        ServerHelper.WriteLine("服务器启动成功，端口号 " + port + " ，开始监听 . . .");

        Task.Run(() =>
        {
            Config.ServerNotice = ServerHelper.GetServerNotice();
            if (Config.ServerNotice != "")
                ServerHelper.WriteLine("\n**********服务器公告**********\n" + Config.ServerNotice + "\n\n");
            else
                ServerHelper.WriteLine("无法读取服务器公告");
        });

        while (Running)
        {
            Socket socket;
            try
            {
                socket = ServerSocket.Accept();
                IPEndPoint? clientIP = (IPEndPoint?)socket.RemoteEndPoint;
                if (clientIP != null)
                    ServerHelper.WriteLine("客户端" + clientIP.ToString() + "连接 . . .");
                else
                    ServerHelper.WriteLine("未知地点客户端连接 . . .");
                if (Read(socket) && Send(socket))
                    Task.Factory.StartNew(() =>
                    {
                        new ClientSocket(socket, Running).Start();
                    });
                else
                    if (clientIP != null)
                    ServerHelper.WriteLine("客户端" + clientIP.ToString() + "连接失败。");
                else
                    ServerHelper.WriteLine("客户端连接失败。");
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

while (Running)
{
    string? order = "";
    order = Console.ReadLine();
    ServerHelper.Type();
    if (order != null && !order.Equals("") && Running)
    {
        switch (order)
        {
            case "quit":
                Running = false;
                break;
        }
    }
}

ServerHelper.WriteLine("服务器已关闭，按任意键退出程序。");
Console.ReadKey();


bool Read(Socket socket)
{
    // 接收客户端消息
    byte[] buffer = new byte[2048];
    int length = socket.Receive(buffer);
    if (length > 0)
    {
        string msg = Config.DEFAULT_ENCODING.GetString(buffer, 0, length);
        string typestring = SocketHelper.GetTypeString(SocketHelper.GetType(msg));
        msg = SocketHelper.GetMessage(msg);
        if (typestring != SocketEnums.TYPE_UNKNOWN)
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
    string msg = ">> 已连接至服务器 -> [ " + hostname + " ] 连接成功";
    byte[] buffer = new byte[2048];
    buffer = Config.DEFAULT_ENCODING.GetBytes(SocketHelper.MakeMessage((int)SocketEnums.Type.CheckLogin, msg));
    if (socket.Send(buffer) > 0)
    {
        ServerHelper.WriteLine("[ 客户端 ] <- " + msg);
        return true;
    }
    else
        ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。");
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