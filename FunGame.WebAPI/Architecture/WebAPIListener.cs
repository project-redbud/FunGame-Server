using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class WebAPIListener : ISocketListener<ServerWebSocket>
    {
        public ConcurrentModelList<IServerModel> ClientList { get; } = [];

        public ConcurrentModelList<IServerModel> UserList { get; } = [];

        public List<string> BannedList { get; } = [];

        public void Close()
        {
            
        }
    }
}
