using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Models;

namespace Milimoe.FunGame.Server.Services.DataUtility
{
    public class SQLiteHelper : SQLHelper
    {
        /// <summary>
        /// FunGame 类型
        /// </summary>
        public override FunGameInfo.FunGame FunGameType { get; } = FunGameInfo.FunGame.FunGame_Server;

        /// <summary>
        /// 使用的数据库类型
        /// </summary>
        public override SQLMode Mode { get; } = SQLMode.SQLite;

        /// <summary>
        /// SQL 脚本
        /// </summary>
        public override string Script { get; set; } = "";

        /// <summary>
        /// 命令类型
        /// </summary>
        public override CommandType CommandType { get; set; } = CommandType.Text;

        /// <summary>
        /// 数据库事务
        /// </summary>
        public override DbTransaction? Transaction => _transaction;

        /// <summary>
        /// 执行结果
        /// </summary>
        public override SQLResult Result => _result;

        /// <summary>
        /// SQL 服务器信息
        /// </summary>
        public override SQLServerInfo ServerInfo => _serverInfo ?? SQLServerInfo.Create();

        /// <summary>
        /// 上一次执行命令影响的行数
        /// </summary>
        public override int AffectedRows => _affectedRows;

        /// <summary>
        /// 上一次执行的命令是 Insert 时，返回的自增 ID，大于 0 有效
        /// </summary>
        public override long LastInsertId => _lastInsertId;

        /// <summary>
        /// 上一次执行命令的查询结果集
        /// </summary>
        public override DataSet DataSet => _dataSet;

        /// <summary>
        /// SQL 语句参数
        /// </summary>
        public override Dictionary<string, object> Parameters { get; } = [];

        private readonly string _connectionString = "";
        private SqliteConnection? _connection;
        private SqliteTransaction? _transaction;
        private DataSet _dataSet = new();
        private SQLResult _result = SQLResult.NotFound;
        private readonly SQLServerInfo? _serverInfo;
        private int _affectedRows = 0;
        private long _lastInsertId = 0;

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
        /// 执行现有命令（<see cref="Script"/>）
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

                ReSet();
                _affectedRows = command.ExecuteNonQuery();
                if (_affectedRows > 0)
                {
                    _result = SQLResult.Success;
                    if (script.Contains(Core.Library.SQLScript.Constant.Command_Insert, StringComparison.OrdinalIgnoreCase))
                    {
                        using SqliteCommand idCommand = new("SELECT last_insert_rowid()", _connection);
                        if (_transaction != null) idCommand.Transaction = _transaction;
                        _lastInsertId = (long?)idCommand.ExecuteScalar() ?? 0;
                        if (_lastInsertId < 0) _lastInsertId = 0;
                    }
                }
                else _result = SQLResult.Fail;
                if (localTransaction) Commit();
            }
            catch (Exception e)
            {
                if (localTransaction) Rollback();
                _result = SQLResult.SQLError;
                ServerHelper.Error(e);
            }
            finally
            {
                if (localTransaction) Close();
                if (ClearParametersAfterExecute) Parameters.Clear();
            }
            return AffectedRows;
        }

        /// <summary>
        /// 异步执行现有命令（<see cref="Script"/>）
        /// </summary>
        /// <returns></returns>
        public override async Task<int> ExecuteAsync()
        {
            return await ExecuteAsync(Script);
        }

        /// <summary>
        /// 异步执行一个指定的命令
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public override async Task<int> ExecuteAsync(string script)
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

                ReSet();
                _affectedRows = await command.ExecuteNonQueryAsync();
                if (_affectedRows > 0)
                {
                    _result = SQLResult.Success;
                    if (script.Contains(Core.Library.SQLScript.Constant.Command_Insert, StringComparison.OrdinalIgnoreCase))
                    {
                        using SqliteCommand idCommand = new("SELECT last_insert_rowid()", _connection);
                        if (_transaction != null) idCommand.Transaction = _transaction;
                        _lastInsertId = (long?)await idCommand.ExecuteScalarAsync() ?? 0;
                        if (_lastInsertId < 0) _lastInsertId = 0;
                    }
                }
                else _result = SQLResult.Fail;
                if (localTransaction) Commit();
            }
            catch (Exception e)
            {
                if (localTransaction) Rollback();
                _result = SQLResult.SQLError;
                ServerHelper.Error(e);
            }
            finally
            {
                if (localTransaction) Close();
                if (ClearParametersAfterExecute) Parameters.Clear();
            }
            return AffectedRows;
        }

        /// <summary>
        /// 执行现有命令（<see cref="Script"/>）查询 DataSet
        /// </summary>
        /// <returns></returns>
        public override DataSet ExecuteDataSet()
        {
            return ExecuteDataSet(Script);
        }

        /// <summary>
        /// 执行指定的命令查询 DataSet
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

                ReSet();
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
                _result = SQLResult.SQLError;
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
        /// 异步执行现有命令（<see cref="Script"/>）查询 DataSet
        /// </summary>
        /// <returns></returns>
        public override async Task<DataSet> ExecuteDataSetAsync()
        {
            return await ExecuteDataSetAsync(Script);
        }

        /// <summary>
        /// 异步执行指定的命令查询 DataSet
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public override async Task<DataSet> ExecuteDataSetAsync(string script)
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

                ReSet();
                using SqliteDataReader reader = await command.ExecuteReaderAsync();
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
                _result = SQLResult.SQLError;
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
        /// 创建一个 SQL 事务
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
                _result = SQLResult.SQLError;
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
                _result = SQLResult.SQLError;
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
            try
            {
                ExecuteDataSet(Core.Library.SQLScript.Common.Configs.Select_GetConfig(this, "Initialization"));
                return Success;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
                _result = SQLResult.SQLError;
                return false;
            }
            finally
            {
                Close();
            }
        }

        private bool _isDisposed = false;

        /// <summary>
        /// 资源清理
        /// </summary>
        private void Dispose(bool disposing)
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

        private void ReSet()
        {
            _result = SQLResult.NotFound;
            _affectedRows = 0;
            _lastInsertId = 0;
            DataSet.Clear();
        }
    }
}
