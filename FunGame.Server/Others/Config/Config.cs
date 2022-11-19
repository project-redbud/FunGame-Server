using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.Server.Others
{
    public static class Config
    {
        public static string SERVER_NAME = "FunGame Server"; // 服务器名称
        public static int SERVER_PORT = 22222; // 默认端口
        public static int SERVER_STATUS = 1; // 默认状态：1可连接 0不可连接 -1不可用
        public static string SERVER_NOTICE = ""; // 服务器的公告
        public static string SERVER_PASSWORD = ""; // 服务器的密码
        public static string SERVER_DESCRIBE = ""; // 服务器的描述
        public static string SERVER_KEY = ""; // 注册社区服务器的Key
        public static int MAX_PLAYERS = 20; // 最多接受连接的玩家数量
        public static int MAX_CONNECTFAILED = 5; // 最大连接失败次数
        public static int ONLINE_PLAYERS = 0; // 已连接的玩家数量
        public static int CONNECTING_PLAYERS = 0; // 正在连接的玩家数量
        public static Encoding DEFAULT_ENCODING = Encoding.UTF8; // 默认传输字符集
        public static FunGameEnum.FunGame FunGameType = FunGameEnum.FunGame.FunGame_Server;

        public static Hashtable OrderList = new();

        public static Hashtable OnlineClients = new Hashtable();

        /// <summary>
        /// string: 玩家标识ID
        /// Task：玩家线程
        /// </summary>
        public static ConcurrentDictionary<string, Task> OnlinePlayers = new ConcurrentDictionary<string, Task>();

        /**
         * string：房间号
         * Task：玩家线程
         */
        public static ConcurrentDictionary<string, Task> PlayingPlayers= new ConcurrentDictionary<string, Task>();

    }
}
