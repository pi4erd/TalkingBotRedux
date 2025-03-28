using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TalkingBot.Services;

namespace TalkingBot.Modules;

public class ButtonModule(MessageCacher cacher, ILogger<ButtonModule> logger)
    : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
{
    [ComponentInteraction("add-role", runMode: RunMode.Async)]
    public async Task AddRoleButton() {
        await DeferAsync(true).ConfigureAwait(false);

        var roleMsg = cacher.FindMessage(Context);

        if(roleMsg is null) {
            await FollowupAsync("Interaction failed because failed to find message in cache.", ephemeral: true);
            logger.LogWarning("Role message interaction failed. Cache probably outdated!");
            return;
        }

        var role = await Context.Guild.GetRoleAsync(roleMsg.RoleId);

        if(role is null) {
            await FollowupAsync("Role wasn't found in guild. Message probably wouldn't work.", ephemeral: true);
            logger.LogWarning("Role message interaction failed. Cache probably outdated!");
            return;
        }

        IGuildUser user = Context.User as IGuildUser ?? throw new Exception("User wasn't a guild user.");

        try {
            await user.AddRoleAsync(role);
        } catch(Exception) {
            await FollowupAsync("Error occured while giving role. " +
                "Probably the bot doesn't have enough permissions. Ask administrator " +
                "if you think this problem shouldn't exist.", ephemeral: true);
            logger.LogWarning("Interaction failed. Bot probably doesn't have" + 
                " enough permissions to give role {}.", role.Name);
            return;
        }

        await FollowupAsync($"You successfully got the role {role.Mention}!", ephemeral: true);
    }
}
