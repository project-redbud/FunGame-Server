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
        public static string SERVER_NAME { get; set; } = "FunGame Server"; // 服务器名称
        public static int SERVER_PORT { get; set; } = 22222; // 默认端口
        public static int SERVER_STATUS { get; set; } = 1; // 默认状态：1可连接 0不可连接 -1不可用
        public static string SERVER_NOTICE { get; set; } = ""; // 服务器的公告
        public static string SERVER_PASSWORD { get; set; } = ""; // 服务器的密码
        public static string SERVER_DESCRIBE { get; set; } = ""; // 服务器的描述
        public static string SERVER_KEY { get; set; } = ""; // 注册社区服务器的Key
        public static int MAX_PLAYERS { get; set; } = 20; // 最多接受连接的玩家数量
        public static int MAX_CONNECTFAILED { get; set; } = 5; // 最大连接失败次数
        public static int ONLINE_PLAYERS { get; set; } = 0; // 已连接的玩家数量
        public static int CONNECTING_PLAYERS { get; set; } = 0; // 正在连接的玩家数量
        public static Encoding DEFAULT_ENCODING { get; } = General.DEFAULT_ENCODING; // 默认传输字符集
        public static int FunGameType { get; } = (int)FunGameEnum.FunGame.FunGame_Server;

        public static Hashtable OrderList { get; } = new();

        public static Hashtable OnlineClients { get; } = new Hashtable();

        /// <summary>
        /// string: 玩家标识ID
        /// Task：玩家线程
        /// </summary>
        public static ConcurrentDictionary<string, Task> OnlinePlayers { get; } = new ConcurrentDictionary<string, Task>();

        /**
         * string：房间号
         * Task：玩家线程
         */
        public static ConcurrentDictionary<string, Task> PlayingPlayers { get; } = new ConcurrentDictionary<string, Task>();

    }
}
