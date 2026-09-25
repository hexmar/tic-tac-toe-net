using Microsoft.Extensions.Options;
using TicTacToeNet.TelegramBot.Clients;
using TicTacToeNet.TelegramBot.Configurations;
using TicTacToeNet.TelegramBot.Services;

namespace TicTacToeNet.TelegramBot;

internal sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<TelegramApiConfiguration>(
            builder.Configuration.GetSection(TelegramApiConfiguration.ConfigurationKey));

        builder.Services.AddRedaction();

        builder.Services
            .AddHttpClient<ITelegramBotApiClient, TelegramBotApiClient>(static (services, client) =>
            {
                var options = services.GetRequiredService<IOptions<TelegramApiConfiguration>>();
                var config = options.Value;
                client.BaseAddress = new Uri($"https://api.telegram.org/bot{config.ApiKey}/");
                client.Timeout = TimeSpan.FromSeconds(config.Timeout);
            })
            .AddExtendedHttpClientLogging(builder.Configuration.GetSection("HttpClientLogging"));

        builder.Services.AddHostedService<TelegramBackgroundService>();
        builder.Services.AddSingleton<ITelegramUpdateQueue, TelegramUpdateQueue>(
            services => new TelegramUpdateQueue(10));

        var host = builder.Build();
        host.Run();
    }
}
