using System.Reflection;
using System.Text.Json;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using PyraBot.Models;

namespace PyraBot;

internal class PyraBot
{
    internal static Configuration Configuration
    {
        get
        {
            var configuration = JsonSerializer.Deserialize<Configuration>(
                File.ReadAllText("configuration.json"),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;

            if (
                configuration
                    .GetType()
                    .GetProperties()
                    .Any(property => property.GetValue(configuration) is null)
            )
                throw new("Configuration could not be loaded.");
            return configuration;
        }
    }
    private static async Task Main()
    {
        var client = new GatewayClient(
            new(TokenType.Bot, Configuration.Token!),
            new()
            {
                Intents = GatewayIntents.AllNonPrivileged
            }
        );

        client.Log += message =>
        {
            Console.WriteLine(message);
            return default;
        };

        await
            (
                await client.Rest
                    .GetGuildAsync((ulong)Configuration.GuildID!)
            )
            .BulkOverwriteApplicationCommandsAsync(575622700001918976, new List<ApplicationCommandProperties>(), new());

        var slashCommandService = new ApplicationCommandService<SlashCommandContext>();
        var messageCommandService = new ApplicationCommandService<MessageCommandContext>();

        ApplicationCommandServiceManager manager = new();
        manager.AddService(slashCommandService);
        manager.AddService(messageCommandService);

        var assembly = Assembly.GetEntryAssembly()!;
        slashCommandService.AddModules(assembly);
        messageCommandService.AddModules(assembly);

        await client.StartAsync();
        await client.ReadyAsync;
        await manager.CreateCommandsAsync(client.Rest, client.ApplicationId!.Value);

        client.InteractionCreate += async interaction =>
        {
            try
            {
                await (
                    interaction switch
                    {
                        SlashCommandInteraction slashCommandInteraction => slashCommandService.ExecuteAsync(new(slashCommandInteraction, client)),
                        MessageCommandInteraction messageCommandInteraction => messageCommandService.ExecuteAsync(new(messageCommandInteraction, client)),
                        _ => throw new("Invalid interaction.")
                    }
                );
            }
            catch (Exception exception)
            {
                await interaction.SendResponseAsync(InteractionCallback.ChannelMessageWithSource($"Error: {exception.Message}"));
            }
        };

        await Task.Delay(-1);
    }
}
