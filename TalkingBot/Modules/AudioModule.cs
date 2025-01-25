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
        public const string NOT_CONNECTED = "You are not connected to voice channel!";
    }

    [SlashCommand("join", "Joins voice channel.", runMode: RunMode.Async)]
    public async Task Join() {
        if(Context.User is not IVoiceState voiceState) {
            await RespondAsync(Messages.NOT_CONNECTED, ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        await audioService.Players.JoinAsync(
            Context.Guild.Id,
            voiceState.VoiceChannel.Id,
            PlayerFactory.Queued,
            Options.Create(new QueuedLavalinkPlayerOptions())
        ).ConfigureAwait(false);

        await RespondAsync($"Joined voice channel {voiceState.VoiceChannel.Mention}");
    }

    [SlashCommand("play", "Plays the song or enqueues it.", runMode: RunMode.Async)]
    public async Task Play(
        [Summary("query", "Url or name of a song.")] string query
    ) {
        await DeferAsync().ConfigureAwait(false);

        var player = await GetPlayerAsync().ConfigureAwait(false);

        if(player is null) {
            return;
        }

        // TODO: Add any searchMode support
        TrackSearchMode searchMode = Uri.IsWellFormedUriString(query, UriKind.Absolute) ? 
            TrackSearchMode.None : TrackSearchMode.YouTube;

        var track = await audioService.Tracks
            .LoadTrackAsync(query, new TrackLoadOptions(searchMode))
            .ConfigureAwait(false);
        
        if(track is null) {
            await FollowupAsync("Couldn't find anything.", ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        // TODO: Add queue check
        await player.PlayAsync(track).ConfigureAwait(false);
        await FollowupAsync($"Now playing: {track.Uri}").ConfigureAwait(false);
    }

    private async Task<LavalinkPlayer?> GetPlayerAsync(bool connectToVoice = true) {
        if(Context.User is not IVoiceState voiceState) {
            await FollowupAsync(Messages.NOT_CONNECTED).ConfigureAwait(false);
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
                PlayerRetrieveStatus.UserNotInVoiceChannel => Messages.NOT_CONNECTED,
                PlayerRetrieveStatus.BotNotConnected => "Bot not connected (TODO: Understand what this message means)",
                _ => "Unknown error."
            };

            await FollowupAsync(error).ConfigureAwait(false);
            return null;
        }

        return result.Player;
    }
}
