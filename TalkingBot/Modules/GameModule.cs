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

    private static char? FormatDice(int n) {
        return n switch {
            1 => '⚀',
            2 => '⚁',
            3 => '⚂',
            4 => '⚃',
            5 => '⚄',
            6 => '⚅',
            _ => null,
        };
    }

    [SlashCommand("dice", "Play dice and gamble :)", runMode: RunMode.Async)]
    public async Task Dice([Summary("bet", "Your bet. You lose or gain this much.")] ulong bet) {
        const double cooldownHours = 1.0;
        await DeferAsync().ConfigureAwait(false);

        UserGameData userData = gameDataCacher.GetUserGameData(Context.User.Id);

        double hoursElapsed = (DateTime.Now - userData.LastDaily).TotalHours;

        if(hoursElapsed < cooldownHours) {
            await FollowupAsync($"You can play dice {(cooldownHours - hoursElapsed) * 60.0:.} minutes.", ephemeral: true);
            return;
        }

        await FollowupAsync($"You bet {bet}🪙.");
        await Task.Delay(1000);
        await FollowupAsync("You roll your dice...");
        await Task.Delay(1000);

        int dice1 = Random.Shared.Next(1, 7);
        int dice2 = Random.Shared.Next(1, 7);

        char diceChar1 = FormatDice(dice1) ?? '-';
        char diceChar2 = FormatDice(dice1) ?? '-';

        await FollowupAsync($"You get: {diceChar1} and {diceChar2}");
        await Task.Delay(1000);

        int myDice1 = Random.Shared.Next(1, 7);
        int myDice2 = Random.Shared.Next(1, 7);
        char myDiceChar1 = FormatDice(dice1) ?? '-';
        char myDiceChar2 = FormatDice(dice1) ?? '-';

        await FollowupAsync($"I roll {myDiceChar1} and {myDiceChar2}");
        await Task.Delay(1000);
        
        if(dice1 + dice2 > myDice1 + myDice2) { // player wins
            await FollowupAsync($"You won! You gain {bet * 2}🪙.");
            userData.Money += bet;
        } else if(dice1 + dice2 < myDice1 + myDice2) { // player loses
            await FollowupAsync($"You lost! You lose {bet}🪙.");
            userData.Money -= bet;
        } else { // draw
            await FollowupAsync($"Draw! You don't gain nor lose.");
        }

        userData.LastDice = DateTime.Now;
        gameDataCacher.ModifyUserData(Context.User.Id, userData);
    }
}
