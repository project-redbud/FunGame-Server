using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;
using TFA = Milimoe.FunGame.Server.Utility.TFA;

namespace Milimoe.FunGame.Server.Controllers
{
    public class Authenticator : Core.Library.Common.Architecture.Authenticator
    {
        public TFA Login2FA = new();

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
                string code = Login2FA.GetTFACode(username);
                if (MailSender != null)
                {
                    // 获取此账号的邮箱
                    string email = "";
                    SQLHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(username));
                    if (SQLHelper.Success && SQLHelper.DataSet.Tables[0].Rows.Count > 0)
                    {
                        email = Convert.ToString(SQLHelper.DataSet.Tables[0].Rows[0][UserQuery.Column_Email]) ?? "";
                    }
                    // 发送验证码
                    if (email != "")
                    {
                        string ServerName = Config.ServerName;
                        string Subject = $"[{ServerName}] FunGame 双重认证";
                        string Body = $"亲爱的 {username}， <br/>    您正在登录[{ServerName}]，为了保证安全性，需要进行邮箱验证，您的验证码是 {code} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.DateOnly)}";
                        string[] To = new string[] { email };
                        if (MailSender.Send(MailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                        {
                            ServerHelper.WriteLine(Server.GetClientName() + $" 已向{email}发送验证码：{code}");
                        }
                        else
                        {
                            ServerHelper.WriteLine(Server.GetClientName() + " 无法发送验证码");
                            ServerHelper.WriteLine(MailSender.ErrorMsg);
                        }
                    }
                    else
                    {
                        ServerHelper.WriteLine(Server.GetClientName() + $" 验证码为：{code}，请服务器管理员告知此用户");
                    }
                }
                else // 不使用MailSender的情况
                {
                    ServerHelper.WriteLine(Server.GetClientName() + $" 验证码为：{code}，请服务器管理员告知此用户");
                }
            }
            return true;
        }

        public override bool BeforeAuthenticator(AuthenticationType type, params object[] args)
        {
            // 添加人机验证或频繁验证等
            return true;
        }

        public bool Check2FA(string username, string code, out string msg)
        {
            return Login2FA.Authenticate(username, code, out msg);
        }
    }
}
