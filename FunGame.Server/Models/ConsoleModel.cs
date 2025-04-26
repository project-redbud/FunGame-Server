using System.Text;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Entity;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services;
using ProjectRedbud.FunGame.SQLQueryExtension;

namespace Milimoe.FunGame.Server.Model
{
    public class ConsoleModel
    {
        public static async Task Order<T>(ISocketListener<T>? server, string order) where T : ISocketMessageProcessor
        {
            try
            {
                switch (order)
                {
                    case OrderDictionary.Kick:
                        {
                            ServerHelper.Write("输入需要踢出的客户端名称：");
                            string client = Console.ReadLine() ?? "";
                            if (client != "" && server != null)
                            {
                                await Kick(server.ClientList[client]);
                            }
                            break;
                        }
                    case OrderDictionary.Logout:
                        {
                            ServerHelper.Write("输入需要强制下线的玩家ID：");
                            string user = Console.ReadLine() ?? "";
                            if (user != "" && server != null)
                            {
                                await ForceLogOut(server.UserList[user]);
                            }
                            break;
                        }
                    case OrderDictionary.ShowList:
                        ShowClients(server);
                        ShowUsers(server);
                        break;
                    case OrderDictionary.ShowClients1:
                    case OrderDictionary.ShowClients2:
                        ShowClients(server);
                        break;
                    case OrderDictionary.ShowUsers1:
                    case OrderDictionary.ShowUsers2:
                        ShowUsers(server);
                        break;
                    default:
                        break;
                }
                // 广播到插件
                if (FunGameSystem.ServerPluginLoader != null)
                {
                    foreach (ServerPlugin plugin in FunGameSystem.ServerPluginLoader.Plugins.Values)
                    {
                        plugin.ProcessInput(order);
                    }
                }
                if (FunGameSystem.WebAPIPluginLoader != null)
                {
                    foreach (WebAPIPlugin plugin in FunGameSystem.WebAPIPluginLoader.Plugins.Values)
                    {
                        plugin.ProcessInput(order);
                    }
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        public static async Task Kick(IServerModel clientModel)
        {
            await clientModel.Kick("您已被服务器管理员踢出此服务器。");
        }

        public static async Task ForceLogOut(IServerModel clientModel)
        {
            await clientModel.ForceLogOut("您已被服务器管理员强制下线。");
        }

        public static void ShowClients<T>(ISocketListener<T>? server) where T : ISocketMessageProcessor
        {
            if (server != null)
            {
                ServerHelper.WriteLine("显示在线客户端列表");
                int index = 1;
                foreach (IServerModel client in server.ClientList)
                {
                    ServerHelper.WriteLine(index++ + ". " + client.ClientName + (client.User.Id != 0 ? " (已登录为：" + client.User.Username + ")" : ""));
                }
            }
        }

        public static void ShowUsers<T>(ISocketListener<T>? server) where T : ISocketMessageProcessor
        {
            if (server != null)
            {
                ServerHelper.WriteLine("显示在线玩家列表");
                int index = 1;
                foreach (IServerModel user in server.UserList.Where(u => u.User.Id != 0))
                {
                    ServerHelper.WriteLine(index++ + ". " + (user.User.Username) + " (客户端：" + user.ClientName + ")");
                }
            }
        }

        public static void FirstRunRegAdmin()
        {
            using SQLHelper? sql = Factory.OpenFactory.GetSQLHelper() ?? throw new SQLServiceException();
            ServerHelper.WriteLine("首次启动需要注册管理员账号，请按提示输入信息！", InvokeMessageType.Core);
            string username, password, email;
            ServerHelper.Write("请输入管理员用户名：", InvokeMessageType.Core);
            while (true)
            {
                username = Console.ReadLine() ?? "";
                int usernameLength = NetworkUtility.GetUserNameLength(username);
                if (usernameLength < 3 || usernameLength > 12)
                {
                    ServerHelper.WriteLine("账号名长度不符合要求：3~12个字符数（一个中文2个字符）", InvokeMessageType.Error);
                }
                else
                {
                    break;
                }
            }
            ServerHelper.Write("请输入管理员邮箱：", InvokeMessageType.Core);
            while (true)
            {
                email = Console.ReadLine() ?? "";
                if (!NetworkUtility.IsEmail(email))
                {
                    ServerHelper.WriteLine("这不是一个邮箱地址！", InvokeMessageType.Error);
                }
                else
                {
                    break;
                }
            }
            ServerHelper.Write("请输入管理员密码：", InvokeMessageType.Core);
            while (true)
            {
                StringBuilder passwordBuilder = new();
                ConsoleKeyInfo key;

                do
                {
                    key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        break;
                    }
                    else if (key.Key == ConsoleKey.Backspace)
                    {
                        if (passwordBuilder.Length > 0)
                        {
                            passwordBuilder.Remove(passwordBuilder.Length - 1, 1);
                            Console.Write("\b \b");
                        }
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        passwordBuilder.Append(key.KeyChar);
                        Console.Write("*");
                    }
                } while (true);

                password = passwordBuilder.ToString();

                if (password.Length < 6 || password.Length > 15)
                {
                    ServerHelper.WriteLine("密码长度不符合要求：6~15个字符数", InvokeMessageType.Error);
                }
                else
                {
                    break;
                }
            }
            (string msg, RegInvokeType type, bool success) = DataRequestService.RegisterUser(sql, username, password, email, "localhost");
            ServerHelper.WriteLine(msg, InvokeMessageType.Core);
            if (success)
            {
                User? user = sql.GetUserByUsernameAndEmail(username, email);
                if (user != null)
                {
                    user.IsAdmin = true;
                    sql.UpdateUser(user);
                }
            }
        }
    }
}
