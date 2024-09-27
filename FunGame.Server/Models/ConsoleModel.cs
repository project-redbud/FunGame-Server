using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

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
                                await Kick((ServerModel<T>)server.ClientList[client]);
                            }
                            break;
                        }
                    case OrderDictionary.Logout:
                        {
                            ServerHelper.Write("输入需要强制下线的玩家ID：");
                            string user = Console.ReadLine() ?? "";
                            if (user != "" && server != null)
                            {
                                await ForceLogOut((ServerModel<T>)server.UserList[user]);
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
                        ShowHelp();
                        break;
                }
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
            }
        }

        public static async Task Kick<T>(ServerModel<T> clientModel) where T : ISocketMessageProcessor
        {
            await clientModel.Kick("您已被服务器管理员踢出此服务器。");
        }
        
        public static async Task ForceLogOut<T>(ServerModel<T> clientModel) where T : ISocketMessageProcessor
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

        public static void ShowHelp()
        {
            ServerHelper.WriteLine("Milimoe -> 帮助");
        }
    }
}
