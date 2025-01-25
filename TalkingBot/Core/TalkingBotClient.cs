using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TalkingBot.Core;

public class TalkingBotClient : IHostedService
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly TalkingBotConfig _config;
    private readonly ILogger<TalkingBotClient> _logger;

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
        _discordSocketClient.Ready -= Ready;

        await _discordSocketClient
            .StopAsync()
            .ConfigureAwait(false);
    }

    public async Task Ready() {
        await SetupInteractions()
            .ConfigureAwait(false);
        
        _logger.LogInformation("Client {0} is ready!", _discordSocketClient);
    }

    private Task InteractionExecuted(ICommandInfo command, IInteractionContext context, IResult result) {
        _logger.LogDebug("@{} executed command '{0}'.", context.User.Username, command.Name);

        return Task.CompletedTask;
    }

    private async Task SetupInteractions() {
        await _interactionService
            .AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider)
            .ConfigureAwait(false);
        
        foreach(var guildId in _config.Guilds) {
            var guild = _discordSocketClient.GetGuild(guildId);

            if(guild is null) {
                _logger.LogWarning("Guild id {0} wasn't found", guildId);
                continue;
            }

            await _interactionService.RegisterCommandsToGuildAsync(guildId);

            _logger.LogInformation("Built commands for guild {0}.", guild.Name);

        }
    }

    public Task InteractionCreated(SocketInteraction interaction) {
        var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);
        return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
    }
}
