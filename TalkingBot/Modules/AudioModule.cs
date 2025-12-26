using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TalkingBot.Modules;

public class AudioModule(
    IAudioService audioService,
    ILogger<AudioModule> logger
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
        SoundCloud, YouTube, Spotify, URL
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
                SearchModeWrapper.Spotify => TrackSearchMode.Spotify,
                _ => TrackSearchMode.None
            };

        var tracks = await audioService.Tracks
            .LoadTracksAsync(query, mode)
            .ConfigureAwait(false);
        
        if(!tracks.IsSuccess) {
            await FollowupAsync("Couldn't find track or playlist.", ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        bool playlist = tracks.IsPlaylist;
        bool enqueued = player.CurrentTrack is not null;

        EmbedBuilder embed = new();

        if(playlist) {
            uint count = 0;
            foreach(var track in tracks.Tracks) {
                await player.PlayAsync(track).ConfigureAwait(false);
                count++;

                // FIXME: Fix bug with Queue crashing if too many tracks are enqueued #1
                if(count >= 15) {
                    break;
                }
            }

            if(enqueued) {
                embed = embed.WithTitle($"Enqueued playlist {tracks.Playlist!.Name}")
                    .WithDescription($"Enqueued [**{tracks.Track.Title}**]({tracks.Track.Uri})");
            } else {
                embed = embed.WithTitle($"Playlist {tracks.Playlist!.Name}")
                    .WithDescription($"Now playing [**{tracks.Track.Title}**]({tracks.Track.Uri})");
            }

            embed = embed.WithColor(Color.Blue)
                .WithThumbnailUrl(tracks.Playlist.SelectedTrack?.ArtworkUri?.OriginalString ?? "")
                .AddField("Number of songs enqueued", count, true);
        } else {
            await player.PlayAsync(tracks.Track);

            if(enqueued) {
                embed = embed.WithTitle($"Enqueued {tracks.Track.Title}")
                    .WithDescription($"Enqueued [**{tracks.Track.Title}**]({tracks.Track.Uri})");
            } else {
                embed = embed.WithTitle($"{tracks.Track.Title}")
                    .WithDescription($"Now playing [**{tracks.Track.Title}**]({tracks.Track.Uri})");
            }

            embed = embed.WithColor(Color.Blue)
                .WithThumbnailUrl(tracks.Track.ArtworkUri?.OriginalString ?? "")
                .AddField("Duration", tracks.Track.Duration, true)
                .AddField("Requested by", Context.User.Mention, true)
                .AddField("Video author", tracks.Track.Author);
        }

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
        await DeferAsync(ephemeral: false).ConfigureAwait(false);

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
        LavalinkTrack?[] queue;
        try { // FIXME: Workaround for #1
            queue = [.. player.Queue.Select((queueItem) => queueItem.Track)];

            foreach(var queuedTrack in queue) {
                if(queuedTrack is null) continue;

                count++; // 1-based indexing (oof)

                if(count > 13) {
                    embedBuilder = embedBuilder.AddField("...", "And more...");
                    break;
                }

                embedBuilder = embedBuilder.AddField($"{count}", $"[**{queuedTrack.Title}**]({queuedTrack.Uri})");
            }

            if(count == 0) {
                embedBuilder = embedBuilder.AddField("No tracks", "Queue is empty. Only currently playing track is there.");
            }

            await FollowupAsync(embeds: [embedBuilder.Build()]).ConfigureAwait(false);
        } catch(Exception ex) {
            logger.LogWarning("Error occured while getting queue: "+ ex);
            await FollowupAsync($"Failed to display queue!", ephemeral: true);
            return;
        }
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
            return; // assume GetPlayerAsync responded
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
        } else if (voiceState.VoiceChannel is null) {
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
