using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;
using TalkingBot.Core.Caching;
using TalkingBot.Services;

namespace TalkingBot.Services;

public class MessageEventListener : IDisposable {
    private readonly DiscordShardedClient _client;
    private readonly GameDataCacher _cacher;
    private readonly ILogger<MessageEventListener> _logger;
    private readonly TalkingBotConfig _config;

    public MessageEventListener(
        DiscordShardedClient client,
        GameDataCacher cacher,
        ILogger<MessageEventListener> logger,
        TalkingBotConfig config
    ) {
        _client = client;
        _cacher = cacher;
        _logger = logger;
        _config = config;

        _client.MessageReceived += OnMessage;
    }

    public async Task OnMessage(SocketMessage message) {
        // TODO: Add OnMessageGame module or something like this
        if(message.Author.IsBot) {
            return;
        }

        await ProcessExperience(message);

        if(message.MentionedUsers.Where((user) => user.Id == _client.CurrentUser.Id).FirstOrDefault() != null) {
            await OnMentionOrReply(message);
        }
    }

    async Task OnMentionOrReply(SocketMessage message) {
        await message.Channel.SendMessageAsync(
            "I hope you're doing well.",
            messageReference: message.Reference
        );
    }

    async Task ProcessExperience(SocketMessage message) {
        // Experience on messages
        UserGameData gameData = _cacher.GetUserGameData(message.Author.Id);

        double secondsElapsed = (DateTime.Now - gameData.LastExpGain).TotalSeconds;

        _logger.LogDebug("User {} sent message.", message.Author.Username);

        if(secondsElapsed < _config.GameConfig.ExpGainDelaySeconds) {
            return; // skip this level gain
        }

        gameData.Experience += _config.GameConfig.ExpGain;
        gameData.LastExpGain = DateTime.Now;

        _logger.LogDebug("User {} gained {} experience.", message.Author.Username, _config.GameConfig.ExpGain);
        
        if(gameData.UpdateLevel()) {
            await message.Channel.SendMessageAsync($"{message.Author.Mention} leveled up to **{gameData.Level}**!");
        }

        _cacher.ModifyUserData(message.Author.Id, gameData);
    }

    public void Dispose()
    {
        _client.MessageReceived -= OnMessage;
        GC.SuppressFinalize(this);
    }
}
