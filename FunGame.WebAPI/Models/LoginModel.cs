namespace Milimoe.FunGame.WebAPI.Models
{
    public class LoginModel(string username, string password)
    {
        public string Username { get; set; } = username;
        public string Password { get; set; } = password;
    }
}
