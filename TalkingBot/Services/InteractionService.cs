using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Modules;

namespace TalkingBot.Services;

public class TalkingInteractionService : IDisposable {
    private readonly InteractionService _interactionService;
    private readonly ILogger<TalkingInteractionService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TalkingBotConfig _tbConfig;
    private bool _modulesLoaded = false;
    public TalkingInteractionService(
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        ILogger<TalkingInteractionService> logger,
        TalkingBotConfig tbConfig
    ) {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _interactionService = interactionService;
        _tbConfig = tbConfig;

        _interactionService.InteractionExecuted += InteractionExecuted;
    }

    public void Dispose()
    {
        _interactionService.InteractionExecuted -= InteractionExecuted;
    }

    public async Task SetupInteractions(DiscordSocketClient client) {
        if(!_modulesLoaded) {
            await _interactionService.AddModuleAsync<AudioModule>(_serviceProvider);
            await _interactionService.AddModuleAsync<GeneralModule>(_serviceProvider);
            await _interactionService.AddModuleAsync<ButtonModule>(_serviceProvider);
            await _interactionService.AddModuleAsync<GameModule>(_serviceProvider);
            _modulesLoaded = true;
        }

        var guild = client.Guilds.First();
        
        if(guild is null) {
            _logger.LogWarning("Shard {} didn't have guilds!", client);
            return;
        }

        if(_tbConfig.ClearCommands) {
            await guild.DeleteApplicationCommandsAsync(RequestOptions.Default);
            _logger.LogDebug("Deleted all commands for guild {}.", guild.Name);
            return;
        }

        await _interactionService.RegisterCommandsToGuildAsync(guild.Id, true);

        _logger.LogDebug("Built commands for guild {}.", guild.Name);
    }

    public Task InteractionCreated(SocketInteraction interaction) {
        DiscordShardedClient _client = _serviceProvider.GetRequiredService<DiscordShardedClient>();
        DiscordSocketClient? client = TalkingBotClient.GetShard(_client, interaction.GuildId);
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
}
