using Newtonsoft.Json;

public class GameConfig(
    ulong expGain,
    ulong expGainDelay
) {
    [JsonProperty("expGain")]
    public ulong ExpGain { get; set; } = expGain;
    
    [JsonProperty("expGainDelaySeconds")]
    public ulong ExpGainDelaySeconds { get; set; } = expGainDelay;
}
