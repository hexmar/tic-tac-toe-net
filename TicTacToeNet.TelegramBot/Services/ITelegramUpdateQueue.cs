namespace TicTacToeNet.TelegramBot.Services;

internal interface ITelegramUpdateQueue
{
    ValueTask QueueUpdateItemAsync(string updateData, CancellationToken cancellationToken);
    ValueTask<string> GetUpdateItemAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<string> GetEnumerableAsync(CancellationToken cancellationToken);
}
