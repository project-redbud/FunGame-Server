﻿using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility.DataUtility;
using MySql.Data.MySqlClient;

namespace Milimoe.FunGame.Server.Utility
{
    public class MySQLHelper : SQLHelper
    {
        public override FunGameInfo.FunGame FunGameType => Config.FunGameType;
        public override SQLMode Mode => SQLMode.MySQL;
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
        private MySqlTransaction? _Transaction;
        private readonly IServerModel? ServerModel;
        private readonly bool _IsOneTime = false;

        /// <summary>
        /// 执行一个命令
        /// </summary>
        /// <param name="Result">执行结果</param>
        /// <returns>影响的行数</returns>
        public override int Execute()
        {
            // _IsOneTime = true需要手动创建连接和关闭
            ServerHelper.WriteLine("SQLQuery -> " + Script, InvokeMessageType.Api);
            _DataSet = new DataSet();
            _UpdateRows = MySQLManager.Execute(this, out _Result);
            if (_IsOneTime) Close();
            return _UpdateRows;
        }

        /// <summary>
        /// 执行一个指定的命令
        /// </summary>
        /// <param name="Script">命令</param>
        /// <param name="Result">执行结果</param>
        /// <returns>影响的行数</returns>
        public override int Execute(string Script)
        {
            // _IsOneTime = true需要手动创建连接和关闭
            ServerHelper.WriteLine("SQLQuery -> " + Script, InvokeMessageType.Api);
            this.Script = Script;
            _DataSet = new DataSet();
            _UpdateRows = MySQLManager.Execute(this, out _Result);
            if (_IsOneTime) Close();
            return _UpdateRows;
        }

        /// <summary>
        /// 查询DataSet
        /// </summary>
        /// <param name="Result">执行结果</param>
        /// <returns>结果集</returns>
        public override DataSet ExecuteDataSet()
        {
            // _IsOneTime = true需要手动创建连接和关闭
            ServerHelper.WriteLine("SQLQuery -> " + Script, InvokeMessageType.Api);
            _DataSet = MySQLManager.ExecuteDataSet(this, out _Result, out _UpdateRows);
            if (_IsOneTime) Close();
            return DataSet;
        }

        /// <summary>
        /// 执行指定的命令查询DataSet
        /// </summary>
        /// <param name="Script">命令</param>
        /// <param name="Result">执行结果</param>
        /// <returns>结果集</returns>
        public override DataSet ExecuteDataSet(string Script)
        {
            // _IsOneTime = true需要手动创建连接和关闭
            ServerHelper.WriteLine("SQLQuery -> " + Script, InvokeMessageType.Api);
            this.Script = Script;
            _DataSet = MySQLManager.ExecuteDataSet(this, out _Result, out _UpdateRows);
            if (_IsOneTime) Close();
            return DataSet;
        }

        /// <summary>
        /// 关闭连接 如有事务会自动提交事务
        /// </summary>
        public override void Close()
        {
            // _IsOneTime = false需要手动调用此方法
            Commit();
            _Connection?.Close();
            ServerHelper.WriteLine($"{(GetClientName() == string.Empty ? "" : GetClientName())}已释放MySQL连接");
        }

        /// <summary>
        /// 创建SQLHelper
        /// </summary>
        /// <param name="IsOneTime">是否是单次使用(执行完毕会自动Close连接)</param>
        /// <param name="script">存储过程名称或者script语句</param> 
        /// <param name="type">存储过程, 文本, 等等</param> 
        /// <param name="parameters">执行命令所用参数的集合</param> 
        public MySQLHelper(string script = "", bool IsOneTime = true, CommandType type = CommandType.Text, params MySqlParameter[] parameters)
        {
            Script = script;
            _IsOneTime = IsOneTime;
            CommandType = type;
            Parameters = parameters;
            _Connection = new MySQLConnection(out _ServerInfo);
        }

        /// <summary>
        /// 创建为SocketModel服务的SQLHelper
        /// </summary>
        /// <param name="ServerModel">SocketModel</param>
        public MySQLHelper(IServerModel ServerModel)
        {
            this.ServerModel = ServerModel;
            Script = "";
            CommandType = CommandType.Text;
            Parameters = [];
            _Connection = new MySQLConnection(out _ServerInfo);
        }

        /// <summary>
        /// 创建一个SQL事务
        /// </summary>
        public override void NewTransaction()
        {
            _Transaction ??= _Connection?.Connection?.BeginTransaction();
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public override void Commit()
        {
            _Transaction?.Commit();
            _Transaction = null;
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        public override void Rollback()
        {
            _Transaction?.Rollback();
            _Transaction = null;
        }

        /// <summary>
        /// 获取客户端名称
        /// </summary>
        /// <returns></returns>
        private string GetClientName()
        {
            return ServerModel?.GetClientName() ?? string.Empty;
        }
    }
}
