using Discord.Interactions;
using TalkingBot.Core.Caching;
using TalkingBot.Services;

namespace TalkingBot.Modules;

public class GameModule(GameDataCacher gameDataCacher) : InteractionModuleBase {
    [SlashCommand("daily", "Get your daily bonus.", runMode: RunMode.Async)]
    public async Task Daily() {
        await DeferAsync(false).ConfigureAwait(false);

        const int MAX_NUMBER = 100;
        const int MIN_NUMBER = 60;
        int bonus = Random.Shared.Next() % (MAX_NUMBER - MIN_NUMBER) + MIN_NUMBER;

        UserGameData userData = gameDataCacher.GetUserGameData(Context.User.Id);

        double hoursElapsed = (DateTime.Now - userData.LastDaily).TotalHours;

        if(hoursElapsed < 24.0) {
            await FollowupAsync($"You can get your bonus after {24.0 - hoursElapsed:.} hours.", ephemeral: true);
            return;
        }

        userData.Money += (ulong)bonus;
        userData.LastDaily = DateTime.Now;

        gameDataCacher.ModifyUserData(Context.User.Id, userData);

        await FollowupAsync($"You got {bonus}🪙.");
    }
    
    [SlashCommand("money", "Show how much money you have", runMode: RunMode.Async)]
    public async Task Money() {
        await DeferAsync().ConfigureAwait(false);

        UserGameData userData = gameDataCacher.GetUserGameData(Context.User.Id);
        await FollowupAsync($"You have {userData.Money}🪙. "+
            "||(to get more, complete quests and run `/daily` daily)||");
    }
}
