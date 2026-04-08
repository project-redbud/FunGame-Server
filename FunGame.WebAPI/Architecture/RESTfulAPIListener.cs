using System.Collections.Concurrent;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Addon;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class RESTfulAPIListener : ISocketListener<RESTfulAPI>
    {
        public static RESTfulAPIListener? Instance { get; set; } = null;

        public string Name => "RESTfulAPIListener";

        public ConcurrentModelList<IServerModel> ClientList { get; } = [];

        public ConcurrentModelList<IServerModel> UserList { get; } = [];

        public ConcurrentDictionary<long, GameModuleServer> NowGamingServers { get; } = [];

        public List<string> BannedList { get; } = [];

        public void Close()
        {

        }
    }
}
