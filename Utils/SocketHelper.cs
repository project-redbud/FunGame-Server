using FunGameServer.Models.Config;
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

        public static string GetTypeString(int type)
        {
            switch (type)
            {
                case (int)SocketEnums.Type.GetNotice:
                    return SocketEnums.TYPE_GetNotice;
                case (int)SocketEnums.Type.Login:
                    return SocketEnums.TYPE_Login;
                case (int)SocketEnums.Type.CheckLogin:
                    return SocketEnums.TYPE_CheckLogin;
                case (int)SocketEnums.Type.Logout:
                    return SocketEnums.TYPE_Logout;
                case (int)SocketEnums.Type.HeartBeat:
                    return SocketEnums.TYPE_HeartBeat;
                default:
                    return SocketEnums.TYPE_UNKNOWN;
            }
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

        public static string GetPrefix()
        {
            DateTime now = System.DateTime.Now;
            return now.AddMilliseconds(-now.Millisecond).ToString() + " " + Config.SERVER_NAME + "：";
        }
    }
}
