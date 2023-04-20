using System.Reflection;
using System.Text.Json;
using NetCord;
using NetCord.Gateway;
using NetCord.Services.ApplicationCommands;
using PyraBot.Models;
using Serilog;

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
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("Logs/PyraBot.log")
            .MinimumLevel.Verbose()
            .CreateLogger();

        var client = new GatewayClient(
            new(TokenType.Bot, Configuration.Token!),
            new()
            {
                Intents = GatewayIntents.AllNonPrivileged
            }
        );

        client.Log += message =>
        {
            Log.Verbose(message.ToString());
            return default;
        };

        var slashCommandService = new ApplicationCommandService<SlashCommandContext>();

        ApplicationCommandServiceManager manager = new();
        manager.AddService(slashCommandService);

        var assembly = Assembly.GetEntryAssembly()!;
        slashCommandService.AddModules(assembly);

        await client.StartAsync();
        await client.ReadyAsync;
        await manager.CreateCommandsAsync(client.Rest, client.ApplicationId!.Value, true);

        client.InteractionCreate += async interaction =>
        {
            try
            {
                await (
                    interaction switch
                    {
                        SlashCommandInteraction slashCommandInteraction => slashCommandService.ExecuteAsync(new(slashCommandInteraction, client)),
                        _ => throw new("Invalid interaction.")
                    }
                );
            }
            catch (Exception exception)
            {
                await interaction.SendResponseAsync(InteractionCallback.ChannelMessageWithSource($"uhm acktually {exception.Message.ToLower()}"));
            }
        };

        await Task.Delay(-1);
    }
}
