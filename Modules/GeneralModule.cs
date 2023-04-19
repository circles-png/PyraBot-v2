using NetCord;
using NetCord.Services.ApplicationCommands;

namespace PyraBot.Modules;

public class GeneralModule : ApplicationCommandModule<SlashCommandContext>
{
    [SlashCommand("ping", "Use a high-tech timing device to measure the lag between me and Discord.", GuildId = 943394644786561064)]
    public Task Ping()
    {
        return RespondAsync(InteractionCallback.ChannelMessageWithSource
            (
                $"""
                    Pong! {(
                            Context.Client.Latency
                                ?? throw new("I couldn't get the latency. I'm either offline or Discord is down.")
                        ).TotalMilliseconds}ms (from me to Discord).
                """
            ));
    }

    [SlashCommand("invite", "Want to invite me to your server? Use this command to invite me.", GuildId = 943394644786561064)]
    public Task Invite()
    {
        return RespondAsync(InteractionCallback.ChannelMessageWithSource
            (
                $"""
                    Invite me to your server with [this link]({PyraBot.Configuration.InviteLink}):
                    Or click my name, then click the **Add to Server** button!
                """
            ));
    }
}
