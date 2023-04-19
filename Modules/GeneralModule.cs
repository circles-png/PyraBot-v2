using NetCord;
using NetCord.Services.ApplicationCommands;

namespace PyraBot.Modules;

public class GeneralModule : ApplicationCommandModule<SlashCommandContext>
{
    [SlashCommand("ping", "Use a high-tech timing device to measure the lag between me and Discord.")]
    public Task Ping()
    {
        return RespondAsync(InteractionCallback.ChannelMessageWithSource($"Pong!")); //todo: add latency
    }
}
