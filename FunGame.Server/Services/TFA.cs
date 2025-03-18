using Milimoe.FunGame.Core.Api.Transmittal;
using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.Server.Services
{
    public class TFA(SQLHelper SQLHelper) : TwoFactorAuthenticator(SQLHelper)
    {
        public override bool IsAvailable(string username)
        {
            return true;
        }
    }
}
