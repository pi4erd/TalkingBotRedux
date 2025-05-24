using Discord;
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
    private readonly LlamaApi _llamaApi;

    private const string SYSTEM_MESSAGE = "You are Jack. You love mexican food, especially tacos. You are a " +
               "gourment with refined taste. You will always try to ask questions to the user " +
               "about their preferences in culinary endeavors. " +
               "You talk very refined, like a noble. Refer to the user as a sweet little bun. " +
               "Talk for at most 3 paragraphs. Don't use character *.";


    public MessageEventListener(
        DiscordShardedClient client,
        GameDataCacher cacher,
        ILogger<MessageEventListener> logger,
        TalkingBotConfig config,
        LlamaApi llamaApi
    )
    {
        _client = client;
        _cacher = cacher;
        _logger = logger;
        _config = config;
        _llamaApi = llamaApi;

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

    async Task<List<LlamaMessage>> ReconstructDialog(SocketMessage message)
    {
        List<LlamaMessage> messages = [];
        MessageReference? reference = new(message.Id, message.Channel.Id);

        while (reference is not null)
        {
            var refMessage = await message.Channel.GetMessageAsync(reference.MessageId.Value, CacheMode.CacheOnly);

            if (refMessage is null)
            {
                break;
            }
            
            messages.Add(new LlamaMessage()
            {
                Role = refMessage.Author.Id == _client.CurrentUser.Id ?
                    "assistant" : "user",
                Content = refMessage.Content,
            });
            reference = refMessage.Reference;
        }

        messages.Add(new LlamaMessage()
        {
            Role = "system",
            Content = SYSTEM_MESSAGE,
        });

        messages.Reverse();

        return messages;
    }

    async Task OnMentionOrReply(SocketMessage message)
    {
        IDisposable typing = message.Channel.EnterTypingState();

        List<LlamaMessage> messages = await ReconstructDialog(message).ConfigureAwait(false);
        LlamaMessage response;
        try
        {
            response = await _llamaApi.ChatComplete([.. messages]).ConfigureAwait(false);
        }
        catch (Exception)
        {
            response = new LlamaMessage()
            {
                Content = "I hope you're doing well.\n-# llama is unavailable"
            };
        }

        await message.Channel.SendMessageAsync(
            response.Content,
            messageReference: new MessageReference(message.Id, message.Channel.Id)
        ).ConfigureAwait(false);

        typing.Dispose();
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
