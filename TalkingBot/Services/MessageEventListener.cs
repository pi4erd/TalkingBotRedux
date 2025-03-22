using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;
using TalkingBot.Services;

namespace TalkingBot.Services;

public class MessageEventListener : IDisposable {
    private readonly DiscordSocketClient _client;
    private readonly GameDataCacher _cacher;
    private readonly ILogger<MessageEventListener> _logger;

    public MessageEventListener(
        DiscordSocketClient client,
        GameDataCacher cacher,
        ILogger<MessageEventListener> logger
    ) {
        _client = client;
        _cacher = cacher;
        _logger = logger;

        _client.MessageReceived += OnMessage;
    }

    public async Task OnMessage(SocketMessage message) {
        // TODO: Add OnMessageGame module or something like this
        if(message.Author.IsBot) {
            return;
        }

        // Experience on messages
        UserGameData gameData = _cacher.GetUserGameData(message.Author.Id);

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

    public void Dispose()
    {
        _client.MessageReceived -= OnMessage;
    }
}
