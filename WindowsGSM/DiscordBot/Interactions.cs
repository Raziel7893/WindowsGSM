using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace WindowsGSM.DiscordBot
{
    public class Interactions
    {
        private readonly DiscordSocketClient _client;
        private InteractionService _interactionService;
        private readonly IServiceProvider _serviceProvider;

        public Interactions(DiscordSocketClient client, IServiceProvider serviceProvider)
        {
            _client = client;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeInteractions()
        {
            _interactionService = _serviceProvider.GetRequiredService<InteractionService>();
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
            await _interactionService.RegisterCommandsGloballyAsync();

            _client.InteractionCreated += HandleInteraction;
            _interactionService.SlashCommandExecuted += SlashCommandExecuted;
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            try
            {
                await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Error executing command: {ex.Message}");
                if (!interaction.HasResponded)
                {
                    await interaction.RespondAsync("Failed to execute command", ephemeral: true);
                }
            }
        }

        private static async Task SlashCommandExecuted(SlashCommandInfo command, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess && !context.Interaction.HasResponded)
            {
                await context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
            }
        }
    }
}