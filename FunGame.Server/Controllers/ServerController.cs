using Milimoe.FunGame.Server.Model;

namespace Milimoe.FunGame.Server.Controller
{
    public class ServerController
    {
        public ServerModel Server { get; }

        public ServerController(ServerModel server)
        {
            Server = server;
        }
    }
}
