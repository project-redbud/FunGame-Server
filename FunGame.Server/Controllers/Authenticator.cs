using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Model;

namespace Milimoe.FunGame.Server.Controller
{
    public class Authenticator : Core.Library.Common.Architecture.Authenticator
    {
        public TwoFactorAuthenticator Login2FA = new();

        private readonly ServerModel Server;
        private readonly SQLHelper SQLHelper;
        private readonly MailSender? MailSender;

        public Authenticator(ServerModel Server, SQLHelper SQLHelper, MailSender? MailSender) : base(SQLHelper)
        {
            this.Server = Server;
            this.SQLHelper = SQLHelper;
            this.MailSender = MailSender;
        }

        public override bool AfterAuthenticator(AuthenticationType type, params object[] args)
        {
            if (type == AuthenticationType.Username)
            {
                // 添加2FA二次验证等
                string username = (string)args[0];
            }
            return true;
        }

        public override bool BeforeAuthenticator(AuthenticationType type, params object[] args)
        {
            // 添加人机验证或频繁验证等
            return true;
        }

        public bool Check2FA(string username, string code)
        {
            return Login2FA.Authenticate(username, code);
        }
    }
}
