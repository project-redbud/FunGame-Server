using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Services;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Controllers;

namespace Milimoe.FunGame.WebAPI.Models
{
    public class RESTfulAPIModel(ISocketListener<RESTfulAPI> server, string clientip) : ServerModel<RESTfulAPI>(server, new RESTfulAPI(Guid.NewGuid(), clientip, clientip), false)
    {
        public Guid RequestID { get; set; } = Guid.Empty;
        public List<SocketObject> ToBeSent { get; set; } = [];

        public override async Task<bool> Send(SocketMessageType type, params object[] objs)
        {
            if (type == SocketMessageType.Disconnect || type == SocketMessageType.ForceLogout)
            {
                RemoveUser();
                await Close();
                return true;
            }
            if (type != SocketMessageType.HeartBeat)
            {
                SocketObject obj = new(type, Token, objs);
                if (RequestID != Guid.Empty)
                {
                    return AdapterController.ResultDatas.TryAdd(RequestID, obj);
                }
                else
                {
                    ToBeSent.Add(obj);
                    return true;
                }
            }
            return false;
        }

        public override async Task<bool> SocketMessageHandler(ISocketMessageProcessor socket, SocketObject obj)
        {
            // 读取收到的消息
            SocketMessageType type = obj.SocketType;
            Guid token = obj.Token;
            string msg = "";

            // 验证Token
            if (type != SocketMessageType.HeartBeat && token != socket.Token)
            {
                ServerHelper.WriteLine(GetClientName() + " 使用了非法方式传输消息，服务器拒绝回应 -> [" + SocketSet.GetTypeString(type) + "]");
                return false;
            }

            if (type == SocketMessageType.EndGame)
            {
                if (NowGamingServer != null && NowGamingServer.IsAnonymous)
                {
                    NowGamingServer.CloseAnonymousServer(this);
                }
                NowGamingServer = null;
                return true;
            }

            if (type == SocketMessageType.AnonymousGameServer)
            {
                return await AnonymousGameServerHandler(obj);
            }
            
            if (type == SocketMessageType.DataRequest)
            {
                return await DataRequestHandler(obj);
            }

            if (type == SocketMessageType.GamingRequest)
            {
                return await GamingRequestHandler(obj);
            }

            if (type == SocketMessageType.Gaming)
            {
                return await GamingMessageHandler(obj);
            }

            return await Send(type, msg);
        }

        public CancellationTokenSource SetRequestTimeout(Guid uid, int timeout = 60000)
        {
            CancellationTokenSource cts = new(timeout);
            cts.Token.Register(() =>
            {
                if (RequestID == uid)
                {
                    RequestID = Guid.Empty;
                    ServerHelper.WriteLine($"请求 {uid} 超时，已释放 RequestID。", InvokeMessageType.DataRequest);
                }
                cts.Dispose();
            });
            return cts;
        }
    }
}
