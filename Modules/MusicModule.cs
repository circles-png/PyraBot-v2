using System.ComponentModel;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Services.ApplicationCommands;

namespace PyraBot.Modules;

public class MusicModule : ApplicationCommandModule<SlashCommandContext>
{
    private static VoiceClient? voiceClient;
    private static ulong? connectedChannelId;
    private static async Task<VoiceClient> JoinAsync(GatewayClient client, ulong guildId, ulong channelId)
    {
        var stateTaskCompletionSource = new TaskCompletionSource<VoiceState>();
        var serverTaskCompletionSource = new TaskCompletionSource<VoiceServerUpdateEventArgs>();

        client.VoiceStateUpdate += HandleVoiceStateUpdateAsync;
        client.VoiceServerUpdate += HandleVoiceServerUpdateAsync;

        await client.UpdateVoiceStateAsync(new(guildId, channelId));
        connectedChannelId = channelId;

        var timeout = TimeSpan.FromSeconds(1);
        VoiceState state;
        VoiceServerUpdateEventArgs server;
        try
        {
            state = await stateTaskCompletionSource.Task.WaitAsync(timeout);
            server = await serverTaskCompletionSource.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            throw new($"Failed to join <#{channelId}> within {timeout.TotalSeconds} seconds!");
        }

        var voiceClient = new VoiceClient(
            state.UserId,
            state.SessionId,
            server.Endpoint!,
            server.GuildId,
            server.Token,
            new()
            {
                RedirectInputStreams = true,
            }
        );

        await voiceClient.StartAsync();
        await voiceClient.ReadyAsync;
        await voiceClient.EnterSpeakingStateAsync(SpeakingFlags.Microphone);
        return voiceClient;

        ValueTask HandleVoiceStateUpdateAsync(VoiceState state)
        {
            if (state.UserId == client.User!.Id && state.GuildId == guildId)
            {
                client.VoiceStateUpdate -= HandleVoiceStateUpdateAsync;
                stateTaskCompletionSource.SetResult(state);
            }
            return default;
        }

        ValueTask HandleVoiceServerUpdateAsync(VoiceServerUpdateEventArgs updateEvent)
        {
            if (updateEvent.GuildId == guildId)
            {
                client.VoiceServerUpdate -= HandleVoiceServerUpdateAsync;
                serverTaskCompletionSource.SetResult(updateEvent);
            }
            return default;
        }
    }

    [SlashCommand("join", "Tell me to join your voice channel.")]
    public async Task Join()
    {
        if (!Context.Guild!.VoiceStates.TryGetValue(Context.User.Id, out var userVoiceState))
            throw new("You aren't connected to a voice channel!");
        if (userVoiceState.ChannelId == connectedChannelId)
            throw new("I'm already connected to that channel!");
        if (connectedChannelId is not null)
        {
            await (voiceClient ?? throw new("Connection already closed.")).CloseAsync();
            voiceClient = null;
        }
        voiceClient ??= await JoinAsync(
            Context.Client,
            Context.Guild!.Id,
            userVoiceState.ChannelId ?? throw new("I couldn't find that channel!")
        );
        voiceClient.Disconnected += async (ex) =>
        {
            await RespondAsync(InteractionCallback.ChannelMessageWithSource("Disconnected from voice!"));
            connectedChannelId = null;
        };
        await RespondAsync(InteractionCallback.ChannelMessageWithSource($"Joined you in <#{connectedChannelId}>!"));
    }
}
