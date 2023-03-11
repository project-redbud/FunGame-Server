using System.Data;
using MySql.Data.MySqlClient;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.Server;
using Milimoe.FunGame.Server.Utility.DataUtility;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Model;

namespace Milimoe.FunGame.Server.Utility
{
    public class MySQLHelper : SQLHelper
    {
        public override FunGameInfo.FunGame FunGameType => Config.FunGameType;
        public override string Script { get; set; } = "";
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override SQLResult Result => _Result;
        public override SQLServerInfo ServerInfo => _ServerInfo ?? SQLServerInfo.Create();
        public override int UpdateRows => _UpdateRows;
        public override DataSet DataSet => _DataSet;
        public MySqlParameter[] Parameters { get; set; }
        public MySQLConnection? Connection => _Connection;

        private SQLResult _Result = SQLResult.Success;
        private SQLServerInfo? _ServerInfo;
        private int _UpdateRows = 0;
        private DataSet _DataSet = new();
        private MySQLConnection? _Connection;
        private readonly ServerModel? ServerModel;
        private readonly bool _IsOneTime = false;

        /// <summary>
        /// 执行一个命令
        /// </summary>
        /// <param name="Result">执行结果</param>
        /// <returns>影响的行数</returns>
        public override int Execute(out SQLResult Result)
        {
            // _IsOneTime = true需要手动创建连接和关闭
            if (_IsOneTime) _Connection = new MySQLConnection(out _ServerInfo);
            ServerHelper.WriteLine("SQLQuery -> " + Script);
            _DataSet = new DataSet();
            _UpdateRows = MySQLManager.Execute(this, out Result);
            _Result = Result;
            if (_IsOneTime) Close();
            return _UpdateRows;
        }

        /// <summary>
        /// 执行一个指定的命令
        /// </summary>
        /// <param name="Script">命令</param>
        /// <param name="Result">执行结果</param>
        /// <returns>影响的行数</returns>
        public override int Execute(string Script, out SQLResult Result)
        {
            // _IsOneTime = true需要手动创建连接和关闭
            if (_IsOneTime) _Connection = new MySQLConnection(out _ServerInfo);
            ServerHelper.WriteLine("SQLQuery -> " + Script);
            this.Script = Script;
            _DataSet = new DataSet();
            _UpdateRows = MySQLManager.Execute(this, out Result);
            _Result = Result;
            if (_IsOneTime) Close();
            return _UpdateRows;
        }

        /// <summary>
        /// 查询DataSet
        /// </summary>
        /// <param name="Result">执行结果</param>
        /// <returns>结果集</returns>
        public override DataSet ExecuteDataSet(out SQLResult Result)
        {
            // _IsOneTime = true需要手动创建连接和关闭
            if (_IsOneTime) _Connection = new MySQLConnection(out _ServerInfo);
            ServerHelper.WriteLine("SQLQuery -> " + Script);
            _DataSet = MySQLManager.ExecuteDataSet(this, out Result, out _UpdateRows);
            _Result = Result;
            if (_IsOneTime) Close();
            return DataSet;
        }
        
        /// <summary>
        /// 执行指定的命令查询DataSet
        /// </summary>
        /// <param name="Script">命令</param>
        /// <param name="Result">执行结果</param>
        /// <returns>结果集</returns>
        public override DataSet ExecuteDataSet(string Script, out SQLResult Result)
        {
            // _IsOneTime = true需要手动创建连接和关闭
            if (_IsOneTime) _Connection = new MySQLConnection(out _ServerInfo);
            ServerHelper.WriteLine("SQLQuery -> " + Script);
            this.Script = Script;
            _DataSet = MySQLManager.ExecuteDataSet(this, out Result, out _UpdateRows);
            _Result = Result;
            if (_IsOneTime) Close();
            return DataSet;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void Close()
        {
            // _IsOneTime = false需要手动调用此方法
            _Connection?.Close();
            ServerHelper.WriteLine($"{GetClientName()}已释放MySQL连接");
        }

        /// <summary>
        /// 创建单次使用的SQLHelper(执行完毕会自动Close连接)
        /// </summary>
        /// <param name="script">存储过程名称或者script语句</param> 
        /// <param name="type">存储过程, 文本, 等等</param> 
        /// <param name="parameters">执行命令所用参数的集合</param> 
        public MySQLHelper(string script = "", CommandType type = CommandType.Text, params MySqlParameter[] parameters)
        {
            _IsOneTime = true;
            Script = script;
            CommandType = type;
            Parameters = parameters;
        }

        /// <summary>
        /// 创建为SocketModel服务的SQLHelper
        /// </summary>
        /// <param name="ServerModel">SocketModel</param>
        public MySQLHelper(ServerModel ServerModel)
        {
            this.ServerModel = ServerModel;
            Script = "";
            CommandType = CommandType.Text;
            Parameters = Array.Empty<MySqlParameter>();
            _Connection = new MySQLConnection(out _ServerInfo);
        }

        private string GetClientName()
        {
            if (ServerModel is null) return "";
            return SocketHelper.MakeClientName(ServerModel.ClientName, ServerModel.User) + " ";
        }
    }
}
