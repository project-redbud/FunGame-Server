using FunGameServer.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FunGame.Core.Api.Model.Enum.CommonEnums;

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

        public static string GetServerNotice()
        {
            try
            {
                
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(e.StackTrace);
            }
            return "";
        }
    }
}
