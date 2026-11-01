namespace RealtimeChat.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsOnline { get; set; } = false;

    public string ProfileImage { get; set; } = "";
}
