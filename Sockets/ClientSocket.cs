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
using FunGame.Models.Entity;

namespace FunGameServer.Sockets
{
    public class ClientSocket
    {
        public bool Running { get; set; } = false;
        public Socket? Socket { get; set; } = null;

        private User? user = null;

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
                    string typestring = SocketHelper.GetTypeString(type);
                    msg = SocketHelper.GetMessage(msg);
                    Console.WriteLine(SocketHelper.GetPrefix() + "[ 客户端（" + typestring + "）] -> " + msg);
                    switch (type)
                    {
                        case (int)SocketEnums.Type.GetNotice:
                            break;
                        case (int)SocketEnums.Type.Login:
                            break;
                        case (int)SocketEnums.Type.CheckLogin:
                            return true;
                        case (int)SocketEnums.Type.Logout:
                            break;
                        case (int)SocketEnums.Type.HeartBeat:
                            msg = "";
                            break;
                    }
                    return Send(socket, type, msg);
                }
                throw new Exception();
            }
            catch (Exception e)
            {
                Console.WriteLine(SocketHelper.GetPrefix() + "ERROR：客户端没有回应。\n" + e.StackTrace);
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
                string typestring = SocketHelper.GetTypeString(type);
                if (socket.Send(buffer) > 0)
                {
                    if (msg != "")
                        Console.WriteLine(SocketHelper.GetPrefix() + "[ 客户端（" + typestring + "）] <- " + msg);
                    else
                        Console.WriteLine(SocketHelper.GetPrefix() + "-> [ 客户端（" + typestring + "）]");
                    return true;
                }
                throw new Exception();
            }
            catch (Exception e)
            {
                Console.WriteLine(SocketHelper.GetPrefix() + "ERROR：客户端没有回应。" + e.StackTrace);
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

        private void CreateStreamReader()
        {
            Thread.Sleep(1000);
            Console.WriteLine(SocketHelper.GetPrefix() + "Creating: StreamReader...OK");
            while (Running)
            {
                if (Socket != null)
                {
                    if (!Read(Socket))
                    {
                        FailedTimes++;
                        if (FailedTimes >= Config.MAX_CONNECTFAILED)
                        {
                            Console.WriteLine(SocketHelper.GetPrefix() + "ERROR: Too Many Faileds.");
                            Console.WriteLine(SocketHelper.GetPrefix() + "CLOSE: StreamReader is Closed.");
                            break;
                        }
                    }
                    else if (FailedTimes - 1 >= 0) FailedTimes--;
                }
                else
                {
                    Console.WriteLine(SocketHelper.GetPrefix() + "ERROR: Socket is Closed.");
                    Console.WriteLine(SocketHelper.GetPrefix() + "CLOSE: StringStream is Closed.");
                    break;
                }
            }
        }
    }
}
