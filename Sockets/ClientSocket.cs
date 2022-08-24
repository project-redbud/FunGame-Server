using FunGameServer.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FunGameServer.Sockets
{
    public class ClientSocket
    {
        public bool Running { get; set; } = false;
        public Socket? Socket { get; set; } = null;

        public ClientSocket(Socket socket, bool running)
        {
            Socket = socket;
            Running = running;
        }

        bool Read(Socket socket)
        {
            // 接收客户端消息
            byte[] buffer = new byte[2048];
            int length = socket.Receive(buffer);
            if (length > 0)
            {
                string type = Encoding.GetEncoding("unicode").GetString(buffer, 0, length);
                Console.Write("收到来自：客户端（" + type + "） -> ");
                buffer = new byte[2048];
                length = socket.Receive(buffer);
                if (length > 0)
                {
                    string msg = Encoding.GetEncoding("unicode").GetString(buffer, 0, length);
                    Console.WriteLine(msg);
                    int getType = Convert.ToInt32(type);
                    if (getType == (int)SocketEnums.ReadType.HeartBeat) // 检测心跳包
                        Send(socket, getType, msg);
                    return true;
                }
                else
                    Console.WriteLine("客户端没有回应。");
                return false;
            }
            else
                Console.WriteLine("客户端没有回应。");
            return false;
        }

        bool Send(Socket socket, int type, string msg)
        {
            // 发送消息给客户端
            byte[] buffer = new byte[2048];
            buffer = Encoding.GetEncoding("unicode").GetBytes(Convert.ToString(type));
            if (socket.Send(buffer) > 0)
            {
                Console.Write("发送给：客户端（" + type + "） <- ");
                buffer = new byte[2048];
                buffer = Encoding.GetEncoding("unicode").GetBytes(msg);
                if (socket.Send(buffer) > 0)
                {
                    Console.WriteLine("发送给：客户端（" + msg + "） <- ");
                    return true;
                }
                else
                Console.WriteLine("无法传输数据，与客户端的连接可能丢失。");
                return false;
            }
            else
                Console.WriteLine("无法传输数据，与客户端的连接可能丢失。");
            return false;
        }

        public void Start()
        {
            Task StringStream = Task.Factory.StartNew(() =>
            {
                CreateStringStream();
            });
            Task IntStream = Task.Factory.StartNew(() =>
            {
                CreateStringStream();
            });
            Task DecimalStream = Task.Factory.StartNew(() =>
            {
                CreateDecimalStream();
            });
            Task ObjectStream = Task.Factory.StartNew(() =>
            {
                CreateObjectStream();
            });
        }

        private void CreateStringStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: StringStream...OK");
            while (Running)
            {
                if (Socket != null)
                    Read(Socket);
                else
                {
                    Console.WriteLine("ERROR: Socket is Closed.");
                    Console.WriteLine("DONE: StringStream is Closed.");
                    break;
                }
            }
        }

        private void CreateIntStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: IntStream...OK");
            while (Running)
            {
                Console.WriteLine("DONE: IntStream is Closed.");
                break;
            }
        }
        
        private void CreateDecimalStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: DecimalStream...OK");
            while (Running)
            {
                Console.WriteLine("DONE: DecimalStream is Closed.");
                break;
            }
        }
        
        private void CreateObjectStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: ObjectStream...OK");
            while (Running)
            {
                Console.WriteLine("DONE: ObjectStream is Closed.");
                break;
            }
        }
    }
}
