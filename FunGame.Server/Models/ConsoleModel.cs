using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public class ConsoleModel
    {
        public static void Order<T>(ISocketListener<T>? server, string order) where T : ISocketMessageProcessor
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
                                ((BaseServerModel<T>)server.ClientList[client])?.Kick("您已被服务器管理员踢出此服务器。");
                            }
                            break;
                        }
                    case OrderDictionary.Logout:
                        {
                            ServerHelper.Write("输入需要强制下线的玩家ID：");
                            string user = Console.ReadLine() ?? "";
                            if (user != "" && server != null)
                            {
                                ((BaseServerModel<T>)server.UserList[user])?.ForceLogOut("您已被服务器管理员强制下线。");
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
                    case OrderDictionary.Help:
                        ServerHelper.WriteLine("Milimoe -> 帮助");
                        break;
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
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
    }
}
