using Discord;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Extensions;
using Lavalink4NET.Protocol.Payloads.Events;
using Microsoft.Extensions.Logging;
using TalkingBot.Core;

namespace TalkingBot.Services;

public class AudioEventListener : IDisposable {
    private readonly IAudioService _audioService;
    private readonly DiscordShardedClient _client;
    private readonly ILogger<AudioEventListener> _logger;

    public AudioEventListener(
        IAudioService audioService,
        DiscordShardedClient client,
        ILogger<AudioEventListener> logger
    ) {
        _audioService = audioService;
        _client = client;
        _logger = logger;

        _logger.LogInformation("Registering AudioEventListener");

        _audioService.TrackStarted   += OnTrackStarted;
        _audioService.TrackEnded     += OnTrackEnd;
        _audioService.TrackException += OnTrackException;
    }

    public async Task OnTrackStarted(object sender, TrackStartedEventArgs args) {
        _logger.LogDebug("Track started: {}", args.Track.Title);

        DiscordSocketClient? client = TalkingBotClient.GetShard(_client, args.Player.GuildId);
        if(client is null) {
            _logger.LogWarning("Guild was invalid when track ended: {}.", args.Player.GuildId);
            return;
        }

        await client.SetActivityAsync(new Game(
            args.Track.Title,
            ActivityType.Listening,
            details: args.Track.Uri?.ToString()
        ));
    }

    public Task OnTrackException(object sender, TrackExceptionEventArgs args) {
        _logger.LogWarning("Track exception occured: {}", args.Exception);
        return Task.CompletedTask;
    }

    public async Task OnTrackEnd(object sender, TrackEndedEventArgs args) {
        _logger.LogDebug("Track ended: {}", args.Track.Title);
        
        DiscordSocketClient? client = TalkingBotClient.GetShard(_client, args.Player.GuildId);
        if(client is null) {
            _logger.LogWarning("Guild was invalid when track ended: {}.", args.Player.GuildId);
            return;
        }

        if(args.Reason == TrackEndReason.Stopped && !args.MayStartNext) {
            await client.SetActivityAsync(new Game(
                "Nothing",
                ActivityType.Listening
            ));
        }
    }

    public void Dispose()
    {
        _audioService.TrackStarted -= OnTrackStarted;
        _audioService.TrackEnded   -= OnTrackEnd;
        GC.SuppressFinalize(this);
    }
}
