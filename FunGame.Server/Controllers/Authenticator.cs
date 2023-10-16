using Milimoe.FunGame.Core.Api.Transmittal;

namespace Milimoe.FunGame.Server.Controllers
{
    public class Authenticator : Core.Library.Common.Architecture.Authenticator
    {
        public Authenticator(SQLHelper SQLHelper) : base(SQLHelper) { }
    }
}
