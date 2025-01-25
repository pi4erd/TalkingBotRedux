using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TalkingBot.Core;

public class TalkingBotConfig {
    [JsonProperty("token")]
    public string Token { get; set; }

    [JsonProperty("guilds")]
    public ulong[] Guilds { get; set; }

    [JsonProperty("lavalinkHost")] // example: "http://localhost:2333"
    public string LavalinkHost { get; set; }

    [JsonProperty("lavalinkPassword")]
    public string LavalinkPassword { get; set; }

    [JsonProperty("logLevel")]
    public LogLevel LogLevel { get; set; }

    public TalkingBotConfig(
        ulong[] guilds,
        string token="",
        string lavalinkHost="http://localhost:2333",
        string lavalinkPassword="youshallnotpass",
        LogLevel logLevel=LogLevel.Debug
    ) {
        Token = token;
        Guilds = guilds;
        LogLevel = logLevel;
        LavalinkHost = lavalinkHost;
        LavalinkPassword = lavalinkPassword;
    }

    public static TalkingBotConfig? Read(string filename) {
        string rawJson = "";

        try {
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
