﻿using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Utility;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Controllers;

namespace Milimoe.FunGame.WebAPI.Models
{
    public class RESTfulAPIModel(ISocketListener<RESTfulAPI> server, string clientip) : ServerModel<RESTfulAPI>(server, new RESTfulAPI(Guid.NewGuid(), clientip, clientip), false)
    {
        public Guid LastRequestID { get; set; } = Guid.Empty;
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
                if (LastRequestID != Guid.Empty)
                {
                    return PostDataController.ResultDatas.TryAdd(LastRequestID, obj);
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
    }
}