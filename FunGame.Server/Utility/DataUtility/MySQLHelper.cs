using System.Data;
using MySql.Data.MySqlClient;
using Milimoe.FunGame.Core.Api.Data;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.Server;
using Milimoe.FunGame.Server.Utility.DataUtility;

namespace Milimoe.FunGame.Server.Utility
{
    public class MySQLHelper : SQLHelper
    {
        public override string Script { get; set; } = "";
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public MySqlParameter[] Parameters { get; set; }
        public override SQLResult Result => _Result;
        public override SQLServerInfo ServerInfo => _ServerInfo ?? SQLServerInfo.Create();
        public MySQLConnection? Connection => _Connection;
        public override int UpdateRows => _UpdateRows;
        public override DataSet DataSet => _DataSet;

        private SQLResult _Result = SQLResult.Success;
        private SQLServerInfo? _ServerInfo;
        private int _UpdateRows = 0;
        private DataSet _DataSet = new();
        private MySQLConnection? _Connection;

        /// <summary>
        /// 创建MySQLHelper实例
        /// </summary>
        /// <param name="script"></param>
        /// <param name="type"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static MySQLHelper GetHelper(string script = "", CommandType type = CommandType.Text, params MySqlParameter[] parameters)
        {
            return new MySQLHelper(script, type, parameters);
        }

        /// <summary>
        /// 执行一个命令
        /// </summary>
        /// <param name="Result">执行结果</param>
        /// <returns>影响的行数</returns>
        public override int Execute(out SQLResult Result)
        {
            _Connection = new MySQLConnection(out _ServerInfo);
            ServerHelper.WriteLine("SQLQuery -> " + Script);
            _UpdateRows = MySQLManager.Execute(this, out Result);
            Close();
            return _UpdateRows;
        }

        /// <summary>
        /// 查询DataSet
        /// </summary>
        /// <param name="Result">执行结果</param>
        /// <returns>结果集</returns>
        public override DataSet ExecuteDataSet(out SQLResult Result)
        {
            _Connection = new MySQLConnection(out _ServerInfo);
            ServerHelper.WriteLine("SQLQuery -> " + Script);
            _DataSet = MySQLManager.ExecuteDataSet(this, out Result);
            _UpdateRows = _DataSet.Tables[0].Rows.Count;
            Close();
            return DataSet;
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public override void Close()
        {
            _Connection?.Close();
            ServerHelper.WriteLine("Connection Release");
        }

        /// <summary>
        /// 创建SQLHelper
        /// </summary>
        /// <param name="script">存储过程名称或者script语句</param> 
        /// <param name="type">存储过程, 文本, 等等</param> 
        /// <param name="parameters">执行命令所用参数的集合</param> 
        private MySQLHelper(string script, CommandType type, params MySqlParameter[] parameters)
        {
            Script = script;
            CommandType = type;
            Parameters = parameters;
        }
    }
}
