using System.Text;
using System.Collections;
using Milimoe.FunGame.Core.Library.Constant;

namespace Milimoe.FunGame.Server.Others
{
    public static class Config
    {
        public static string ServerName { get; set; } = "FunGame Server"; // 服务器名称
        public static int ServerPort { get; set; } = 22222; // 默认端口
        public static int ServerStatus { get; set; } = 1; // 默认状态：1可连接 0不可连接 -1不可用
        public static string ServerNotice { get; set; } = ""; // 服务器的公告
        public static string ServerPassword { get; set; } = ""; // 服务器的密码
        public static string ServerDescription { get; set; } = ""; // 服务器的描述
        public static string ServerKey { get; set; } = ""; // 注册社区服务器的Key
        public static int MaxPlayers { get; set; } = 20; // 最多接受连接的玩家数量
        public static int MaxConnectionFaileds { get; set; } = 5; // 最大连接失败次数
        public static int OnlinePlayersCount { get; set; } = 0; // 已连接的玩家数量
        public static int ConnectingPlayersCount { get; set; } = 0; // 正在连接的玩家数量
        public static Encoding DefaultEncoding { get; } = General.DefaultEncoding; // 默认传输字符集
        public static FunGameInfo.FunGame FunGameType { get; } = FunGameInfo.FunGame.FunGame_Server;
        public static Hashtable OrderList { get; } = new();
        public static Hashtable OnlineClients { get; } = new Hashtable();
    }

    public static class OfficialEmail
    {
        public static string Email { get; set; } = "";
        public static string SupportEmail { get; set; } = "";
    }
}
