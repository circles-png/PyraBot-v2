namespace PyraBot.Models;

[Serializable]
public class Configuration
{
    public string? Token { get; set; }
    public string? InviteLink { get; set; }
    public ulong? GuildId { get; set; }
    public string? Activity { get; set; }
    public ulong? OwnerId { get; set; }
}
