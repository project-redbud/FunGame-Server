using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

namespace Milimoe.FunGame.Server.Model
{
    public class ConsoleModel
    {
        public static void Order(ServerSocket? server, string order)
        {
            try
            {
                switch (order)
                {
                    case OrderDictionary.Kick:
                        ServerHelper.Write("输入需要踢出的玩家ID：");
                        string user = Console.ReadLine() ?? "";
                        ServerHelper.Type();
                        if (user != "" && server != null)
                        {
                            ((ServerModel)server.Get(user))?.ForceLogOut("您已被服务器管理员踢出此服务器。");
                        }
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
    }
}
