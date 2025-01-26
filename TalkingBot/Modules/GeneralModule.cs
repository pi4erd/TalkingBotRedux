using Discord.Interactions;
using TalkingBot.Core;

namespace TalkingBot.Modules;

public class GeneralModule : InteractionModuleBase {
    [SlashCommand("ping", "Pings-Pongs. Used to test if bot is working.", runMode: RunMode.Async)]
    public async Task Ping() {
        await RespondAsync("Pong!").ConfigureAwait(false);
    }

    [SlashCommand("version", "Gets current bot version.", runMode: RunMode.Async)]
    public async Task Version() {
        await RespondAsync($"Current version: **{TalkingBotClient.CurrentVersion}**").ConfigureAwait(false);
    }

    [SlashCommand("roll", "Rolls a random number between 0 and `limit`.")]
    public async Task Roll(
        [Summary("limit", "Highest number to roll.")] long limit=6
    ) {
        if(limit <= 0) {
            await RespondAsync("Your limit is too low!", ephemeral: true);
            return;
        }

        var number = Random.Shared.NextInt64(limit) + 1; // add 1 because rolls [1; limit]

        if(limit == 1) {
            await RespondAsync($"{Context.User.Mention} rolled **{number}**/**{limit}**.\n-# Fancy pants... :unamused:");
        } else {
            await RespondAsync($"{Context.User.Mention} rolled **{number}**/**{limit}**.");
        }
    }
}
