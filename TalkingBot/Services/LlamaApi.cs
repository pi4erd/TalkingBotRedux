using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TalkingBot.Core;

namespace TalkingBot.Services;

public class LlamaApi(TalkingBotConfig config, ILogger<LlamaApi> logger)
{
    private bool? llamaAvailable;
    public bool LlamaAvailable
    {
        get
        {
            if (llamaAvailable is null)
            {
                HttpClient client = new()
                {
                    BaseAddress = new Uri(config.OllamaHost),
                };

                HttpResponseMessage response = client.Send(new HttpRequestMessage());
                llamaAvailable = response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            
            return llamaAvailable.Value;
        }
    }

    private const string ENDPOINT = "/api/chat";
    public async Task<LlamaMessage> ChatComplete(LlamaMessage[] messages)
    {
        if (!LlamaAvailable)
        {
            throw new Exception("Llama not connected");
        }

        HttpClient client = new()
        {
            BaseAddress = new Uri(config.OllamaHost + ENDPOINT),
        };

        LlamaRequest request = new()
        {
            Model = "llama3.2:3b",
            Messages = messages,
            Stream = false,
        };
        
        StringContent content = new(JsonConvert.SerializeObject(request));

        logger.LogDebug("New request to llama.");

        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            Content = content,
        });

        string responseString = await response.Content.ReadAsStringAsync();

        logger.LogDebug("{}", responseString);

        var llamaResponse = JsonConvert.DeserializeObject<LlamaResponse>(responseString);

        return llamaResponse.Message;
    }
}

struct LlamaRequest
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("messages")]
    public LlamaMessage[] Messages { get; set; }

    [JsonProperty("stream")]
    public bool Stream { get; set; }

    [JsonProperty("options", Required = Required.DisallowNull)]
    public LlamaOptions Options { get; set; }
}

struct LlamaResponse
{
    [JsonProperty("model")]
    public string Model { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("message")]
    public LlamaMessage Message { get; set; }
}

struct LlamaOptions
{
    [JsonProperty("temperature")]
    public float Temperature { get; set; }
}

public struct LlamaMessage
{
    [JsonProperty("role")]
    public string Role { get; set; }
    [JsonProperty("content")]
    public string Content { get; set; }
}
