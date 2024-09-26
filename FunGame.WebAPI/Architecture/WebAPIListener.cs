using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class WebAPIListener : ISocketListener<ClientWebSocket>
    {
        public ConcurrentModelList<IServerModel> ClientList => [];

        public ConcurrentModelList<IServerModel> UserList => [];

        public List<string> BannedList => [];

        public void Close()
        {
            
        }
    }
}
