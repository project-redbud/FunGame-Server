using System.Text;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services;

namespace Milimoe.FunGame.Server.Controller
{
    public class ConnectController
    {
        /// <summary>
        /// 因为异步函数无法使用 ref 变量，因此使用元组返回
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listener"></param>
        /// <param name="socket"></param>
        /// <param name="token"></param>
        /// <param name="clientip"></param>
        /// <param name="objs"></param>
        /// <returns>[0]isConnected；[1]isDebugMode</returns>
        public static async Task<(bool, bool)> Connect<T>(ISocketListener<T> listener, ISocketMessageProcessor socket, Guid token, string clientip, IEnumerable<SocketObject> objs) where T : ISocketMessageProcessor
        {
            try
            {
                bool isConnected = false;
                bool isDebugMode = false;
                objs = objs.Where(o => o.SocketType == SocketMessageType.Connect);
                foreach (SocketObject obj in objs)
                {
                    if (Config.ConnectingPlayerCount + Config.OnlinePlayerCount > Config.MaxPlayers)
                    {
                        await SendRefuseConnect(socket, "服务器可接受的连接数量已上限！");
                        ServerHelper.WriteLine("服务器可接受的连接数量已上限！", InvokeMessageType.Core);
                        return (isConnected, isDebugMode);
                    }
                    ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 正在连接服务器 . . .", InvokeMessageType.Core);
                    if (IsIPBanned(listener, clientip))
                    {
                        await SendRefuseConnect(socket, "服务器已拒绝黑名单用户连接。");
                        ServerHelper.WriteLine("检测到 " + ServerHelper.MakeClientName(clientip) + " 为黑名单用户，已禁止其连接！", InvokeMessageType.Core);
                        return (isConnected, isDebugMode);
                    }

                    ServerHelper.WriteLine("[" + SocketSet.GetTypeString(obj.SocketType) + "] " + ServerHelper.MakeClientName(socket.ClientIP), InvokeMessageType.Core);

                    // 验证客户端参数
                    string msg = OnValidateClientParameters(ref isDebugMode, obj);

                    if (msg == "" && await socket.SendAsync(SocketMessageType.Connect, true, msg, token, Config.ServerName, Config.ServerNotice) == SocketResult.Success)
                    {
                        isConnected = true;
                        ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "已确认连接", InvokeMessageType.Core);
                        return (isConnected, isDebugMode);
                    }
                    else if (msg != "" && await socket.SendAsync(SocketMessageType.Connect, false, msg) == SocketResult.Success)
                    {
                        ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "拒绝连接", InvokeMessageType.Core);
                        return (isConnected, isDebugMode);
                    }
                    else
                    {
                        ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。", InvokeMessageType.Core);
                        return (isConnected, isDebugMode);
                    }
                }

                await SendRefuseConnect(socket, "服务器已拒绝连接。");
                ServerHelper.WriteLine("客户端发送了不符合FunGame规定的字符，拒绝连接。", InvokeMessageType.Core);
                return (isConnected, isDebugMode);
            }
            catch (Exception e)
            {
                ServerHelper.Error(e);
                await SendRefuseConnect(socket, "请勿发送错误的数据，请保持FunGame版本为最新。");
                throw new SocketWrongInfoException();
            }
        }

        /// <summary>
        /// 回复拒绝连接消息
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        private static async Task<bool> SendRefuseConnect(ISocketMessageProcessor socket, string msg)
        {
            // 发送消息给客户端
            msg = "连接被拒绝，如有疑问请联系服务器管理员：" + msg;
            if (await socket.SendAsync(SocketMessageType.Connect, false, msg) == SocketResult.Success)
            {
                ServerHelper.WriteLine(ServerHelper.MakeClientName(socket.ClientIP) + " <- " + "已拒绝连接", InvokeMessageType.Core);
                return true;
            }
            else
            {
                ServerHelper.WriteLine("无法传输数据，与客户端的连接可能丢失。", InvokeMessageType.Core);
                return false;
            }
        }

        /// <summary>
        /// 判断是否是黑名单里的IP
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="server"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        private static bool IsIPBanned<T>(ISocketListener<T> server, string ip) where T : ISocketMessageProcessor
        {
            string[] strs = ip.Split(":");
            if (strs.Length == 2 && server.BannedList.Contains(strs[0]))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 验证客户端参数事件
        /// </summary>
        public static event Func<SocketObject, string>? ValidateClientParameters;

        /// <summary>
        /// 触发验证客户端参数事件
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string OnValidateClientParameters(ref bool isDebugMode, SocketObject obj)
        {
            StringBuilder builder = new();

            // 先检查 Desktop 参数
            if (Config.UseDesktopParameters)
            {
                // 参数1：客户端的游戏模组列表，没有服务器的需要拒绝
                if (obj.Parameters.Length > 0)
                {
                    string[] modes = obj.GetParam<string[]>(0) ?? [];
                    List<string> ClientDontHave = [];
                    string strDontHave = string.Join("\r\n", Config.GameModuleSupported.Where(mode => !modes.Contains(mode)));
                    if (strDontHave != "")
                    {
                        strDontHave = "客户端缺少服务器所需的模组：" + strDontHave;
                        ServerHelper.WriteLine(strDontHave, InvokeMessageType.Core);
                        builder.AppendLine(strDontHave);
                    }
                }
                // 参数2：客户端是否开启了开发者模式，开启开发者模式部分功能不可用
                if (obj.Parameters.Length > 1)
                {
                    isDebugMode = obj.GetParam<bool>(1);
                    if (isDebugMode) ServerHelper.WriteLine("客户端已开启开发者模式");
                }
            }

            if (ValidateClientParameters is null)
            {
                return "";
            }

            foreach (Func<SocketObject, string> handler in ValidateClientParameters.GetInvocationList().Cast<Func<SocketObject, string>>())
            {
                string msg = handler(obj);
                if (msg != "")
                {
                    builder.AppendLine(msg);
                    ServerHelper.WriteLine(msg, InvokeMessageType.Error);
                }
            }
            return builder.ToString().Trim();
        }
    }
}
