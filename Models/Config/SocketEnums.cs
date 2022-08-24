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
            Login = 1,
            CheckLogin = 2,
            Logout = 3,
            HeartBeat = 4,
        }

        public enum ReadType
        {
            Login = 1,
            CheckLogin = 2,
            Logout = 3,
            HeartBeat = 4,
        }
    }
}
