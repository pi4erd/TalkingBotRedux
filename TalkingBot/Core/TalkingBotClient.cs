using System.Reflection;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace TalkingBot.Core;

public class TalkingBotClient : IHostedService
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;

    public TalkingBotClient(
        DiscordSocketClient discordSocketClient,
        InteractionService interactionService,
        IServiceProvider serviceProvider
    ) {
        ArgumentNullException.ThrowIfNull(discordSocketClient);
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _discordSocketClient = discordSocketClient;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordSocketClient.InteractionCreated += InteractionCreated;
        _discordSocketClient.Ready += Ready;

        await _discordSocketClient
            .LoginAsync(Discord.TokenType.Bot, "")
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
        await _interactionService
            .AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider)
            .ConfigureAwait(false);
        
        // TODO: Setup commands
    }

    public Task InteractionCreated(SocketInteraction interaction) {
        var interactionContext = new SocketInteractionContext(_discordSocketClient, interaction);
        return _interactionService.ExecuteCommandAsync(interactionContext, _serviceProvider);
    }
}
