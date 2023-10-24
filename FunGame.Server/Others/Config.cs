using System.Collections;
using System.Data;
using System.Text;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Utility;

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
        public static string ServerBannedList { get; set; } = ""; // 禁止连接的黑名单
        public static int MaxPlayers { get; set; } = 20; // 最多接受连接的玩家数量
        public static int MaxConnectionFaileds { get; set; } = 5; // 最大连接失败次数
        public static int OnlinePlayerCount { get; set; } = 0; // 已连接的玩家数量
        public static int ConnectingPlayerCount { get; set; } = 0; // 正在连接的玩家数量
        public static Encoding DefaultEncoding { get; } = General.DefaultEncoding; // 默认传输字符集
        public static FunGameInfo.FunGame FunGameType { get; } = FunGameInfo.FunGame.FunGame_Server; // FunGame Runtime
        public static Hashtable OrderList { get; } = new(); // 服务器指令列表
        public static RoomList RoomList { get; } = new(); // 在线房间列表
        public static bool SQLMode { get; set; } = false; // 是否运行数据库模式
        public static SQLHelper SQLHelper
        {
            // 全局数据库连接器
            get
            {
                if (_SQLHelper is null) throw new MySQLConfigException();
                return _SQLHelper;
            }
        }

        private static SQLHelper? _SQLHelper;

        public static void InitSQLHelper()
        {
            try
            {
                _SQLHelper = new MySQLHelper("", false);
                if (((MySQLHelper)_SQLHelper).Connection != null)
                {
                    SQLMode = true;
                    ServerLogin();
                    InitRoomList();
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        public static void ServerLogin()
        {
            if (SQLMode)
            {
                SQLHelper.Execute(ServerLoginLogs.Insert_ServerLoginLogs(Config.ServerName, Config.ServerKey));
            }
        }

        public static void InitRoomList()
        {
            if (SQLMode)
            {
                // 初始化服务器中的房间列表
                DataSet DsRoomTemp = SQLHelper.ExecuteDataSet(RoomQuery.Select_Rooms);
                DataSet DsUserTemp = SQLHelper.ExecuteDataSet(UserQuery.Select_Users);
                List<Room> rooms = Factory.GetRooms(DsRoomTemp, DsUserTemp);
                RoomList.AddRooms(rooms);
            }
        }
    }

    public static class OfficialEmail
    {
        public static string Email { get; set; } = "";
        public static string SupportEmail { get; set; } = "";
    }
}
