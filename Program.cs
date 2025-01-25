using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Lavalink4NET.Extensions;
using Discord.WebSocket;
using Discord.Interactions;
using TalkingBot.Core;
using Microsoft.Extensions.Logging;
using Discord.Rest;

namespace TalkingBot;

class Program {
    static void Main(string[] args) {
        HostApplicationBuilder builder = new(args);

        TalkingBotConfig? botConfig = TalkingBotConfig.Read("Config.json");

        if(botConfig is null) {
            new TalkingBotConfig([]).Write("Config.json");
            throw new FileNotFoundException("Config.json wasn't found. Created default.");
        }

        builder.Services.AddSingleton(botConfig);
        builder.Services.AddSingleton<DiscordSocketClient>();
        builder.Services.AddSingleton<InteractionService>();
        builder.Services.AddSingleton<IRestClientProvider>(x => x.GetRequiredService<DiscordSocketClient>());
        builder.Services.AddHostedService<TalkingBotClient>();

        builder.Services.AddLavalink();
        // TODO: Add custom config with log-level select
        builder.Services.AddLogging(x => x.AddConsole().SetMinimumLevel(botConfig.LogLevel));
        builder.Services.ConfigureLavalink(config => {
            config.BaseAddress = new Uri(botConfig.LavalinkHost);
            config.Passphrase = botConfig.LavalinkPassword;
        });

        try {
            builder.Build().Run();
        } catch(Exception e) {
            Console.WriteLine("\n-----------------\nError while running application: {0}", e);
        }
    }
}
