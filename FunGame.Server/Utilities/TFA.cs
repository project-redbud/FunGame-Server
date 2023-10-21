using Milimoe.FunGame.Core.Api.Utility;

namespace Milimoe.FunGame.Server.Utility
{
    public class TFA : TwoFactorAuthenticator
    {
        public override bool IsAvailable(string username)
        {
            return true;
        }
    }
}
