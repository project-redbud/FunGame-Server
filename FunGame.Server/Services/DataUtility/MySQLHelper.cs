using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Models;
using MySql.Data.MySqlClient;

namespace Milimoe.FunGame.Server.Services.DataUtility
{
    public class MySQLHelper : SQLHelper
    {
        public override FunGameInfo.FunGame FunGameType { get; } = FunGameInfo.FunGame.FunGame_Server;
        public override SQLMode Mode { get; } = SQLMode.MySQL;
        public override string Script { get; set; } = "";
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override SQLResult Result => _result;
        public override SQLServerInfo ServerInfo => _serverInfo ?? SQLServerInfo.Create();
        public override int UpdateRows => _updateRows;
        public override DataSet DataSet => _dataSet;
        public override Dictionary<string, object> Parameters { get; } = [];

        private readonly string _connectionString = "";
        private MySqlConnection? _connection;
        private MySqlTransaction? _transaction;
        private DataSet _dataSet = new();
        private SQLResult _result = SQLResult.NotFound;
        private readonly SQLServerInfo? _serverInfo;
        private int _updateRows = 0;

        public MySQLHelper(string script = "", CommandType type = CommandType.Text)
        {
            Script = script;
            CommandType = type;
            _connectionString = ConnectProperties.GetConnectPropertiesForMySQL();
            string[] strings = _connectionString.Split(";");
            if (strings.Length > 1 && strings[0].Length > 14 && strings[1].Length > 8)
            {
                string ip = strings[0][14..];
                string port = strings[1][8..];
                _serverInfo = SQLServerInfo.Create(ip: ip, port: port);
            }
        }

        /// <summary>
        /// 打开数据库连接
        /// </summary>
        private void OpenConnection()
        {
            _connection ??= new MySqlConnection(_connectionString);
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public override void Close()
        {
            _transaction?.Dispose();
            _transaction = null;
            if (_connection?.State != ConnectionState.Closed)
            {
                _connection?.Close();
            }
            _connection?.Dispose();
            _connection = null;
        }

        /// <summary>
        /// 执行一个命令
        /// </summary>
        /// <returns></returns>
        public override int Execute()
        {
            return Execute(Script);
        }

        /// <summary>
        /// 执行一个指定的命令
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public override int Execute(string script)
        {
            bool localTransaction = _transaction == null;

            try
            {
                if (localTransaction)
                {
                    NewTransaction();
                }

                OpenConnection();
                Script = script;
                ServerHelper.WriteLine("SQLQuery -> " + script, InvokeMessageType.Api);
                using MySqlCommand command = new(script, _connection);
                command.CommandType = CommandType;
                foreach (KeyValuePair<string, object> param in Parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
                if (_transaction != null) command.Transaction = _transaction;

                _updateRows = command.ExecuteNonQuery();
                _result = SQLResult.Success;
                if (localTransaction) Commit();
            }
            catch (Exception e)
            {
                if (localTransaction) Rollback();
                _result = SQLResult.Fail;
                ServerHelper.Error(e);
            }
            finally
            {
                if (localTransaction) Close();
                if (ClearParametersAfterExecute) Parameters.Clear();
            }
            return UpdateRows;
        }

        /// <summary>
        /// 查询DataSet
        /// </summary>
        /// <returns></returns>
        public override DataSet ExecuteDataSet()
        {
            return ExecuteDataSet(Script);
        }

        /// <summary>
        /// 执行指定的命令查询DataSet
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public override DataSet ExecuteDataSet(string script)
        {
            bool localTransaction = _transaction == null;

            try
            {
                if (localTransaction)
                {
                    NewTransaction();
                }

                OpenConnection();
                Script = script;
                ServerHelper.WriteLine("SQLQuery -> " + script, InvokeMessageType.Api);

                using MySqlCommand command = new(script, _connection)
                {
                    CommandType = CommandType
                };
                foreach (KeyValuePair<string, object> param in Parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
                if (_transaction != null) command.Transaction = _transaction;

                MySqlDataAdapter adapter = new()
                {
                    SelectCommand = command
                };
                _dataSet = new();
                adapter.Fill(_dataSet);

                if (localTransaction) Commit();

                _result = _dataSet.Tables.Cast<DataTable>().Any(table => table.Rows.Count > 0) ? SQLResult.Success : SQLResult.NotFound;
            }
            catch (Exception e)
            {
                if (localTransaction) Rollback();
                _result = SQLResult.Fail;
                ServerHelper.Error(e);
            }
            finally
            {
                if (localTransaction) Close();
                if (ClearParametersAfterExecute) Parameters.Clear();
            }
            return _dataSet;
        }

        /// <summary>
        /// 检查数据库是否存在
        /// </summary>
        /// <returns></returns>
        public override bool DatabaseExists()
        {
            try
            {
                ExecuteDataSet(Core.Library.SQLScript.Common.Configs.Select_GetConfig(this, "Initialization"));
                return Success;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
                _result = SQLResult.Fail;
                return false;
            }
            finally
            {
                Close();
            }
        }

        /// <summary>
        /// 创建一个SQL事务
        /// </summary>
        public override void NewTransaction()
        {
            OpenConnection();
            if (_connection != null)
            {
                _transaction = _connection.BeginTransaction();
            }
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public override void Commit()
        {
            try
            {
                _transaction?.Commit();
                _result = SQLResult.Success;
            }
            catch (Exception e)
            {
                _result = SQLResult.Fail;
                ServerHelper.Error(e);
            }
            finally
            {
                _transaction = null;
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        public override void Rollback()
        {
            try
            {
                _transaction?.Rollback();
                _result = SQLResult.SQLError;
            }
            catch (Exception e)
            {
                _result = SQLResult.Fail;
                ServerHelper.Error(e);
            }
            finally
            {
                _transaction = null;
            }
        }

        private bool _isDisposed = false;

        /// <summary>
        /// 资源清理
        /// </summary>
        public void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
                    _transaction = null;
                    _connection?.Close();
                    _connection?.Dispose();
                    _connection = null;
                }
            }
            _isDisposed = true;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
