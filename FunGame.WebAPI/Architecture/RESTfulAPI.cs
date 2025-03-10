using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;

namespace Milimoe.FunGame.WebAPI.Architecture
{
    public class RESTfulAPI(Guid token, string clientip, string clientname) : ISocketMessageProcessor
    {
        public Type InstanceType => typeof(RESTfulAPI);

        public Guid Token { get; init; } = token;

        public string ClientIP { get; init; } = clientip;

        public string ClientName { get; init; } = clientname;

        public void Close()
        {

        }

        public async Task CloseAsync()
        {
            await Task.Delay(100);
        }

        public SocketObject[] Receive()
        {
            return [];
        }

        public async Task<SocketObject[]> ReceiveAsync()
        {
            await Task.CompletedTask;
            return [];
        }

        public SocketResult Send(SocketMessageType type, params object[] objs)
        {
            return SocketResult.Success;
        }

        public async Task<SocketResult> SendAsync(SocketMessageType type, params object[] objs)
        {
            await Task.CompletedTask;
            return SocketResult.Success;
        }
    }
}
