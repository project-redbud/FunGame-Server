using System.Data;
using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Event;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;
using ProjectRedbud.FunGame.SQLQueryExtension;

namespace Milimoe.FunGame.Server.Services
{
    public class DataRequestService
    {
        /// <summary>
        /// 获取插件取消请求的原因
        /// </summary>
        /// <param name="type"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private static string GetPluginCancelString(DataRequestType type, GeneralEventArgs e) => $"{DataRequestSet.GetTypeString(type)} 请求已取消。{(e.EventMsg != "" ? $"原因：{e.EventMsg}" : "")}";

        #region Register

        public static (string Msg, RegInvokeType RegInvokeType, bool Success) Reg(object sender, string username, string password, string email, string verifyCode, string clientIP = "")
        {
            string msg;
            RegInvokeType type = RegInvokeType.None;
            bool success = false;
            string clientName = ServerHelper.MakeClientName(clientIP);

            RegisterEventArgs eventArgs = new(username, password, email);
            FunGameSystem.ServerPluginLoader?.OnBeforeRegEvent(sender, eventArgs);
            FunGameSystem.WebAPIPluginLoader?.OnBeforeRegEvent(sender, eventArgs);
            if (eventArgs.Cancel)
            {
                msg = GetPluginCancelString(DataRequestType.Reg_Reg, eventArgs);
                ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                return (eventArgs.EventMsg, RegInvokeType.None, false);
            }

            using SQLHelper? sqlHelper = Factory.OpenFactory.GetSQLHelper();
            using MailSender? mailSender = Factory.OpenFactory.GetMailSender();

            if (sqlHelper != null)
            {
                (msg, type, success) = ProcessRegistration(sqlHelper, mailSender, username, password, email, verifyCode, clientIP, clientName);
            }
            else
            {
                msg = "服务器无法处理您的注册，注册失败！";
            }

            eventArgs.Success = success;
            FunGameSystem.ServerPluginLoader?.OnAfterRegEvent(sender, eventArgs);
            FunGameSystem.WebAPIPluginLoader?.OnAfterRegEvent(sender, eventArgs);

            return (msg, type, success);
        }

        internal static (string Msg, RegInvokeType Type, bool Success) ProcessRegistration(SQLHelper sqlHelper, MailSender? mailSender, string username, string password, string email, string verifyCode, string clientIP, string clientName)
        {
            if (string.IsNullOrWhiteSpace(verifyCode))
            {
                return HandleNoVerifyCode(sqlHelper, mailSender, username, email, clientName);
            }
            else
            {
                return HandleWithVerifyCode(sqlHelper, username, password, email, verifyCode, clientIP, clientName);
            }
        }

        internal static (string Msg, RegInvokeType Type, bool Success) HandleNoVerifyCode(SQLHelper sqlHelper, MailSender? mailSender, string username, string email, string clientName)
        {
            // 先检查账号是否重复
            sqlHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(sqlHelper, username));
            if (sqlHelper.Result == SQLResult.Success)
            {
                ServerHelper.WriteLine(clientName + " 账号已被注册");
                return ("此账号名已被使用！", RegInvokeType.DuplicateUserName, false);
            }

            // 检查邮箱是否重复
            sqlHelper.ExecuteDataSet(UserQuery.Select_IsExistEmail(sqlHelper, email));
            if (sqlHelper.Result == SQLResult.Success)
            {
                ServerHelper.WriteLine(clientName + " 邮箱已被注册");
                return ("此邮箱已被注册！", RegInvokeType.DuplicateEmail, false);
            }

