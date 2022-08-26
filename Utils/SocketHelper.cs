using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunGameServer.Utils
{
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
