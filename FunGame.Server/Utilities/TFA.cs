using Milimoe.FunGame.Server.Model;

namespace Milimoe.FunGame.Server.Utility
{
    public class TFA : Core.Api.Utility.TFA
    {
        public override bool IsAvailable(string username)
        {
            return true;
        }
    }
}
