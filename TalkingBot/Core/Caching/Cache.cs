using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TalkingBot.Core.Caching;

// NOTE: This is a service
public class Cache<T>(ILogger<Cache<T>> logger) {
    private readonly string typename = typeof(T).Name;
    // TODO: Allow setting cache directory in a config
    private readonly string cacheDirectory = Directory.GetCurrentDirectory();

    public void SaveCached(T[] cache) {
        string json = JsonConvert.SerializeObject(cache);

        string filename = $"cache_{typename}.json";
        string dir = cacheDirectory + "/Cache/";
        
        if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        logger.LogInformation("Saved cache for {}.", typename);

        using StreamWriter sw = new(dir + filename);
        sw.Write(json);
    }
    
    public T[]? LoadCached() {
        string filename = $"cache_{typename}.json";
        string dir = cacheDirectory + "/Cache/";

        string json = "";

        try {
            using StreamReader sr = new(dir + filename);

            json = sr.ReadToEnd();
        } catch(IOException) {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.Create(dir + filename);
            return null;
        }

        logger.LogInformation("Loaded cache for {}.", typename);

        var result = JsonConvert.DeserializeObject<T[]>(json);

        return result;
    }
}