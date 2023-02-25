using System.Data;
using MySql.Data.MySqlClient;
using Milimoe.FunGame.Core.Api.Data;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Utility.DataUtility;

namespace Milimoe.FunGame.Server.Utility
{
    internal class MySQLHelper : SQLHelper
    {
        public new string Script { get; set; } = "";
        public new EntityType EntityType => _EntityType;
        public new object Entity => _Entity;
        public new SQLResult Result => _Result;
        public override SQLServerInfo ServerInfo
        {
            get
            {
                return _ServerInfo ?? SQLServerInfo.Create();
            }
        }
        public new int UpdateRows => _UpdateRows;
        public DataSet DataSet => _DataSet;

        private EntityType _EntityType;
        private object _Entity = General.EntityInstance;
        private SQLResult _Result;
        private SQLServerInfo? _ServerInfo;
        private int _UpdateRows = 0;
        private DataSet _DataSet = new DataSet();

        public static SQLHelper GetHelper()
        {
            return new MySQLHelper();
        }

        public override SQLResult Execute()
        {
            return SQLResult.NotFound;
        }

        public DataSet ExecuteDataSet()
        {
            return _DataSet;
        }

        /// <summary> 
        /// 执行一个sql命令
        /// </summary> 
        /// <param name="type">存储过程, 文本, 等等</param> 
        /// <param name="sql">存储过程名称或者sql语句</param> 
        /// <param name="parameters">执行命令的参数</param> 
        /// <returns>执行命令所影响的行数</returns> 
        private int Execute(CommandType type, string sql, params MySqlParameter[] parameters)
        {
            MySqlCommand cmd = new();

            PrepareCommand(cmd, null, type, sql, parameters);

            int updaterow = cmd.ExecuteNonQuery();
            return updaterow;
        }

        /// <summary> 
        /// 返回DataSet 
        /// </summary> 
        /// <param name="type">存储过程, 文本, 等等</param> 
        /// <param name="sql">存储过程名称或者sql语句</param> 
        /// <param name="parameters">执行命令所用参数的集合</param> 
        /// <returns></returns> 
        private DataSet ExecuteDataSet(CommandType type, string sql, params MySqlParameter[] parameters)
        {
            MySqlCommand cmd = new();
            DataSet ds = new();

            try
            {
                PrepareCommand(cmd, null, type, sql, parameters);

                MySqlDataAdapter adapter = new()
                {
                    SelectCommand = cmd
                };
                adapter.Fill(ds);

                //清 除参数
                cmd.Parameters.Clear();
                return ds;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }

            return ds;
        }

        /// <summary>
        /// 返回插入值ID
        /// </summary>
        /// <param name="type">存储过程, 文本, 等等</param> 
        /// <param name="sql">存储过程名称或者sql语句</param> 
        /// <param name="parameters">执行命令所用参数的集合</param> 
        /// <returns></returns>
        private object ExecuteNonExist(CommandType type, string sql, params MySqlParameter[] parameters)
        {
            MySqlCommand cmd = new();

            PrepareCommand(cmd, null, type, sql, parameters);
            cmd.ExecuteNonQuery();

            return cmd.LastInsertedId;
        }

        /// <summary> 
        /// 准备执行一个命令 
        /// </summary> 
        /// <param name="cmd">命令</param> 
        /// <param name="trans">事务</param> 
        /// <param name="type">存储过程或者文本</param> 
        /// <param name="sql">sql语句</param> 
        /// <param name="parameters">执行命令的参数</param> 
        private void PrepareCommand(MySqlCommand cmd, MySqlTransaction? trans, CommandType type, string sql, MySqlParameter[] parameters)
        {
            MySqlConnection? conn = MySQLConnection.Connection;
            if (conn != null && conn.State != ConnectionState.Open)
                conn.Open();

            cmd.Connection = conn;
            cmd.CommandText = sql;

            if (trans != null)
            {
                cmd.Transaction = trans;
            }

            cmd.CommandType = type;

            if (parameters != null)
            {
                foreach (MySqlParameter parm in parameters)
                {
                    cmd.Parameters.Add(parm);
                }
            }
        }

        /// <summary>
        /// 创建SQLHelper
        /// </summary>
        /// <param name="type">获取实体类的类型</param>
        /// <param name="info">SQL服务器信息</param>
        /// <param name="result">执行结果</param>
        /// <param name="rows">更新的行数</param>
        private MySQLHelper(EntityType type = EntityType.Empty, SQLServerInfo? info = null, SQLResult result = SQLResult.Success, int rows = 0)
        {
            _EntityType = type;
            if (info == null) _ServerInfo = SQLServerInfo.Create();
            else _ServerInfo = info;
            _Result = result;
            _UpdateRows = rows;
        }
    }
}
