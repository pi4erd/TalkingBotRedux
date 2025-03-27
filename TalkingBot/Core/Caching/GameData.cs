// Per-user game data
using Newtonsoft.Json;

namespace TalkingBot.Core.Caching;

public class UserGameData {
    public const ulong ExpRequirement = 500; // linear for now

    [JsonProperty("money")]
    public ulong Money { get; set; }

    [JsonProperty("lastDaily")]
    public DateTime LastDaily { get; set; }

    [JsonProperty("lastDice")]
    public DateTime LastDice { get; set; }

    [JsonProperty("experience")]
    public ulong Experience { get; set; }

    [JsonProperty("level")]
    public ulong Level { get; set; }

    [JsonProperty("lastExpGain")]
    public DateTime LastExpGain { get; set; }

    public static ulong ExpectedExp(ulong level) {
        return level * ExpRequirement; // TODO: Make non-linear
    }

    // Updates level based on experience. If exceeds level requirements, returns true
    public bool UpdateLevel() {
        ulong expectedExp = ExpectedExp(Level);
        if(Experience - expectedExp >= ExpRequirement) {
            Level += 1;
            return true;
        }
        return false;
    }

    public static UserGameData Default() {
        return new UserGameData {
            Money = 0,
            LastDaily = DateTime.MinValue,
            LastDice = DateTime.MinValue,
            Experience = 0,
            Level = 0,
            LastExpGain = DateTime.MinValue
        };
    }
}
