namespace Milimoe.FunGame.WebAPI.Models
{
    public class LoginDTO
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class RegDTO
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Email { get; set; } = "";
        public string VerifyCode { get; set; } = "";
    }
}
