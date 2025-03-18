using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Interface.Base;
using Milimoe.FunGame.Core.Library.Constant;

namespace Milimoe.FunGame.Server.Services
{
    public class Authenticator(IServerModel Server, SQLHelper SQLHelper, MailSender? MailSender) : Core.Library.Common.Architecture.Authenticator(SQLHelper)
    {
        public TFA TFA = new(SQLHelper);

        private readonly IServerModel Server = Server;
        private readonly SQLHelper SQLHelper = SQLHelper;
        private readonly MailSender? MailSender = MailSender;

        public override bool AfterAuthenticator(AuthenticationType type, params object[] args)
        {
            if (type == AuthenticationType.Username && args[0] is string username)
            {
                // 添加2FA二次验证等
            }
            return true;
        }

        public override bool BeforeAuthenticator(AuthenticationType type, params object[] args)
        {
            // 添加人机验证或频繁验证等
            return true;
        }
    }
}
