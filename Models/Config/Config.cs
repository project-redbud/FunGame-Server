using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace FunGameServer.Models.Config
{
    public static class Config
    {
        public static int MAX_PLAYERS = 16; // 最多接受连接的玩家数量
        public static int ONLINE_PLAYERS = 0; // 已连接的玩家数量
        public static int CONNECTING_PLAYERS = 0; // 正在连接的玩家数量
        public static string SERVER_IPADRESS = "127.0.0.1"; // 默认IP地址
        public static int SERVER_PORT = 22222; // 默认端口

        /// <summary>
        /// string: 玩家标识ID
        /// Task：玩家线程
        /// </summary>
        public static ConcurrentDictionary<string, Task> OnlinePlayers = new ConcurrentDictionary<string, Task>();

        /**
         * string：房间号
         * string：玩家标识ID
         * Task：玩家线程
         */
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, Task>> PlayingPlayers= new ConcurrentDictionary<string, ConcurrentDictionary<string, Task>>();

    }
}
