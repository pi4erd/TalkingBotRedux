using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TalkingBot.Core;

public class TalkingBotConfig(
    ulong[] guilds,
    string token = "",
    string lavalinkHost = "http://localhost:2333",
    string lavalinkPassword = "youshallnotpass",
    LogLevel logLevel = LogLevel.Debug,
    bool clearCommands = false
    )
{
    [JsonProperty("token")]
    public string Token { get; set; } = token;

    [JsonProperty("guilds")]
    public ulong[] Guilds { get; set; } = guilds;

    [JsonProperty("lavalinkHost")]
    public string LavalinkHost { get; set; } = lavalinkHost;

    [JsonProperty("lavalinkPassword")]
    public string LavalinkPassword { get; set; } = lavalinkPassword;

    [JsonProperty("logLevel")]
    public LogLevel LogLevel { get; set; } = logLevel;

    [JsonProperty("clearCommands")]
    public bool ClearCommands { get; set; } = clearCommands;

    public static TalkingBotConfig? Read(string filename) {
        string rawJson;

        try
        {
            rawJson = File.ReadAllText(filename);
        } catch(FileNotFoundException) {
            return null;
        }

        return JsonConvert.DeserializeObject<TalkingBotConfig>(rawJson);
    }

    public void Write(string filename) {
        string rawJson = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(filename, rawJson);
    }
}
