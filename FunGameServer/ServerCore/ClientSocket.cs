using FunGameServer.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using FunGameServer.Utils;
using System.Reflection.Metadata;
using FunGame.Core.Api.Model.Entity;
using System.Net;
using FunGame.Core.Api.Model.Enum;
using FunGame.Core.Api.Util;
using MySqlX.XDevAPI.Common;

namespace FunGameServer.ServerCore
{
    public class ClientSocket
    {
        public bool Running { get; set; } = false;
        public Socket? Socket { get; set; } = null;
        public Task? Task = null;

        private User? User = null;

        public ClientSocket(Socket socket, bool running)
        {
            Socket = socket;
            Running = running;
        }

        private int FailedTimes = 0; // 超过一定次数断开连接

        private bool Read(Socket socket)
        {
            // 接收客户端消息
            try
            {
                byte[] buffer = new byte[2048];
                int length = socket.Receive(buffer);
                if (length > 0)
                {
                    string msg = Config.DEFAULT_ENCODING.GetString(buffer, 0, length);
                    int type = SocketHelper.GetType(msg);
                    string typestring = EnumHelper.GetSocketTypeName(type);
                    msg = SocketHelper.GetMessage(msg);
                    if (type != (int)SocketMessageType.HeartBeat) ServerHelper.WriteLine("[ 客户端（" + typestring + "）] -> " + msg);
                    switch (type)
                    {
                        case (int)SocketMessageType.GetNotice:
                            msg = Config.SERVER_NOTICE;
                            break;
                        case (int)SocketMessageType.Login:
                            break;
                        case (int)SocketMessageType.CheckLogin:
                            // 添加至玩家列表
                            User = new User(msg);
                            msg = " >> 欢迎回来， " + msg + " 。";
                            AddUser();
                            ServerHelper.WriteLine("目前在线玩家数量: " + Config.OnlinePlayers.Count);
                            break;
                        case (int)SocketMessageType.Logout:
                            msg = " >> 你已成功退出登录！ ";
                            RemoveUser();
                            GetUserCount();
                            break;
                        case (int)SocketMessageType.Disconnect:
                            msg = " >> 你已成功断开与服务器的连接: " + Config.SERVER_NAME + "。 ";
                            RemoveUser();
                            GetUserCount();
                            break;
                        case (int)SocketMessageType.HeartBeat:
                            msg = "";
                            break;
                    }
                    return Send(socket, type, msg);
                }
                throw new Exception();
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine("客户端没有回应。\n" + e.StackTrace);
                return false;
            }
        }

        private bool Send(Socket socket, int type, string msg, object[]? objs = null)
        {
            // 发送消息给客户端
            try
            {
                byte[] buffer = new byte[2048];
                buffer = Config.DEFAULT_ENCODING.GetBytes(Convert.ToString(SocketHelper.MakeMessage(type, msg)));
                string typestring = EnumHelper.GetSocketTypeName(type);
                if (socket.Send(buffer) > 0)
                {
                    if (msg != "")
                        ServerHelper.WriteLine("[ 客户端（" + typestring + "）] <- " + msg);
                    return true;
                }
                throw new Exception();
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine("客户端没有回应。" + e.StackTrace);
                return false;
            }
        }

        public void Start()
        {
            Task StreamReader = Task.Factory.StartNew(() =>
            {
                CreateStreamReader();
            });
        }

        private void KickUser()
        {
            if (User != null)
            {
                ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Userame + " 重复登录！");
            }
        }

        private bool AddUser()
        {
            if (User != null)
            {
                if (!Config.OnlinePlayers.ContainsKey(User.Userame))
                {
                    if (Task != null)
                    {
                        Config.OnlinePlayers.AddOrUpdate(User.Userame, Task, (key, value) => value);
                        ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Userame + " 已添加");
                        return true;
                    }
                }
                else
                {
                    KickUser();
                }
            }
            return false;
        }

        private bool RemoveUser()
        {
            if (Task != null && User != null)
            {
                if (Config.OnlinePlayers.TryRemove(User.Userame, out Task))
                {
                    ServerHelper.WriteLine("OnlinePlayers: 玩家 " + User.Userame + " 已移除");
                    Task = null;
                    User = null;
                    return true;
                }
                else ServerHelper.WriteLine("OnlinePlayers: 移除玩家 " + User.Userame + " 失败");
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
            ServerHelper.WriteLine("Creating: StreamReader...OK");
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
                            ServerHelper.WriteLine("ERROR -> Too Many Faileds.");
                            ServerHelper.WriteLine("CLOSE -> StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (FailedTimes - 1 >= 0) FailedTimes--;
                }
                else
                {
                    RemoveUser();
                    GetUserCount();
                    ServerHelper.WriteLine("ERROR -> Socket is Closed.");
                    ServerHelper.WriteLine("CLOSE -> StringStream is Closed.");
                    break;
                }
            }
        }
    }

    public class SocketHelper
    {
        public static int GetType(string msg)
        {
            int index = msg.IndexOf(';') - 1;
            if (index > 0)
                return Convert.ToInt32(msg[..index]);
            else
                return Convert.ToInt32(msg[..1]);
        }

        public static string GetMessage(string msg)
        {
            int index = msg.IndexOf(';') + 1;
            return msg[index..];
        }

        public static string MakeMessage(int type, string msg)
        {
            return type + ";" + msg;
        }
    }
}
