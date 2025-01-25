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
}
