using Newtonsoft.Json;

namespace TalkingBot.Core.Caching;

public class RoleMessageCache {
    [JsonProperty("messageId")]
    public ulong MessageId { get; set; }

    [JsonProperty("roleId")]
    public ulong RoleId { get; set; }
}
