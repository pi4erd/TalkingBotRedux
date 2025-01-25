using Discord.Interactions;

namespace TalkingBot.Modules;

public class GeneralModule : InteractionModuleBase {
    [SlashCommand("ping", "Pings-Pongs. Used to test if bot is working.", runMode: RunMode.Async)]
    public async Task Ping() {
        await RespondAsync("Pong!").ConfigureAwait(false);
    }
}
