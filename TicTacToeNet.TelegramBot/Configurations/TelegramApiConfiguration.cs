namespace TicTacToeNet.TelegramBot.Configurations;

internal sealed class TelegramApiConfiguration
{
    internal static string ConfigurationKey = "TelegramApi";

    public string? ApiKey { get; set; }

    public int Timeout { get; set; } = 40;
}
