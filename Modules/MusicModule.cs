using System.Diagnostics;
using CliWrap;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Services.ApplicationCommands;
using Serilog;

namespace PyraBot.Modules;

public class MusicModule : ApplicationCommandModule<SlashCommandContext>
{
    private const long GuildId = 943394644786561064;
    private static readonly Dictionary<ulong, VoiceInstance> voiceInstances = new();

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

    private async Task JoinAsync(bool silent = false, VoiceGuildChannel? channel = null)
    {
        if (channel is null)
            if (Context.Guild!.VoiceStates.TryGetValue(Context.User.Id, out var userVoiceState))
                channel = (Context.Guild.Channels[userVoiceState.ChannelId.GetValueOrDefault()] as VoiceGuildChannel)!;
            else
                throw new("You aren't connected to a voice channel!");

        var userChannelId = channel.Id;
        if (voiceInstances.ContainsKey(Context.Guild!.Id))
            throw new("I'm already connected to a voice channel!");
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
        if (silent) return;
        await RespondAsync(InteractionCallback.ChannelMessageWithSource
            ($"Joined you in <#{userChannelId}>!"));
    }
    [SlashCommand("join", "Tell me to join a voice channel.", GuildId = GuildId)]
    public async Task Join(
        [
            SlashCommandParameter
            (
                Name = "channel",
                Description = "Channel to join, if you're not in one."
            )
        ] VoiceGuildChannel? channel = null
    )
        => await JoinAsync(false, channel);

    [SlashCommand("leave", "Tell me to leave the voice channel.", GuildId = GuildId)]
    public async Task Leave()
    {
        if (!voiceInstances.TryGetValue(Context.Guild!.Id, out var voiceInstance))
            throw new("I'm not connected to a voice channel!");
        await Context.Client.UpdateVoiceStateAsync(new(Context.Guild.Id, null));
        await voiceInstance.VoiceClient.CloseAsync();
        voiceInstances.Remove(Context.Guild.Id);
        await RespondAsync(InteractionCallback.ChannelMessageWithSource("Left the voice channel!"));
    }

    [SlashCommand("play", "Play some fire beats in your voice channel!", GuildId = GuildId)]
    public async Task Play(
        [
            SlashCommandParameter
            (
                Name = "query",
                Description = "Text to search with"
            )
        ] string query
    )
    {
        if (!voiceInstances.ContainsKey(Context.Guild!.Id))
            await JoinAsync(true);
        await RespondAsync(InteractionCallback.ChannelMessageWithSource($"Playing \"**{query}**\"!"));

        var voiceInstance = voiceInstances[Context.Guild!.Id];
        var outputStream = voiceInstance.VoiceClient.CreateOutputStream();
        var opusStream = new OpusEncodeStream(outputStream, VoiceChannels.Stereo, OpusApplication.Audio);

        await (
            Cli.Wrap("yt-dlp")
                .WithArguments($"ytsearch1:\"{query}\" -o -")
                .WithStandardInputPipe(PipeSource.FromStream(Stream.Null))
                .WithStandardErrorPipe(PipeTarget.ToStream(Stream.Null))
                .WithValidation(CommandResultValidation.None)
            |
            Cli.Wrap("ffmpeg")
                .WithArguments("-i pipe: -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardErrorPipe(PipeTarget.ToStream(Stream.Null))
                .WithValidation(CommandResultValidation.None)
            |
            PipeTarget.ToStream(opusStream, false)
        ).ExecuteAsync();

        await opusStream.FlushAsync();
        await opusStream.DisposeAsync();
    }

    private struct VoiceInstance
    {
        public VoiceClient VoiceClient { get; }
        public ulong ChannelId { get; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        public VoiceInstance(VoiceClient voiceClient, ulong channelId)
        {
            VoiceClient = voiceClient;
            ChannelId = channelId;
        }
    }
}
