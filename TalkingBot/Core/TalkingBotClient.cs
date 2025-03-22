using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;
using TalkingBot.Modules;
using TalkingBot.Services;

namespace TalkingBot.Core;

public class TalkingBotClient : IHostedService
{
    public static string CurrentVersion { get; private set; }

    private readonly DiscordSocketClient _discordSocketClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly TalkingBotConfig _config;
    private readonly ILogger<TalkingBotClient> _logger;

    static TalkingBotClient() {
        CurrentVersion = Assembly
            .GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ??
            "unknown";
    }

    public TalkingBotClient(
        DiscordSocketClient discordSocketClient,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        TalkingBotConfig config,
        ILogger<TalkingBotClient> logger
    ) {
        ArgumentNullException.ThrowIfNull(discordSocketClient);
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _discordSocketClient = discordSocketClient;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated += InteractionCreated;
        _discordSocketClient.Ready += Ready;
        _interactionService.InteractionExecuted += InteractionExecuted;
        _discordSocketClient.Log += Log;
        _discordSocketClient.MessageReceived += OnMessage;

        await _discordSocketClient
            .LoginAsync(Discord.TokenType.Bot, _config.Token)
            .ConfigureAwait(false);

        await _discordSocketClient
            .StartAsync()
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated -= InteractionCreated;
        _interactionService.InteractionExecuted -= InteractionExecuted;
        _discordSocketClient.Ready -= Ready;
        _discordSocketClient.Log -= Log;
        _discordSocketClient.MessageReceived -= OnMessage;

        await _discordSocketClient
            .StopAsync()
            .ConfigureAwait(false);
    }

    public async Task OnMessage(SocketMessage message) {
        // TODO: Add OnMessageGame module or something like this
        if(message.Author.IsBot) {
            return;
        }

        // Experience on messages
        GameDataCacher cacher = _serviceProvider.GetRequiredService<GameDataCacher>();

        UserGameData gameData = cacher.GetUserGameData(message.Author.Id);

        double minutes_elapsed = (DateTime.Now - gameData.LastExpGain).TotalMinutes;

        _logger.LogDebug("User {} sent message.", message.Author.Username);

        if(minutes_elapsed < 1.0) {
            return; // skip this level gain
        }

        gameData.Experience += UserGameData.ExpGainPerMessage;
        gameData.LastExpGain = DateTime.Now;

        _logger.LogDebug("User {} gained {} experience.", message.Author.Username, UserGameData.ExpGainPerMessage);
        
        if(gameData.UpdateLevel()) {
            await message.Channel.SendMessageAsync($"{message.Author.Mention} leveled up to **{gameData.Level}**!");
        }
    }

    public async Task Ready() {
        await SetupInteractions();
        
        _logger.LogInformation("Client {} is ready!", _discordSocketClient);
    }

    private async Task InteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result) {
        _logger.LogDebug("@{} executed command '{}'.", context.User.Username, command.Name);

        if(!result.IsSuccess) {
            _logger.LogWarning("Error occured while executing interaction: {}.\n{}", result.Error, result.ErrorReason);

            if(context.Interaction.HasResponded) {
                await context.Interaction.FollowupAsync("Failed to execute interaction!", ephemeral: true);
            } else {
                await context.Interaction.RespondAsync("Failed to execute interaction!", ephemeral: true);
            }
        }
    }

    private async Task SetupInteractions() {
        await _interactionService.AddModuleAsync<AudioModule>(_serviceProvider);
        await _interactionService.AddModuleAsync<GeneralModule>(_serviceProvider);
        await _interactionService.AddModuleAsync<ButtonModule>(_serviceProvider);
        await _interactionService.AddModuleAsync<GameModule>(_serviceProvider);
        
        foreach(var guildId in _config.Guilds) {
            var guild = _discordSocketClient.GetGuild(guildId);

            if(guild is null) {
                _logger.LogWarning("Guild id {} wasn't found", guildId);
                continue;
            }

            if(_config.ClearCommands) {
                await guild.DeleteApplicationCommandsAsync(RequestOptions.Default);
                _logger.LogDebug("Deleted all commands for guild {}.", guild.Name);
                continue;
            }

            await _interactionService.RegisterCommandsToGuildAsync(guildId, true);

            _logger.LogDebug("Built commands for guild {}.", guild.Name);
        }

        if(_config.ClearCommands) {
            _logger.LogInformation("Stopping because deleted commands in config.");
            Environment.Exit(0);
        }

        _logger.LogInformation("Built {} commands for {} guilds.",
            _interactionService.SlashCommands.Count, _config.Guilds.Length
        );
    }

    public Task Log(LogMessage message) {
        LogLevel level = message.Severity switch {
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Debug => LogLevel.Debug,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Verbose => LogLevel.Trace,
            _ => LogLevel.None
        };

        _logger.Log(logLevel: level, message: message.Message, exception: message.Exception);
        
        return Task.CompletedTask;
    }

    public Task InteractionCreated(SocketInteraction interaction) {
        if(interaction is SocketMessageComponent component) {
            var componentContext = new SocketInteractionContext<SocketMessageComponent>(
                _discordSocketClient,
                component
            );
            return _interactionService.ExecuteCommandAsync(componentContext, _serviceProvider);
        }

        var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);
        return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
    }
}
