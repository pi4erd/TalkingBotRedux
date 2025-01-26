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
        var roleMsg = cacher.cachedMessages.Find(m => m.MessageId == Context.Interaction.Message.Id);

        if(roleMsg is null) {
            await RespondAsync("Interaction failed because failed to find message in cache.", ephemeral: true);
            logger.LogWarning("Interaction failed. Cache probably outdated!");
            return;
        }

        var role = await Context.Guild.GetRoleAsync(roleMsg.RoleId);

        if(role is null) {
            await RespondAsync("Role wasn't found in guild. Message probably wouldn't work.", ephemeral: true);
            logger.LogWarning("Interaction failed. Cache probably outdated!");
            return;
        }

        IGuildUser user = Context.User as IGuildUser ?? throw new Exception("User wasn't a guild user.");

        try {
            await user.AddRoleAsync(role);
        } catch(Exception) {
            await RespondAsync("Error occured while giving role. " +
                "Probably the bot doesn't have enough permissions. Ask administrator " +
                "if you think this problem shouldn't exist.", ephemeral: true);
            logger.LogWarning("Interaction failed. Bot probably doesn't have" + 
                " enough permissions to give role {}.", role.Name);
            return;
        }

        await RespondAsync($"You successfully got the role {role.Mention}!", ephemeral: true);
    }
}
