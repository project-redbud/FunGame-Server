using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.Server.Services
{
    public class TFA : TwoFactorAuthenticator
    {
        public override bool IsAvailable(string username)
        {
            return true;
        }
    }
}
