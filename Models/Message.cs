namespace RealtimeChat.Models;

public class Message
{
    public int Id { get; set; }

    public string RoomName { get; set; } = "";

    public string SenderUsername { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTime SentAt { get; set; } = DateTime.Now;
}
