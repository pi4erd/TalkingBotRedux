using Discord;
using Discord.Interactions;
using TalkingBot.Core.Caching;
using TalkingBot.Services;

namespace TalkingBot.Modules;

public class GameModule(GameDataCacher gameDataCacher) : InteractionModuleBase {
    [SlashCommand("daily", "Get your daily bonus.", runMode: RunMode.Async)]
    public async Task Daily() {
        const double cooldownHours = 24.0;
        await DeferAsync(false).ConfigureAwait(false);

        const int MAX_NUMBER = 100;
        const int MIN_NUMBER = 60;
        int bonus = Random.Shared.Next() % (MAX_NUMBER - MIN_NUMBER) + MIN_NUMBER;

        UserGameData userData = gameDataCacher.GetUserGameData(Context.User.Id);

        double hoursElapsed = (DateTime.Now - userData.LastDaily).TotalHours;

        if(hoursElapsed < cooldownHours) {
            await FollowupAsync($"You can get your bonus after {cooldownHours - hoursElapsed:.} hours.", ephemeral: true);
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

    [SlashCommand("dice", "Play dice and gamble :)", runMode: RunMode.Async)]
    public async Task Dice([Summary("bet", "Your bet. You lose or gain this much."), ] long bet) {
        const double cooldownHours = 1.0;
        await DeferAsync().ConfigureAwait(false);

        UserGameData userData = gameDataCacher.GetUserGameData(Context.User.Id);

        double hoursElapsed = (DateTime.Now - userData.LastDice).TotalHours;

        if(bet <= 0) {
            await FollowupAsync("Can't bet zero or negative 🪙!!!", ephemeral: true);
            return;
        }

        if(hoursElapsed < cooldownHours) {
            await FollowupAsync($"You can play dice in {(cooldownHours - hoursElapsed) * 60.0:.} minutes.", ephemeral: true);
            return;
        }

        if(bet > (long)userData.Money) {
            await FollowupAsync($"You can't bet {bet}🪙 because you're broke.", ephemeral: true);
            return;
        }

        await FollowupAsync($"You bet **{bet}**🪙.");
        await Task.Delay(1000);
        await FollowupAsync("You roll your dice...", ephemeral: true);
        await Task.Delay(1000);

        int dice1 = Random.Shared.Next(1, 7);
        int dice2 = Random.Shared.Next(1, 7);

        await FollowupAsync($"You get: **{dice1}** and **{dice2}**", ephemeral: true);
        await Task.Delay(1000);

        int myDice1 = Random.Shared.Next(1, 7);
        int myDice2 = Random.Shared.Next(1, 7);

        await FollowupAsync($"I roll **{myDice1}** and **{myDice2}**", ephemeral: true);
        await Task.Delay(1000);
        
        if(dice1 + dice2 > myDice1 + myDice2) { // player wins
            await FollowupAsync($"You won! You gain **{bet * 2}**🪙.");
            userData.Money += (ulong)bet;
        } else if(dice1 + dice2 < myDice1 + myDice2) { // player loses
            await FollowupAsync($"You lost! You lose **{bet}**🪙.");
            userData.Money -= (ulong)bet;
        } else { // draw
            await FollowupAsync($"Draw! You don't gain nor lose.");
        }

        userData.LastDice = DateTime.Now;
        gameDataCacher.ModifyUserData(Context.User.Id, userData);
    }

    [SlashCommand("level", "Show information on your level")]
    public async Task Level() {
        await DeferAsync().ConfigureAwait(false);

        UserGameData userData = gameDataCacher.GetUserGameData(Context.User.Id);

        await FollowupAsync($"Your level is **{userData.Level}** " +
            $"({userData.Experience}/{(userData.Level + 1) * UserGameData.ExpRequirement})");
    }
}
