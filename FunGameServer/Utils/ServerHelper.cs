using FunGame.Core.Api.Model.Enum;
using FunGameServer.Models.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FunGameServer.Utils
{
    public class ServerHelper
    {
        public static string GetPrefix()
        {
            DateTime now = System.DateTime.Now;
            return now.AddMilliseconds(-now.Millisecond).ToString() + " " + Config.SERVER_NAME + "：";
        }

        public static void Error(Exception e)
        {
            Console.Write("\r" + GetPrefix() + e.Message + "\n" + e.StackTrace + "\n\r> ");
        }

        public static void WriteLine(string? msg)
        {
            Console.Write("\r" + GetPrefix() + msg + "\n\r> ");
        }
        
        public static void Type()
        {
            Console.Write("\r> ");
        }

        public static void GetServerSettings()
        {
            try
            {
                Hashtable? settings = (Hashtable?)Config.DefaultAssemblyHelper.GetFunGameCoreValue((int)InterfaceType.ServerInterface, (int)InterfaceMethod.GetServerSettings);
                if (settings != null)
                {
                    string? Name = (string?)settings["Name"];
                    string? Password = (string?)settings["Password"];
                    string? Describe = (string?)settings["Describe"];
                    string? Notice = (string?)settings["Notice"];
                    string? Key = (string?)settings["Key"];
                    if (Name != null) Config.SERVER_NAME = Name;
                    if (Password != null) Config.SERVER_PASSWORD = Password;
                    if (Describe != null) Config.SERVER_DESCRIBE = Describe;
                    if (Notice != null) Config.SERVER_NOTICE = Notice;
                    if (Key != null) Config.SERVER_KEY = Key;
                    int? Status = (int?)settings["Status"];
                    int? Port = (int?)settings["Port"];
                    int? MaxPlayer = (int?)settings["MaxPlayer"];
                    int? MaxConnectFailed = (int?)settings["MaxConnectFailed"];
                    if (Status != null) Config.SERVER_STATUS = (int)Status;
                    if (Port != null) Config.SERVER_PORT = (int)Port;
                    if (MaxPlayer != null) Config.MAX_PLAYERS = (int)MaxPlayer;
                    if (MaxConnectFailed != null) Config.MAX_CONNECTFAILED = (int)MaxConnectFailed;
                }
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(e.StackTrace);
            }
        }

        public static void InitOrderList()
        {
            Config.OrderList.Clear();
            Config.OrderList.Add(OrderDictionary.Help, "Milimoe -> 帮助");
            Config.OrderList.Add(OrderDictionary.Quit, "关闭服务器");
            Config.OrderList.Add(OrderDictionary.Exit, "关闭服务器");
            Config.OrderList.Add(OrderDictionary.Close, "关闭服务器");
            Config.OrderList.Add(OrderDictionary.Restart, "重启服务器");
        }
    }
}
