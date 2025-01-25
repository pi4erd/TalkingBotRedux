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

        builder.Services.AddSingleton<DiscordSocketClient>();
        builder.Services.AddSingleton<InteractionService>();
        builder.Services.AddSingleton<IRestClientProvider>(x => x.GetRequiredService<DiscordSocketClient>());
        builder.Services.AddHostedService<TalkingBotClient>();

        builder.Services.AddLavalink();
        // TODO: Add custom config with log-level select
        builder.Services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug));
        builder.Services.ConfigureLavalink(config => {
            config.BaseAddress = new Uri("http://localhost:2333");
        });

        builder.Build().Run();
    }
}
