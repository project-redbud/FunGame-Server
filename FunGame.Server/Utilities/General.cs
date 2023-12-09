using System.Collections;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Others;

namespace Milimoe.FunGame.Server.Utility
{
    public class ServerHelper
    {
        public static string GetPrefix(InvokeMessageType type)
        {
            string prefix = "";
            switch (type)
            {
                case InvokeMessageType.Core:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    prefix = "[Core] ";
                    break;
                case InvokeMessageType.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    prefix = "[Error] ";
                    break;
                case InvokeMessageType.Api:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    prefix = "[Api] ";
                    break;
                case InvokeMessageType.Interface:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    prefix = "[Interface] ";
                    break;
                case InvokeMessageType.DataRequest:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    prefix = "[DataRequest] ";
                    break;
                case InvokeMessageType.Plugin:
                    Console.ForegroundColor = ConsoleColor.Green;
                    prefix = "[Plugin] ";
                    break;
                case InvokeMessageType.GameMode:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    prefix = "[GameMode] ";
                    break;
                case InvokeMessageType.System:
                case InvokeMessageType.None:
                default:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    prefix = "[System] ";
                    break;
            }
            DateTime now = DateTime.Now;
            return now.AddMilliseconds(-now.Millisecond).ToString() + " " + prefix + Config.ServerName + "：";
        }

        public static void Error(Exception e)
        {
            Console.Write("\r" + GetPrefix(InvokeMessageType.Error) + e.Message + "\n" + e.StackTrace + "\n\r> ");
            Console.ResetColor();
        }

        public static void Write(string msg, InvokeMessageType type = InvokeMessageType.System)
        {
            if (msg.Trim() != "") Console.Write("\r" + GetPrefix(type) + msg + "> ");
        }

        public static void WriteLine(string msg, InvokeMessageType type = InvokeMessageType.System)
        {
            if (msg.Trim() != "") Console.Write("\r" + GetPrefix(type) + msg + "\n\r> ");
        }

        public static void Type()
        {
            Console.Write("\r> ");
        }

        public static string MakeClientName(string name, User? user = null)
        {
            if (user != null && user.Id != 0)
            {
                return "玩家 " + user.Username;
            }
            if (name != "") return "客户端(" + name + ")";
            return "客户端";
        }

        private static Hashtable GetServerSettingHashtable()
        {
            Hashtable settings = new();
            if (INIHelper.ExistINIFile())
            {
                settings.Add("Name", INIHelper.ReadINI("Server", "Name"));
                settings.Add("Password", INIHelper.ReadINI("Server", "Password"));
                settings.Add("Describe", INIHelper.ReadINI("Server", "Describe"));
                settings.Add("Notice", INIHelper.ReadINI("Server", "Notice"));
                settings.Add("Key", INIHelper.ReadINI("Server", "Key"));
                settings.Add("Status", Convert.ToInt32(INIHelper.ReadINI("Server", "Status")));
                settings.Add("BannedList", INIHelper.ReadINI("Server", "BannedList"));
                settings.Add("OfficialMail", INIHelper.ReadINI("ServerMail", "OfficialMail"));
                settings.Add("SupportMail", INIHelper.ReadINI("ServerMail", "SupportMail"));
                settings.Add("Port", Convert.ToInt32(INIHelper.ReadINI("Socket", "Port")));
                settings.Add("MaxPlayer", Convert.ToInt32(INIHelper.ReadINI("Socket", "MaxPlayer")));
                settings.Add("MaxConnectFailed", Convert.ToInt32(INIHelper.ReadINI("Socket", "MaxConnectFailed")));
            }
            return settings;
        }

        public static void GetServerSettings()
        {
            try
            {
                Hashtable settings = GetServerSettingHashtable();
                if (settings != null)
                {
                    string? Name = (string?)settings["Name"];
                    string? Password = (string?)settings["Password"];
                    string? Describe = (string?)settings["Describe"];
                    string? Notice = (string?)settings["Notice"];
                    string? Key = (string?)settings["Key"];
                    string? BannedList = (string?)settings["BannedList"];

                    if (Name != null) Config.ServerName = Name;
                    if (Password != null) Config.ServerPassword = Password;
                    if (Describe != null) Config.ServerDescription = Describe;
                    if (Notice != null) Config.ServerNotice = Notice;
                    if (Key != null) Config.ServerKey = Key;
                    if (BannedList != null) Config.ServerBannedList = BannedList;

                    string? OfficialMail = (string?)settings["OfficialMail"];
                    string? SupportMail = (string?)settings["SupportMail"];

                    if (OfficialMail != null) OfficialEmail.Email = OfficialMail;
                    if (SupportMail != null) OfficialEmail.SupportEmail = SupportMail;

                    int? Status = (int?)settings["Status"];
                    int? Port = (int?)settings["Port"];
                    int? MaxPlayer = (int?)settings["MaxPlayer"];
                    int? MaxConnectFailed = (int?)settings["MaxConnectFailed"];

                    if (Status != null) Config.ServerStatus = (int)Status;
                    if (Port != null) Config.ServerPort = (int)Port;
                    if (MaxPlayer != null) Config.MaxPlayers = (int)MaxPlayer;
                    if (MaxConnectFailed != null) Config.MaxConnectionFaileds = (int)MaxConnectFailed;
                }
            }
            catch (Exception e)
            {
                ServerHelper.WriteLine(e.StackTrace ?? "");
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

    public class SmtpHelper
    {
        public static string SenderMailAddress { get; set; } = "";
        public static string SenderName { get; set; } = "";
        public static string SenderPassword { get; set; } = "";
        public static string SmtpHost { get; set; } = "";
        public static int SmtpPort { get; set; } = 587;
        public static bool OpenSSL { get; set; } = true;

        public static MailSender? GetMailSender()
        {
            try
            {
                if (SenderMailAddress == "" && SenderName == "" && SenderPassword == "" && SmtpHost == "")
                {
                    if (INIHelper.ExistINIFile())
                    {
                        if (bool.TryParse(INIHelper.ReadINI("Mailer", "UseMailSender").ToLower(), out bool use))
                        {
                            if (use)
                            {
                                SenderMailAddress = INIHelper.ReadINI("Mailer", "MailAddress");
                                SenderName = INIHelper.ReadINI("Mailer", "Name");
                                SenderPassword = INIHelper.ReadINI("Mailer", "Password");
                                SmtpHost = INIHelper.ReadINI("Mailer", "Host");
                                if (int.TryParse(INIHelper.ReadINI("Mailer", "Port"), out int Port))
                                    SmtpPort = Port;
                                if (bool.TryParse(INIHelper.ReadINI("Mailer", "OpenSSL").ToLower(), out bool SSL))
                                    OpenSSL = SSL;
                                if (SmtpPort > 0) return new MailSender(SenderMailAddress, SenderName, SenderPassword, SmtpHost, SmtpPort, OpenSSL);
                            }
                        }
                        ServerHelper.WriteLine("Smtp服务处于关闭状态");
                        return null;
                    }
                    throw new SmtpHelperException();
                }
                return new MailSender(SenderMailAddress, SenderName, SenderPassword, SmtpHost, SmtpPort, OpenSSL);
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
            return null;
        }
    }
}
