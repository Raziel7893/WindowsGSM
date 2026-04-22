using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WindowsGSM.DiscordBot
{
	class Bot
	{
		private DiscordSocketClient _client;
		private string _donorType;
		private CancellationTokenSource _cancellationTokenSource;
		private readonly IServiceProvider _serviceProvider = CreateServices();
		private Interactions _interactions;

		public Bot()
		{
			Configs.CreateConfigs();
		}

        public async Task<bool> Start()
        {
            // Always create a new client instance
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages |
                                 GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessageTyping |
                                 GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions |
                                 GatewayIntents.DirectMessageTyping | GatewayIntents.MessageContent,
                UseInteractionSnowflakeDate = false
            };
            _client = new DiscordSocketClient(config);
            _client.Ready += On_Bot_Ready;

            try
            {
                await _client.LoginAsync(TokenType.Bot, Configs.GetBotToken());
                await _client.StartAsync();
            }
            catch
            {
                return false;
            }

            // Listen Commands
            new Commands(_client);
            _interactions = new Interactions(_client, _serviceProvider);

            return true;
        }

        private async Task On_Bot_Ready()
        {
            // Set bot avatar and username
            try
            {
                Stream stream = DiscordBot.Configs.GetBotCustomImage();
                if (stream == null)
                    stream = Application.GetResourceStream(
                        new Uri($"pack://application:,,,/Images/WindowsGSM{(string.IsNullOrWhiteSpace(_donorType) ? string.Empty : $"-{_donorType}")}.png")
                    ).Stream;

                await _client.CurrentUser.ModifyAsync(x =>
                {
                    x.Username = DiscordBot.Configs.GetBotName();
                    x.Avatar = new Image(stream);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set bot avatar/username: {ex}");
            }

            // Set initial presence
            int serverCount = 0;
            if (Application.Current != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                    serverCount = WindowsGSM.ServerGrid.Items.Count;
                });
            }
            await _client.SetGameAsync($"{serverCount} game server{(serverCount != 1 ? "s" : string.Empty)}");

            // Start the background update loop
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => StartDiscordPresenceUpdate(_cancellationTokenSource.Token));
        }

        private async Task StartDiscordPresenceUpdate(CancellationToken token)
        {
            while (_client != null && _client.CurrentUser != null && !token.IsCancellationRequested)
            {
                int serverCount = 0;
                if (Application.Current != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MainWindow WindowsGSM = (MainWindow)Application.Current.MainWindow;
                        serverCount = WindowsGSM.ServerGrid.Items.Count;
                    });
                }

                await _client.SetGameAsync($"{serverCount} game server{(serverCount != 1 ? "s" : string.Empty)}");

                try
                {
                    await Task.Delay(900000, token); // 15 minutes or until cancelled
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public void SetDonorType(string donorType)
		{
			_donorType = donorType;
		}

        public async Task Stop()
        {
            if (_client != null)
            {
                try
                {
                    _cancellationTokenSource?.Cancel();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"{e.Message}");
                }

                _client.Ready -= On_Bot_Ready;

                try
                {
                    await _client.StopAsync();
                    await _client.LogoutAsync();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"{e.Message}");
                }

                _client.Dispose();
                _client = null;

            }
        }

        public string GetInviteLink()
		{
			return (_client == null || _client.CurrentUser == null) ? string.Empty : $"https://discordapp.com/api/oauth2/authorize?client_id={_client.CurrentUser.Id}&permissions=67497024&scope=bot%20applications.commands";
		}
		
		static IServiceProvider CreateServices()
		{
			var config = new DiscordSocketConfig()
			{
				GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages |
				                 GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessageTyping |
				                 GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions |
				                 GatewayIntents.DirectMessageTyping | GatewayIntents.MessageContent,
				// Prevent snowflake date from being used in the interaction service
				UseInteractionSnowflakeDate = false
			};

			return new ServiceCollection()
				.AddSingleton(new DiscordSocketClient(config))
				.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
				.BuildServiceProvider();
		}
	}
}
