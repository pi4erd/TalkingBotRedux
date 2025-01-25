using Discord;
using Discord.Interactions;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.Options;

namespace TalkingBot.Modules;

public class AudioModule(
    IAudioService audioService
) : InteractionModuleBase {
    [SlashCommand("join", "Joins voice channel.", runMode: RunMode.Async)]
    public async Task Join() {
        if(Context.User is not IVoiceState voiceState) {
            await RespondAsync("You are not connected to voice channel!", ephemeral: true)
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
}
