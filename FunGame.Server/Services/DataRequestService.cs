using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Core.Library.SQLScript.Common;
using Milimoe.FunGame.Core.Library.SQLScript.Entity;
using Milimoe.FunGame.Server.Others;

namespace Milimoe.FunGame.Server.Services
{
    public class DataRequestService
    {
        public static (string Msg, RegInvokeType RegInvokeType, bool Success) Reg(string username, string password, string email, string verifyCode, string clientIP = "", SQLHelper? sqlHelper = null, MailSender? mailSender = null)
        {
            string msg = "";
            RegInvokeType type = RegInvokeType.None;
            bool success = false;
            string clientName = ServerHelper.MakeClientName(clientIP);

            sqlHelper ??= Factory.OpenFactory.GetSQLHelper();
            mailSender ??= Factory.OpenFactory.GetMailSender();
            if (sqlHelper != null)
            {
                // 如果没发验证码，就生成验证码
                if (verifyCode.Trim() == "")
                {
                    // 先检查账号是否重复
                    sqlHelper.ExecuteDataSet(UserQuery.Select_IsExistUsername(sqlHelper, username));
                    if (sqlHelper.Result == SQLResult.Success)
                    {
                        ServerHelper.WriteLine(clientName + " 账号已被注册");
                        msg = "此账号名已被使用！";
                        type = RegInvokeType.DuplicateUserName;
                    }
                    else
                    {
                        // 检查邮箱是否重复
                        sqlHelper.ExecuteDataSet(UserQuery.Select_IsExistEmail(sqlHelper, email));
                        if (sqlHelper.Result == SQLResult.Success)
                        {
                            ServerHelper.WriteLine(clientName + " 邮箱已被注册");
                            msg = "此邮箱已被注册！";
                            type = RegInvokeType.DuplicateEmail;
                        }
                        else
                        {
                            // 检查验证码是否发送过
                            sqlHelper.ExecuteDataSet(RegVerifyCodes.Select_HasSentRegVerifyCode(sqlHelper, username, email));
                            if (sqlHelper.Result == SQLResult.Success && DateTime.TryParse(sqlHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegTime].ToString(), out DateTime RegTime) && (DateTime.Now - RegTime).TotalMinutes < 10)
                            {
                                string RegVerifyCode = sqlHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode].ToString() ?? "";
                                ServerHelper.WriteLine(clientName + $" 十分钟内已向{email}发送过验证码：{RegVerifyCode}");
                                type = RegInvokeType.InputVerifyCode;
                            }
                            else
                            {
                                // 发送验证码，需要先删除之前过期的验证码
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
                                        string Subject = $"[{ServerName}] FunGame 注册验证码";
                                        string Body = $"亲爱的 {username}， <br/>    感谢您注册[{ServerName}]，您的验证码是 {regVerify} ，10分钟内有效，请及时输入！<br/><br/>{ServerName}<br/>{DateTimeUtility.GetDateTimeToString(TimeType.LongDateOnly)}";
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
                                    type = RegInvokeType.InputVerifyCode;
                                }
                                else sqlHelper.Rollback();
                            }
                        }
                    }
                }
                else
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
                            msg = "此验证码已过期，请重新注册。";
                            sqlHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(sqlHelper, username, email));
                            type = RegInvokeType.None;
                        }
                        else
                        {
                            // 注册
                            if (verifyCode.Equals(sqlHelper.DataSet.Tables[0].Rows[0][RegVerifyCodes.Column_RegVerifyCode]))
                            {
                                sqlHelper.NewTransaction();
                                ServerHelper.WriteLine("[Reg] Username: " + username + " Email: " + email);
                                sqlHelper.Execute(UserQuery.Insert_Register(sqlHelper, username, password, email, clientIP));
                                if (sqlHelper.Result == SQLResult.Success)
                                {
                                    success = true;
                                    msg = "注册成功！请牢记您的账号与密码！";
                                    sqlHelper.Execute(RegVerifyCodes.Delete_RegVerifyCode(sqlHelper, username, email));
                                    sqlHelper.Commit();
                                }
                                else
                                {
                                    sqlHelper.Rollback();
                                    msg = "服务器无法处理您的注册，注册失败！";
                                }
                            }
                            else msg = "验证码不正确，请重新输入！";
                        }
                    }
                    else if (sqlHelper.Result == SQLResult.NotFound) msg = "验证码不正确，请重新输入！";
                    else msg = "服务器无法处理您的注册，注册失败！";
                }
            }

            return (msg, type, success);
        }
    }
}
