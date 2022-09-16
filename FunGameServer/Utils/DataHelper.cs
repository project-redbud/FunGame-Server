using FunGame.Core.Api.Model.Entity;
using FunGame.Core.Api.Model.Enum;
using FunGameServer.Models.Config;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunGameServer.Utils
{
    public class DataHelper
    {
        private string? GetConnection = "";
        private MySqlConnection? msc = null;

        public DataHelper()
        {

        }

        public bool Connect()
        {
            try
            {
                GetConnection = (string?)Config.DefaultAssemblyHelper.GetFunGameCoreValue((int)InterfaceType.ServerInterface, (int)InterfaceMethod.DBConnection);
                if (GetConnection != null)
                {
                    string[] DataSetting = GetConnection.Split(";");
                    if (DataSetting.Length > 1 && DataSetting[0].Length > 14 && DataSetting[1].Length > 8)
                    {
                        ServerHelper.WriteLine("Connect -> MySQL://" + DataSetting[0][14..] + ":" + DataSetting[1][8..]);
                    }
                    msc = new MySqlConnection(GetConnection);
                    msc.Open();
                    if (msc.State == System.Data.ConnectionState.Open)
                    {
                        ServerHelper.WriteLine("Connected: MySQL服务器连接成功");
                        return true;
                    }
                }
                else
                {
                    throw new Exception("MySQL服务启动失败：无法找到MySQL配置文件。");
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
            return false;
        }

        public void Close()
        {
            if (msc != null && msc.State == System.Data.ConnectionState.Open)
            {
                msc.Close();
            }
            msc = null;
        }
    }
}
