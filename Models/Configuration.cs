namespace PyraBot.Models;

[Serializable]
public class Configuration
{
    public string? Token { get; set; }
    public string? InviteLink { get; set; }
    public ulong? GuildID { get; set; }
    public string? Activity { get; set; }
}
