using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Lavalink4NET.Extensions;
using Discord.WebSocket;
using Discord.Interactions;
using TalkingBot.Core;
using Microsoft.Extensions.Logging;
using Discord.Rest;
using TalkingBot.Core.Caching;
using TalkingBot.Services;

HostApplicationBuilder builder = new(args);

string? configFilename = null;

for (int i = 0; i < args.Length; i++)
{
    var arg = args[i];

    switch (arg)
    {
        case "-C":
            string? filename = args.GetValue(++i) as string;

            if (filename is null)
            {
                Console.WriteLine("No filename provided for '-C'.");
                Environment.Exit(1);
            }

            configFilename = filename;
            break;
        default:
            break;
    }
}

// NOTE: This might not be the best way to put in config
// but cut me some slack, it works.
TalkingBotConfig? botConfig = TalkingBotConfig.Read(configFilename ?? "Config.json");

Console.WriteLine("Selected {0} as config.", configFilename ?? "Config.json");

if (botConfig is null)
{
    new TalkingBotConfig([]).Write(configFilename ?? "Config.json");
    throw new FileNotFoundException("Config.json wasn't found. Created default.");
}

builder.Services.AddSingleton(botConfig);
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<Cache<RoleMessageCache>>();
builder.Services.AddSingleton<MessageCacher>();
builder.Services.AddSingleton<IRestClientProvider>(x => x.GetRequiredService<DiscordSocketClient>());
builder.Services.AddHostedService<TalkingBotClient>();

builder.Services.AddLavalink();
builder.Services.AddLogging(x => x.AddConsole().SetMinimumLevel(botConfig.LogLevel));
builder.Services.ConfigureLavalink(config =>
{
    config.BaseAddress = new Uri(botConfig.LavalinkHost);
    config.Passphrase = botConfig.LavalinkPassword;
});

try
{
    builder.Build().Run();
}
catch (Exception e)
{
    Console.WriteLine("\n-----------------\nError while running application: {0}", e);
}
