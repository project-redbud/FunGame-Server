using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunGameServer.Models.Config
{
    public static class SocketEnums
    {
        public enum SendType
        {
            GetNotice = 1,
            Login = 2,
            CheckLogin = 3,
            Logout = 4,
            HeartBeat = 5
        }

        public enum ReadType
        {
            GetNotice = 1,
            Login = 2,
            CheckLogin = 3,
            Logout = 4,
            HeartBeat = 5
        }

        public static string SENDTYPE_GetNotice = "GetNotice";
        public static string SENDTYPE_Login = "Login";
        public static string SENDTYPE_CheckLogin = "CheckLogin";
        public static string SENDTYPE_Logout = "Logout";
        public static string SENDTYPE_HeartBeat = "HeartBeat";
    }
}
