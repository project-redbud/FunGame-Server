using System;
using System.Collections.Generic;
using System.Linq;
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

            }
        }

        private void CreateIntStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: IntStream...OK");
            while (Running)
            {

            }
        }
        
        private void CreateDecimalStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: DecimalStream...OK");
            while (Running)
            {

            }
        }
        
        private void CreateObjectStream()
        {
            Thread.Sleep(1000);
            Console.WriteLine("Creating: ObjectStream...OK");
            while (Running)
            {

            }
        }
    }
}
