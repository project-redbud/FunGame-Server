using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Interface.Base;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class RESTfulAPIListener : ISocketListener<RESTfulAPI>
    {
        public string Name => "RESTfulAPIListener";

        public ConcurrentModelList<IServerModel> ClientList { get; } = [];

        public ConcurrentModelList<IServerModel> UserList { get; } = [];

        public List<string> BannedList { get; } = [];

        public void Close()
        {

        }
    }
}
