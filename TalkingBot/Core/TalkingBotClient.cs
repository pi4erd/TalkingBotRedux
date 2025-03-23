using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
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

    private readonly DiscordShardedClient _client;
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
        DiscordShardedClient discordSocketClient,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        TalkingBotConfig config,
        ILogger<TalkingBotClient> logger
    ) {
        ArgumentNullException.ThrowIfNull(discordSocketClient);
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _client = discordSocketClient;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;

        // Initialize unused directly, but necessary services
        _ = _serviceProvider.GetRequiredService<MessageEventListener>();
        _ = _serviceProvider.GetRequiredService<AudioEventListener>();
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.InteractionCreated += InteractionCreated;
        _client.ShardReady += ShardReady;
        _interactionService.InteractionExecuted += InteractionExecuted;
        _client.Log += Log;

        // var audioService = _serviceProvider.GetService<IAudioService>();

        await _client
            .LoginAsync(TokenType.Bot, _config.Token)
            .ConfigureAwait(false);

        await _client
            .StartAsync()
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.InteractionCreated -= InteractionCreated;
        _interactionService.InteractionExecuted -= InteractionExecuted;
        _client.ShardReady -= ShardReady;
        _client.Log -= Log;

        await _client
            .StopAsync()
            .ConfigureAwait(false);
    }

    public async Task ShardReady(DiscordSocketClient client) {
        await SetupInteractions();
        
        _logger.LogInformation("Shard for {} is ready!", client.Guilds.First().Name);
        await client.SetActivityAsync(new Game(
            "Nothing",
            ActivityType.Listening
        ));
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
            var guild = _client.GetGuild(guildId);

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
        DiscordSocketClient? client = GetShard(_client, interaction.GuildId);
        if(client is null) {
            _logger.LogWarning("Interaction from unsharded guild received: {}.", interaction.GuildId);
            return Task.CompletedTask;
        }

        if(interaction is SocketMessageComponent component) {
            var componentContext = new SocketInteractionContext<SocketMessageComponent>(
                client,
                component
            );
            return _interactionService.ExecuteCommandAsync(componentContext, _serviceProvider);
        }

        var interactionContext = new SocketInteractionContext(client, interaction);
        return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
    }

    public static DiscordSocketClient? GetShard(DiscordShardedClient client, ulong? guildId) {
        if(guildId is null) return null;
        IGuild? guild = client.GetGuild(guildId.Value);

        if(guild is null) return null;
        return client.GetShardFor(guild);
    }
}
