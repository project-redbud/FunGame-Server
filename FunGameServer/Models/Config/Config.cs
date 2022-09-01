using FunGame.Core.Api.Util;
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
        public static int MAX_PLAYERS = 20; // 最多接受连接的玩家数量
        public static int ONLINE_PLAYERS = 0; // 已连接的玩家数量
        public static int CONNECTING_PLAYERS = 0; // 正在连接的玩家数量
        public static string SERVER_NAME = "米粒的糖果屋"; // 服务器名称
        public static int SERVER_PORT = 22222; // 默认端口
        public static Encoding DEFAULT_ENCODING = Encoding.UTF8; // 默认传输字符集
        public static int MAX_CONNECTFAILED = 5; // 最大连接失败次数
        public const string CONSOLE_TITLE = "FunGame Server"; // 控制台的标题
        public static string ServerNotice = ""; // 服务器的公告

        public static AssemblyHelper DefaultAssemblyHelper = new AssemblyHelper();

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
