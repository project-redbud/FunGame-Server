using System.Data;
using Milimoe.FunGame.Core.Library.Constant;
using MySql.Data.MySqlClient;

namespace Milimoe.FunGame.Server.Utility
{
    public class MySQLManager
    {
        /// <summary>
        /// 执行Script命令
        /// </summary>
        /// <param name="Helper">MySQLHelper</param>
        /// <param name="Result">执行结果</param>
        /// <returns>影响的行数</returns>
        public static int Execute(MySQLHelper Helper, out SQLResult Result)
        {
            MySqlCommand cmd = new();
            int updaterow;

            try
            {
                PrepareCommand(Helper, cmd);

                updaterow = cmd.ExecuteNonQuery();
                if (updaterow > 0)
                {
                    Result = SQLResult.Success;
                }
                else Result = SQLResult.NotFound;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
                updaterow = -1;
                Result = SQLResult.Fail;
            }

            return updaterow;
        }

        /// <summary>
        /// 返回DataSet
        /// </summary>
        /// <param name="Helper">MySQLHelper</param>
        /// <param name="Result">执行结果</param>
        /// <returns>结果集</returns>
        public static DataSet ExecuteDataSet(MySQLHelper Helper, out SQLResult Result, out int Rows)
        {
            MySqlCommand cmd = new();
            DataSet ds = new();
            Rows = 0;

            try
            {
                PrepareCommand(Helper, cmd);

                MySqlDataAdapter adapter = new()
                {
                    SelectCommand = cmd
                };
                adapter.Fill(ds);

                //清除参数
                cmd.Parameters.Clear();

                Rows = ds.Tables[0].Rows.Count;
                if (Rows > 0) Result = SQLResult.Success;
                else Result = SQLResult.NotFound;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
                Result = SQLResult.Fail;
            }

            return ds;
        }

        /// <summary>
        /// 返回插入值ID
        /// </summary>
        /// <param name="Helper">MySQLHelper</param>
        /// <param name="Result">执行结果</param>
        /// <returns>插入值ID</returns>
        public static long ExecuteAndGetLastInsertedID(MySQLHelper Helper, out SQLResult Result)
        {
            MySqlCommand cmd = new();
            int updaterow;

            try
            {
                PrepareCommand(Helper, cmd);

                updaterow = cmd.ExecuteNonQuery();
                if (updaterow > 0)
                {
                    Result = SQLResult.Success;
                }
                else Result = SQLResult.NotFound;
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
                Result = SQLResult.Fail;
            }

            return cmd.LastInsertedId;
        }

        /// <summary>
        /// 准备执行一个命令
        /// </summary>
        /// <param name="Helper">MySQLHelper</param>
        /// <param name="cmd">命令对象</param>
        public static void PrepareCommand(MySQLHelper Helper, MySqlCommand cmd)
        {
            if (Helper.Connection != null)
            {
                MySqlConnection? conn = Helper.Connection.Connection;

                if (conn != null)
                {
                    if (conn.State != ConnectionState.Open) conn.Open();

                    cmd.Connection = conn;
                    cmd.CommandText = Helper.Script;
                    cmd.CommandType = Helper.CommandType;

                    if (Helper.Parameters != null)
                    {
                        foreach (MySqlParameter parm in Helper.Parameters)
                        {
                            cmd.Parameters.Add(parm);
                        }
                    }
                }
            }
        }
    }
}
