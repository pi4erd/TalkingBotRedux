// Per-user game data
using Newtonsoft.Json;

namespace TalkingBot.Core.Caching;

public class UserGameData {
    [JsonProperty("money")]
    public ulong Money { get; set; }

    [JsonProperty("lastDaily")]
    public DateTime LastDaily { get; set; }

    [JsonProperty("lastDice")]
    public DateTime LastDice { get; set; }

    public static UserGameData Default() {
        return new UserGameData {
            Money = 0,
            LastDaily = DateTime.MinValue,
            LastDice = DateTime.MinValue,
        };
    }
}
