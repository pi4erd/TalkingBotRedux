using Discord;
using Discord.Interactions;
using TalkingBot.Core;
using TalkingBot.Core.Caching;
using TalkingBot.Services;

namespace TalkingBot.Modules;

public class GeneralModule(MessageCacher cacher) : InteractionModuleBase {
    [SlashCommand("ping", "Pings-Pongs. Used to test if bot is working.", runMode: RunMode.Async)]
    public async Task Ping() {
        await DeferAsync().ConfigureAwait(false);
        await FollowupAsync("Pong!").ConfigureAwait(false);
    }

    [SlashCommand("version", "Gets current bot version.", runMode: RunMode.Async)]
    public async Task Version() {
        await DeferAsync().ConfigureAwait(false);
        await FollowupAsync($"Current version: **{TalkingBotClient.CurrentVersion}**").ConfigureAwait(false);
    }

    [SlashCommand("roll", "Rolls a random number between 0 and `limit`.")]
    public async Task Roll(
        [Summary("limit", "Highest number to roll.")] long limit=6
    ) {
        await DeferAsync().ConfigureAwait(false);

        if(limit <= 0) {
            await FollowupAsync("Your limit is too low!", ephemeral: true);
            return;
        }

        var number = Random.Shared.NextInt64(limit) + 1; // add 1 because rolls [1; limit]

        if(limit == 1) {
            await FollowupAsync($"{Context.User.Mention} rolled **{number}**/**{limit}**.\n-# Fancy pants... :unamused:");
        } else {
            await FollowupAsync($"{Context.User.Mention} rolled **{number}**/**{limit}**.");
        }
    }

    [DefaultMemberPermissions(GuildPermission.Administrator)] // don't wanna risk it y'know
    [SlashCommand("rolemsg", "Creates 'get role' button on a target message.", runMode: RunMode.Async)]
    public async Task RoleMsg(
        [Summary("messageId", "ID of a message to attach to")] string messageIdStr,
        [Summary("role", "Role to give on button click")] IRole role
    ) {
        await DeferAsync(true).ConfigureAwait(false);

        var button = new ButtonBuilder()
            .WithLabel("Get role")
            .WithCustomId("add-role")
            .WithStyle(ButtonStyle.Primary);
        
        ulong messageId = ulong.Parse(messageIdStr); // Workaround for discord limitation on integer size
        var message = await Context.Channel.GetMessageAsync(messageId);

        if(message is not null) {
            var components = ComponentBuilder.FromMessage(message)
                .WithButton(button)
                .Build();
            
            var botMessage = await Context.Channel.SendMessageAsync(message.Content, components: components);

            cacher.AddMessage(new RoleMessageCache() {
                MessageId = botMessage.Id,
                RoleId = role.Id
            });

            await message.DeleteAsync();
            await FollowupAsync("Deleted old message and created a new message successfully.", ephemeral: true);
            
            return;
        }

        await FollowupAsync("Failed to add component as message wasn't found in current channel.", ephemeral: true);
    }
}