            // 检查验证码是否发送过
            sqlHelper.ExecuteDataSet(RegVerifyCodes.Select_HasSentRegVerifyCode(sqlHelper, username, email));
            if (sqlHelper.Result == SQLResult.Success && DateTime.TryParse(sqlHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime].ToString(), out DateTime RegTime) && (DateTime.Now - RegTime).TotalMinutes < 10)
            {
                string RegVerifyCode = sqlHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode].ToString() ?? "";
                ServerHelper.WriteLine(clientName + $" 十分钟内已向{email}发送过验证码：{RegVerifyCode}");
                return ("", RegInvokeType.InputVerifyCode, false);
            }

            // 发送验证码
            return SendVerificationCode(sqlHelper, mailSender, username, email, clientName);
        }

        internal static (string Msg, RegInvokeType Type, bool Success) SendVerificationCode(SQLHelper sqlHelper, MailSender? mailSender, string username, string email, string clientName)
        {
            sqlHelper.NewTransaction();
            sqlHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(sqlHelper, username, email));
            string regVerify = Verification.CreateVerifyCode(VerifyCodeType.NumberVerifyCode, 6);
            sqlHelper.Execute(RegVerifyCodes.Insert_RegVerifyCode(sqlHelper, username, email, regVerify));

            if (sqlHelper.Result == SQLResult.Success)
            {
                sqlHelper.Commit();

                if (mailSender != null)
                {
                    // 发送验证码
                    string ServerName = Config.ServerName;
                    string Subject = $"[{ServerName}] 注册验证码";
                    string Body = $"亲爱的 {username}， <br/>    感谢您注册 [{ServerName}]，您的验证码是 {regVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.LongDateOnly)}";
                    string[] To = [email];
                    if (mailSender.Send(mailSender.CreateMail(Subject, Body, System.Net.Mail.MailPriority.Normal, true, To)) == MailSendResult.Success)
                    {
                        ServerHelper.WriteLine(clientName + $" 已向{email}发送验证码：{regVerify}");
                    }
                    else
                    {
                        ServerHelper.WriteLine(clientName + " 无法发送验证码", InvokeMessageType.Error);
                        ServerHelper.WriteLine(mailSender.ErrorMsg, InvokeMessageType.Error);
                    }
                }
                else // 不使用MailSender的情况
                {
                    ServerHelper.WriteLine(clientName + $" 验证码为：{regVerify}，请服务器管理员告知此用户");
                }
                return ("", RegInvokeType.InputVerifyCode, false);
            }
            else
            {
                sqlHelper.Rollback();
                return ("发送验证码失败！", RegInvokeType.None, false);
            }
        }

        internal static (string Msg, RegInvokeType Type, bool Success) HandleWithVerifyCode(SQLHelper sqlHelper, string username, string password, string email, string verifyCode, string clientIP, string clientName)
        {
            // 先检查验证码
            sqlHelper.ExecuteDataSet(RegVerifyCodes.Select_RegVerifyCode(sqlHelper, username, email, verifyCode));
            if (sqlHelper.Result == SQLResult.Success)
            {
                if (!DateTime.TryParse(sqlHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime].ToString(), out DateTime RegTime))
                {
                    RegTime = General.DefaultTime;
                }

                // 检查验证码是否过期
                if ((DateTime.Now - RegTime).TotalMinutes >= 10)
                {
                    ServerHelper.WriteLine(clientName + " 验证码已过期");
                    sqlHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(sqlHelper, username, email));
                    return ("此验证码已过期，请重新注册。", RegInvokeType.None, false);
                }

                // 注册
                return RegisterUser(sqlHelper, username, password, email, clientIP);
            }
            else if (sqlHelper.Result == SQLResult.NotFound)
            {
                return ("验证码不正确，请重新输入！", RegInvokeType.None, false);
            }
            else
            {
                return ("服务器无法处理您的注册，注册失败！", RegInvokeType.None, false);
            }
        }

        internal static (string Msg, RegInvokeType Type, bool Success) RegisterUser(SQLHelper sqlHelper, string username, string password, string email, string clientIP)
        {
            sqlHelper.NewTransaction();
            ServerHelper.WriteLine("[Reg] Username: " + username + " Email: " + email);
            FunGameSystem.UpdateUserKey(username);
            password = password.Encrypt(FunGameSystem.GetUserKey(username));
            sqlHelper.RegisterUser(username, password, email, clientIP);

            if (sqlHelper.Result == SQLResult.Success)
            {
                sqlHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(sqlHelper, username, email));
                sqlHelper.Commit();
                return ("注册成功！请牢记您的账号与密码！", RegInvokeType.None, true);
            }
            else
            {
                sqlHelper.Rollback();
                return ("服务器无法处理您的注册，注册失败！", RegInvokeType.None, false);
            }
        }

        #endregion

        #region Login

        public static (bool Success, DataSet DataSet, string Msg, Guid Key) PreLogin(object sender, string username, string password, string autokey = "")
        {
            bool success = false;
            DataSet dsUser = new();
            string msg;
            Guid key = Guid.Empty;

            LoginEventArgs eventArgs = new(username, password, autokey);
            FunGameSystem.ServerPluginLoader?.OnBeforeLoginEvent(sender, eventArgs);
            FunGameSystem.WebAPIPluginLoader?.OnBeforeLoginEvent(sender, eventArgs);
            if (eventArgs.Cancel)
            {
                msg = GetPluginCancelString(DataRequestType.Login_Login, eventArgs);
                ServerHelper.WriteLine(msg, InvokeMessageType.DataRequest, LogLevel.Warning);
                return (success, dsUser, eventArgs.EventMsg, key);
            }

            (success, dsUser, msg, key) = ProcessLogin(username, password, autokey);

            eventArgs.Success = success;
            FunGameSystem.ServerPluginLoader?.OnAfterLoginEvent(sender, eventArgs);
            FunGameSystem.WebAPIPluginLoader?.OnAfterLoginEvent(sender, eventArgs);

            ServerHelper.WriteLine(msg, InvokeMessageType.Core);
            return (success, dsUser, msg, key);
        }

        internal static (bool Success, DataSet DataSet, string Msg, Guid Key) ProcessLogin(string username, string password, string autokey)
        {
            bool success = false;
            DataSet dsUser = new();
            string msg = "用户名或密码不正确。";
            Guid key = Guid.Empty;

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                password = password.Encrypt(FunGameSystem.GetUserKey(username));
                ServerHelper.WriteLine("[" + DataRequestSet.GetTypeString(DataRequestType.Login_Login) + "] Username: " + username);

                using SQLHelper? sqlHelper = Factory.OpenFactory.GetSQLHelper();
                if (sqlHelper != null)
                {
                    (success, dsUser, msg, key) = ValidateLogin(sqlHelper, username, password, autokey);
                }
                else
                {
                    msg = "无法连接到数据库，登录失败！";
                }
            }

            return (success, dsUser, msg, key);
        }

        internal static (bool Success, DataSet DataSet, string Msg, Guid Key) ValidateLogin(SQLHelper sqlHelper, string username, string password, string autokey)
        {
            bool success = false;
            DataSet dsUser = new();
            string msg = "用户名或密码不正确。";
            Guid key = Guid.NewGuid();

            sqlHelper.ExecuteDataSet(UserQuery.Select_Users_LoginQuery(sqlHelper, username, password));
            if (sqlHelper.Result == SQLResult.Success)
            {
                dsUser = sqlHelper.DataSet;
                success = true;
                msg = "";

                if (!string.IsNullOrWhiteSpace(autokey))
                {
                    (success, msg) = CheckAutoKey(sqlHelper, username, autokey);
                }
            }

            if (success)
            {
                // 检查重复登录

            }

            return (success, dsUser, msg, key);
        }

        internal static (bool Success, string Msg) CheckAutoKey(SQLHelper sqlHelper, string username, string autokey)
        {
            sqlHelper.ExecuteDataSet(UserQuery.Select_CheckAutoKey(sqlHelper, username, autokey));
            if (sqlHelper.Result == SQLResult.Success)
            {
                ServerHelper.WriteLine("[" + DataRequestSet.GetTypeString(DataRequestType.Login_Login) + "] AutoKey: 已确认");
                return (true, "");
            }
            else
            {
                string msg = "AutoKey 不正确，拒绝自动登录！";
                ServerHelper.WriteLine("[" + DataRequestSet.GetTypeString(DataRequestType.Login_Login) + "] " + msg);
                return (false, msg);
            }
        }

        #endregion
    }
}
