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
        public static void InitOrders<T>(ISocketListener<T>? server) where T : ISocketMessageProcessor
        {
            FunGameSystem.OrderList[OrderDictionary.Kick] = async (args) =>
            {
                if (args.Length == 0 || args[0] is not string client)
                {
                    ServerHelper.Write("输入需要踢出的客户端名称：");
                    client = await Console.In.ReadLineAsync() ?? "";
                }
                if (client != "" && server != null && server.ClientList.ContainsKey(client))
                {
                    await Kick(server.ClientList[client]);
                }
                else
                {
                    ServerHelper.WriteLine("未找到指定的客户端。");
                }
            };
            FunGameSystem.OrderList[OrderDictionary.Logout] = async (args) =>
            {
                if (args.Length == 0 || args[0] is not string user)
                {
                    ServerHelper.Write("输入需要强制下线的玩家ID：");
                    user = await Console.In.ReadLineAsync() ?? "";
                }
                if (user != "" && server != null && server.UserList.ContainsKey(user))
                {
                    await ForceLogOut(server.UserList[user]);
                }
                else
                {
                    ServerHelper.WriteLine("未找到指定的玩家。");
                }
            };
            FunGameSystem.OrderList[OrderDictionary.Show] = async (args) =>
            {
                if (args.Length >= 0 && args[0] is string type)
                {
                    switch (type)
                    {
                        case "clients":
                        case "-c":
                            ShowClients(server);
                            break;
                        case "users":
                        case "-u":
                            ShowUsers(server);
                            break;
                        default:
                            ServerHelper.WriteLine($"指令 '{OrderDictionary.Show}' 的参数 '{type}' 无效。", InvokeMessageType.Warning);
                            ShowClients(server);
                            ShowUsers(server);
                            break;
                    }
                }
                else
                {
                    ShowClients(server);
                    ShowUsers(server);
                }
            };
            FunGameSystem.OrderList[OrderDictionary.ShowClients] = async (args) =>
            {
                ShowClients(server);
            };
            FunGameSystem.OrderList[OrderDictionary.ShowUsers] = async (args) =>
            {
                ShowUsers(server);
            };
            FunGameSystem.OrderList[OrderDictionary.Reload] = async (args) =>
            {
                if (args.Length >= 0 && args[0] is string type)
                {
                    switch (type)
                    {
                        case "addons":
                        case "-a":
                            FunGameSystem.HotReloadServerPlugins();
                            FunGameSystem.HotReloadWebAPIPlugins();
                            FunGameSystem.HotReloadGameModuleList();
                            break;
                        case "modules":
                        case "-m":
                            FunGameSystem.HotReloadGameModuleList();
                            break;
                        case "plugins":
                        case "-p":
                            FunGameSystem.HotReloadServerPlugins();
                            FunGameSystem.HotReloadWebAPIPlugins();
                            break;
                        case "serverplugins":
                        case "-sp":
                            FunGameSystem.HotReloadServerPlugins();
                            break;
                        case "webapiplugins":
                        case "-wp":
                            FunGameSystem.HotReloadWebAPIPlugins();
                            break;
                        default:
                            ServerHelper.WriteLine($"指令 '{OrderDictionary.Reload}' 的参数 '{type}' 无效。", InvokeMessageType.Error);
                            break;
                    }
                }
            };
            FunGameSystem.OrderList[OrderDictionary.Help] = async (args) =>
            {
                ServerHelper.WriteLine($"可用指令：{string.Join("，", FunGameSystem.OrderAliasList.Keys.Select(c => $"{c}{GetOrderAliases(c)}"))}");
            };
            FunGameSystem.OrderList[OrderDictionary.Ban] = async (args) =>
            {
                if (args.Length > 0 && args[0] is string type)
                {
                    if (args.Length == 1 || args[1] is not string banned)
                    {
                        ServerHelper.WriteLine($"没有提供指令 '{OrderDictionary.Ban}' 所需的第二个参数 'banned ip' 值。", InvokeMessageType.Error);
                        return;
                    }
                    if (!NetworkUtility.IsIP(banned))
                    {
                        ServerHelper.WriteLine($"指令 '{OrderDictionary.Ban}' 的参数 '{banned}' 不是一个 IP 地址。", InvokeMessageType.Error);
                        return;
                    }
                    switch (type)
                    {
                        case "add":
                        case "-a":
                            Config.ServerBannedList.Add(banned);
                            ServerHelper.WriteLine($"将 {banned} 添加入黑名单成功。");
                            break;
                        case "remove":
                        case "-r":
                            Config.ServerBannedList.Remove(banned);
                            ServerHelper.WriteLine($"将 {banned} 移出黑名单成功。");
                            break;
                        default:
                            ServerHelper.WriteLine($"指令 '{OrderDictionary.Ban}' 的参数 '{type}' 无效。", InvokeMessageType.Error);
                            break;
                    }
                }
                else
                {
                    ServerHelper.WriteLine($"没有提供指令 '{OrderDictionary.Ban}' 所需的参数。", InvokeMessageType.Error);
                }
            };
        }

        public static void AddOrderAlias(string order, params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                FunGameSystem.OrderAliasList[alias] = order;
            }
        }

        public static async Task Order(string order, string[] args)
        {
            try
            {
                if (FunGameSystem.OrderList.TryGetValue(order, out Func<string[], Task>? func) && func != null)
                {
                    await func.Invoke(args);
                }
                else if (FunGameSystem.OrderAliasList.TryGetValue(order, out string? actualOrder) && actualOrder != null)
                {
                    if (FunGameSystem.OrderList.TryGetValue(actualOrder, out Func<string[], Task>? func2) && func2 != null)
                    {
                        await func2.Invoke(args);
                    }
                }
                // 广播到插件
                if (FunGameSystem.ServerPluginLoader != null)
                {
                    foreach (ServerPlugin plugin in FunGameSystem.ServerPluginLoader.Plugins.Values)
                    {
                        plugin.ProcessInput(order, args);
                    }
                }
                if (FunGameSystem.WebAPIPluginLoader != null)
                {
                    foreach (WebAPIPlugin plugin in FunGameSystem.WebAPIPluginLoader.Plugins.Values)
                    {
                        plugin.ProcessInput(order, args);
                    }
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        public static string GetOrderAliases(string order)
        {
            string[] alias = [.. FunGameSystem.OrderAliasList.Where(kv => kv.Value == order).Select(kv => kv.Key)];
            return alias.Length > 0 ? $"（替代：{string.Join("，", alias)}）" : "";
        }

        private static async Task Kick(IServerModel clientModel)
        {
            await clientModel.Kick("您已被服务器管理员踢出此服务器。");
        }

        private static async Task ForceLogOut(IServerModel clientModel)
        {
            await clientModel.ForceLogOut("您已被服务器管理员强制下线。");
        }

        private static void ShowClients<T>(ISocketListener<T>? server) where T : ISocketMessageProcessor
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

        private static void ShowUsers<T>(ISocketListener<T>? server) where T : ISocketMessageProcessor
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
