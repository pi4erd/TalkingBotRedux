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
    private readonly TalkingInteractionService _interactionService;
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
        TalkingInteractionService interactionService,
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
        _client.InteractionCreated += _interactionService.InteractionCreated;
        _client.ShardReady += ShardReady;
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
        _client.InteractionCreated -= _interactionService.InteractionCreated;
        _client.ShardReady -= ShardReady;
        _client.Log -= Log;

        await _client
            .StopAsync()
            .ConfigureAwait(false);
    }

    public async Task ShardReady(DiscordSocketClient client) {
        await _interactionService.SetupInteractions(client);
        
        _logger.LogInformation("Shard for {} is ready!", client.Guilds.First().Name);
        await client.SetActivityAsync(new Game(
            "Nothing",
            ActivityType.Listening
        ));
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

    public static DiscordSocketClient? GetShard(DiscordShardedClient client, ulong? guildId) {
        if(guildId is null) return null;
        IGuild? guild = client.GetGuild(guildId.Value);

        if(guild is null) return null;
        return client.GetShardFor(guild);
    }
}
