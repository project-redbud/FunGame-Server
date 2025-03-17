﻿using System.Text;
using Milimoe.FunGame.Core.Library.Constant;

namespace Milimoe.FunGame.Server.Others
{
    public static class Config
    {
        /// <summary>
        /// 使用 ASP.NET Core Web API
        /// </summary>
        public static bool AspNetCore { get; set; } = false;

        /// <summary>
        /// 日志级别
        /// </summary>
        public static string LogLevel { get; set; } = "INFO";

        /// <summary>
        /// 日志级别（枚举值）
        /// </summary>
        public static LogLevel LogLevelValue
        {
            get
            {
                return LogLevel.ToUpper() switch
                {
                    "TRACE" => Core.Library.Constant.LogLevel.Trace,
                    "DEBUG" => Core.Library.Constant.LogLevel.Debug,
                    "INFO" => Core.Library.Constant.LogLevel.Info,
                    "WARN" => Core.Library.Constant.LogLevel.Warning,
                    "ERROR" => Core.Library.Constant.LogLevel.Error,
                    "CRIT" => Core.Library.Constant.LogLevel.Critical,
                    _ => Core.Library.Constant.LogLevel.Info
                };
            }
        }

        /// <summary>
        /// 服务器名称
        /// </summary>
        public static string ServerName { get; set; } = "FunGame Server";

        /// <summary>
        /// Socket 端口
        /// </summary>
        public static int ServerPort { get; set; } = 22222;

        /// <summary>
        /// 使用 WebSocket
        /// </summary>
        public static bool UseWebSocket { get; set; } = false;

        /// <summary>
        /// WebSocket 监听地址
        /// </summary>
        public static string WebSocketAddress { get; set; } = "localhost";

        /// <summary>
        /// WebSocket 端口
        /// </summary>
        public static int WebSocketPort { get; set; } = 22222;

        /// <summary>
        /// WebSocket 监听子路径
        /// </summary>
        public static string WebSocketSubUrl { get; set; } = "ws";

        /// <summary>
        /// WebSocket 开启 SSL
        /// </summary>
        public static bool WebSocketSSL { get; set; } = false;

        /// <summary>
        /// 默认状态：1可连接 0不可连接 -1不可用
        /// </summary>
        public static int ServerStatus { get; set; } = 1;

        /// <summary>
        /// 服务器的公告
        /// </summary>
        public static string ServerNotice { get; set; } = "";

        /// <summary>
        /// 服务器的密码
        /// </summary>
        public static string ServerPassword { get; set; } = "";

        /// <summary>
        /// 服务器的描述
        /// </summary>
        public static string ServerDescription { get; set; } = "";

        /// <summary>
        /// 注册社区服务器的Key
        /// </summary>
        public static string ServerKey { get; set; } = "";

        /// <summary>
        /// 禁止连接的黑名单
        /// </summary>
        public static List<string> ServerBannedList { get; set; } = [];

        /// <summary>
        /// 是否使用 FunGame.Desktop 的参数检查
        /// </summary>
        public static bool UseDesktopParameters { get; set; } = true;

        /// <summary>
        /// 最多接受连接的玩家数量
        /// </summary>
        public static int MaxPlayers { get; set; } = 20;

        /// <summary>
        /// 最大连接失败次数
        /// </summary>
        public static int MaxConnectionFaileds { get; set; } = 5;

        /// <summary>
        /// 已连接的玩家数量
        /// </summary>
        public static int OnlinePlayerCount { get; set; } = 0;

        /// <summary>
        /// 正在连接的玩家数量
        /// </summary>
        public static int ConnectingPlayerCount { get; set; } = 0;

        /// <summary>
        /// 默认传输字符集
        /// </summary>
        public static Encoding DefaultEncoding { get; } = General.DefaultEncoding;

        /// <summary>
        /// FunGame Runtime
        /// </summary>
        public static FunGameInfo.FunGame FunGameType => FunGameInfo.FunGame.FunGame_Server;

        /// <summary>
        /// 运行的数据库模式
        /// </summary>
        public static SQLMode SQLMode { get; set; } = SQLMode.None;

        /// <summary>
        /// 未Loadmodules时，此属性表示至少需要安装的模组
        /// </summary>
        public static string[] GameModuleSupported { get; set; } = [];
    }

    /// <summary>
    /// 此服务器的联系邮箱
    /// </summary>
    public static class OfficialEmail
    {
        public static string Email { get; set; } = "";
        public static string SupportEmail { get; set; } = "";
    }
}
