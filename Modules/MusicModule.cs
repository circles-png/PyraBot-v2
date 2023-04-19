using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Services.ApplicationCommands;
using Serilog;

namespace PyraBot.Modules;

public class MusicModule : ApplicationCommandModule<SlashCommandContext>
{
    private record VoiceInstance(VoiceClient VoiceClient, ulong ChannelId);
    private static Dictionary<ulong, VoiceInstance> voiceInstances = new();

    private static async Task<VoiceClient> JoinAsync(GatewayClient client, ulong guildId, ulong channelId)
    {
        var stateTaskCompletionSource = new TaskCompletionSource<VoiceState>();
        var serverTaskCompletionSource = new TaskCompletionSource<VoiceServerUpdateEventArgs>();

        client.VoiceStateUpdate += HandleVoiceStateUpdateAsync;
        client.VoiceServerUpdate += HandleVoiceServerUpdateAsync;

        await client.UpdateVoiceStateAsync(new(guildId, channelId));

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

        voiceClient.Disconnected += (willReconnect) =>
        {
            if (!willReconnect)
                voiceInstances.Remove(guildId);
            return default;
        };

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

    [SlashCommand("join", "Tell me to join your voice channel.", GuildId = 943394644786561064)]
    public async Task Join(VoiceGuildChannel? channel = null)
    {
        if (channel is null)
            if (Context.Guild!.VoiceStates.TryGetValue(Context.User.Id, out var userVoiceState))
                channel = (Context.Guild.Channels[userVoiceState.ChannelId.GetValueOrDefault()] as VoiceGuildChannel)!;
            else
                throw new("You aren't connected to a voice channel!");

        var userChannelId = channel.Id;
        if (voiceInstances.ContainsKey(Context.Guild!.Id))
            throw new("I'm already connected to a voice channel!");
        else
            voiceInstances.Add(
                Context.Guild!.Id,
                new(
                    await JoinAsync(
                        Context.Client,
                        Context.Guild.Id,
                        userChannelId
                    ),
                    userChannelId
                )
            );
        await RespondAsync(InteractionCallback.ChannelMessageWithSource
            ($"Joined you in <#{userChannelId}>!"));
    }

    [SlashCommand("leave", "Tell me to leave the voice channel.", GuildId = 943394644786561064)]
    public async Task Leave()
    {
        if (!voiceInstances.TryGetValue(Context.Guild!.Id, out var voiceInstance))
            throw new("I'm not connected to a voice channel!");
        await Context.Client.UpdateVoiceStateAsync(new(Context.Guild.Id, null));
        await voiceInstance.VoiceClient.CloseAsync();
        voiceInstances.Remove(Context.Guild.Id);
        await RespondAsync(InteractionCallback.ChannelMessageWithSource("Left the voice channel!"));
    }
}
