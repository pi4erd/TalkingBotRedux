using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Options;

namespace TalkingBot.Modules;

public class AudioModule(
    IAudioService audioService
) : InteractionModuleBase {
    static class Messages {
        public const string USER_NOT_CONNECTED = "You are not connected to voice channel!";
        public const string BOT_NOT_CONNECTED = "I am not connected!";
        public const string NOT_PLAYING = "Not playing anything!";
    }

    [SlashCommand("join", "Joins voice channel.", runMode: RunMode.Async)]
    public async Task Join() {
        await DeferAsync().ConfigureAwait(false);

        if(Context.User is not IVoiceState voiceState) {
            await FollowupAsync(Messages.USER_NOT_CONNECTED, ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await audioService.Players.JoinAsync(
            Context.Guild.Id,
            voiceState.VoiceChannel.Id,
            PlayerFactory.Queued,
            Options.Create(new QueuedLavalinkPlayerOptions())
        ).ConfigureAwait(false);

        await FollowupAsync($"Joined voice channel {voiceState.VoiceChannel.Mention}");
    }

    public enum SearchModeWrapper {
        YouTube, SoundCloud
    }

    [SlashCommand("play", "Plays the song or enqueues it.", runMode: RunMode.Async)]
    public async Task Play(
        [Summary("query", "Url or name of a song.")] string query,
        [Summary("searchMode", "Where to search for the song.")] SearchModeWrapper searchMode=SearchModeWrapper.YouTube
    ) {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync().ConfigureAwait(false);

        if(player is null) {
            return;
        }

        TrackSearchMode mode = Uri.IsWellFormedUriString(query, UriKind.Absolute) ?
            TrackSearchMode.None : searchMode switch {
                SearchModeWrapper.YouTube => TrackSearchMode.YouTube,
                SearchModeWrapper.SoundCloud => TrackSearchMode.SoundCloud,
                _ => TrackSearchMode.None
            };

        var track = await audioService.Tracks
            .LoadTrackAsync(query, new TrackLoadOptions(mode))
            .ConfigureAwait(false);
        
        if(track is null) {
            await FollowupAsync("Couldn't find anything.", ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        bool enqueued = player.CurrentTrack is not null;

        await player.PlayAsync(track).ConfigureAwait(false);

        EmbedBuilder embed = new();

        if(enqueued) {
            embed = embed.WithTitle($"Enqueued {track.Title}")
                .WithDescription($"Enqueued [**{track.Title}**]({track.Uri})");
        } else {
            embed = embed.WithTitle(track.Title)
                .WithDescription($"Now playing [**{track.Title}**]({track.Uri})");
        }

        embed = embed.WithColor(Color.Blue)
            .WithThumbnailUrl(track.ArtworkUri?.OriginalString ?? "")
            .AddField("Duration", track.Duration, true)
            .AddField("Requested by", Context.User.Mention, true)
            .AddField("Video author", track.Author);

        await FollowupAsync(embeds: [embed.Build()]).ConfigureAwait(false);
    }

    [SlashCommand("stop", "Stops the music and clears queue.", runMode: RunMode.Async)]
    public async Task Stop() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING, ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await player.StopAsync();
        await FollowupAsync("Stopped all current tracks.");
    }

    [SlashCommand("now", "Shows currently playing track", runMode: RunMode.Async)]
    public async Task CurrentTrack() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING)
                .ConfigureAwait(false);
            return;
        }

        // TODO: Fix too many decimals
        await FollowupAsync(string.Format("Now playing [**{0}**]({1}) ({2:g}/{3:g})",
            player.CurrentTrack.Title, player.CurrentTrack.Uri,
            player.Position!.Value.Position, player.CurrentTrack.Duration
        )).ConfigureAwait(false);
    }

    [SlashCommand("queue", "Shows currently queued tracks.", runMode: RunMode.Async)]
    public async Task GetQueue() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING);
            return;
        }

        var track = player.CurrentTrack;

        EmbedBuilder embedBuilder = new EmbedBuilder()
            .WithTitle("Current queue")
            .WithColor(Color.Green)
            .WithDescription($"Now playing [**{track.Title}**]({track.Uri})")
            .WithThumbnailUrl(track.ArtworkUri?.OriginalString);
        
        uint count = 0;
        foreach(var queueItem in player.Queue) {
            count++; // 1-based indexing (oof)

            var queuedTrack = queueItem.Track;
            if(queuedTrack is null) {
                embedBuilder = embedBuilder.AddField($"{count}", "Failed to decode the track.");
            } else {
                embedBuilder = embedBuilder.AddField($"{count}", $"[**{queuedTrack.Title}**]({queuedTrack.Uri})");
            }
        }

        if(count == 0) {
            embedBuilder.AddField("No tracks", "Queue is empty. Only currently playing track is there.");
        }

        await FollowupAsync(embeds: [embedBuilder.Build()]);
    }

    [SlashCommand("skip", "Skips current track.", runMode: RunMode.Async)]
    public async Task Skip() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING);
            return;
        }

        if(player.Queue.IsEmpty) {
            await FollowupAsync("Cannot skip track because it's the only one in queue.", ephemeral: true);
            return;
        }

        var previousTrack = player.CurrentTrack;

        await player.SkipAsync().ConfigureAwait(false);

        var track = player.CurrentTrack ?? throw new ArgumentNullException("For some reason.");

        var embed = new EmbedBuilder()
            .WithTitle(track.Title)
            .WithDescription($"Now playing [**{track.Title}**]({track.Uri})")
            .WithColor(Color.Blue)
            .WithThumbnailUrl(track.ArtworkUri?.OriginalString ?? "")
            .AddField("Duration", track.Duration, true)
            .AddField("Skipped to by", Context.User.Mention, true)
            .AddField("Video author", track.Author)
            .Build();

        await FollowupAsync($"Skipped track [**{previousTrack.Title}**]({previousTrack.Uri}).", embeds: [embed]);
    }

    [SlashCommand("seek", "Changes the position of a currently playing track.", runMode: RunMode.Async)]
    public async Task Seek(
        [Summary("timecode", "A time code to seek to. Format `HH:mm:ss`")] string timecode
    ) {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING);
            return;
        }

        if(!TimeSpan.TryParse(timecode, null, out TimeSpan timeSpan)) {
            await FollowupAsync("Failed to parse timecode! Format has to be: `HH:mm:ss`", ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(timeSpan);
        await FollowupAsync($"Changed song position to {timeSpan}.");
    }

    [SlashCommand("pause", "Pauses currently playing track.", runMode: RunMode.Async)]
    public async Task Pause() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING);
            return;
        }

        if(player.State.HasFlag(PlayerState.Paused)) {
            await FollowupAsync("Already paused.", ephemeral: true);
            return;
        }

        await player.PauseAsync();
        await FollowupAsync($"Paused track at {player.Position!.Value.Position}")
            .ConfigureAwait(false);
    }

    [SlashCommand("resume", "Resumes paused track.", runMode: RunMode.Async)]
    public async Task Resume() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.State.HasFlag(PlayerState.Playing) && !player.State.HasFlag(PlayerState.Paused)) {
            await FollowupAsync("Already playing.", ephemeral: true);
            return;
        }

        await player.ResumeAsync();
        await FollowupAsync($"Resumed track.")
            .ConfigureAwait(false);
    }

    [SlashCommand("leave", "Makes bot leave a voice chat.", runMode: RunMode.Async)]
    public async Task Leave() {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        await player.DisconnectAsync().ConfigureAwait(false);
        await FollowupAsync("Left voice chat.");
    }

    [SlashCommand("remove", "Removes song from queue.", runMode: RunMode.Async)]
    public async Task Remove(
        [Summary("songId", "Position of song in a queue. `0` skips current song.")] int songId
    ) {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        if(player.CurrentTrack is null) {
            await FollowupAsync(Messages.NOT_PLAYING);
            return;
        }

        if(songId < 0) {
            await FollowupAsync("Invalid ID. Only positive numbers and 0 are allowed.");
            return;
        }

        if(songId == 0) {
            if(player.Queue.IsEmpty) {
                await FollowupAsync("Cannot skip track because it's the only one in queue.", ephemeral: true);
                return;
            }

            await player.SkipAsync();

            var newTrack = player.CurrentTrack;

            var embed = new EmbedBuilder()
                .WithTitle(newTrack.Title)
                .WithDescription($"Now playing [**{newTrack.Title}**]({newTrack.Uri})")
                .WithColor(Color.Blue)
                .WithThumbnailUrl(newTrack.ArtworkUri?.OriginalString ?? "")
                .AddField("Duration", newTrack.Duration, true)
                .AddField("Skipped to by", Context.User.Mention, true)
                .AddField("Video author", newTrack.Author)
                .Build();

            await FollowupAsync($"Removed current song from playback. Skipping to next one.", embeds: [embed]);
            return;
        }

        int index = songId - 1;
        if(index >= player.Queue.Count) {
            await FollowupAsync($"ID {songId} is outside of queue! " + 
                "Type `/queue` to find out what number a song has.");
            return;
        }

        var track = player.Queue[index].Track;

        if(track is null) {
            await FollowupAsync("Something went wrong. Try again later.", ephemeral: true);
            return;
        }

        await player.Queue.RemoveAtAsync(index);
        await FollowupAsync($"Removed [**{track.Title}**]({track.Uri})");
    }

    // TODO: Figure out looping
    public async Task SetLoop(
        [Summary("loops", "Number of times to loop. `-1` for endless.")] int loops=-1
    ) {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync(false).ConfigureAwait(false);

        if(player is null) {
            return;
        }

        throw new NotImplementedException("Biggest problem would be tracking" + 
            " next song, because there is no explicit API to loop in the library.");
    }

    private async Task<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoice = true) {
        if(Context.User is not IVoiceState voiceState) {
            await FollowupAsync(Messages.USER_NOT_CONNECTED).ConfigureAwait(false);
            return null;
        }

        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: connectToVoice ? PlayerChannelBehavior.Join :
                PlayerChannelBehavior.None
        );

        var result = await audioService.Players.RetrieveAsync(
            Context.Guild.Id,
            voiceState.VoiceChannel.Id,
            PlayerFactory.Queued,
            Options.Create(new QueuedLavalinkPlayerOptions()),
            retrieveOptions
        ).ConfigureAwait(false);
        
        if(!result.IsSuccess) {
            var error = result.Status switch {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Messages.USER_NOT_CONNECTED,
                PlayerRetrieveStatus.BotNotConnected => Messages.BOT_NOT_CONNECTED,
                _ => "Unknown error."
            };

            await FollowupAsync(error).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }
}
