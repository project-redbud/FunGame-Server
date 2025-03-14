using System.Data;
using Microsoft.Data.Sqlite;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Models;

namespace Milimoe.FunGame.Server.Services.DataUtility
{
    public class SQLiteHelper : SQLHelper
    {
        public override FunGameInfo.FunGame FunGameType { get; } = FunGameInfo.FunGame.FunGame_Server;
        public override SQLMode Mode { get; } = SQLMode.SQLite;
        public override string Script { get; set; } = "";
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override SQLResult Result => _result;
        public override SQLServerInfo ServerInfo => _serverInfo ?? SQLServerInfo.Create();
        public override int UpdateRows => _updateRows;
        public override DataSet DataSet => _dataSet;
        public override Dictionary<string, object> Parameters { get; } = [];

        private readonly string _connectionString = "";
        private SqliteConnection? _connection;
        private SqliteTransaction? _transaction;
        private DataSet _dataSet = new();
        private SQLResult _result = SQLResult.NotFound;
        private readonly SQLServerInfo? _serverInfo;
        private int _updateRows = 0;

        public SQLiteHelper(string script = "", CommandType type = CommandType.Text)
        {
            Script = script;
            CommandType = type;
            _connectionString = ConnectProperties.GetConnectPropertiesForSQLite();
            string[] strings = _connectionString.Split("=");
            if (strings.Length > 1)
            {
                _serverInfo = SQLServerInfo.Create(database: strings[1]);
            }
        }

        /// <summary>
        /// 打开数据库连接
        /// </summary>
        private void OpenConnection()
        {
            _connection ??= new SqliteConnection(_connectionString);
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
                using SqliteCommand command = new(script, _connection);
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
                using SqliteCommand command = new(script, _connection)
                {
                    CommandType = CommandType
                };
                foreach (KeyValuePair<string, object> param in Parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
                if (_transaction != null) command.Transaction = _transaction;

                using SqliteDataReader reader = command.ExecuteReader();
                _dataSet = new();
                do
                {
                    DataTable table = new();
                    table.Load(reader);
                    _dataSet.Tables.Add(table);
                } while (!reader.IsClosed && reader.NextResult());

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

        /// <summary>
        /// 检查数据库是否存在
        /// </summary>
        /// <returns></returns>
        public override bool DatabaseExists()
        {
            return File.Exists(ServerInfo.SQLServerDataBase);
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
