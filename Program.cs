using System.Reflection;
using System.Text.Json;
using NetCord;
using NetCord.Gateway;
using NetCord.Services.ApplicationCommands;
using PyraBot.Models;

namespace PyraBot;

internal class Program
{
    private static Configuration Configuration
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
                throw new InvalidOperationException("Configuration could not be loaded.");
            return configuration;
        }
    }
    private static async Task Main()
    {
        var configuration = Configuration;
        var client = new GatewayClient(
            new Token(TokenType.Bot, configuration.Token!),
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

        var applicationCommandService = new ApplicationCommandService<SlashCommandContext>();
        applicationCommandService.AddModules(Assembly.GetEntryAssembly()!);

        client.InteractionCreate += async interaction =>
        {
            if (interaction is not SlashCommandInteraction slashCommandInteraction)
                return;
            try
            {
                await applicationCommandService.ExecuteAsync(new SlashCommandContext(slashCommandInteraction, client));
            }
            catch (Exception exception)
            {
                await interaction.SendResponseAsync(InteractionCallback.ChannelMessageWithSource($"Error: {exception.Message}"));
            }
        };

        await client.StartAsync();
        await client.ReadyAsync;
        await applicationCommandService.CreateCommandsAsync(client.Rest, client.ApplicationId!.Value);

        await Task.Delay(-1);
    }
}
