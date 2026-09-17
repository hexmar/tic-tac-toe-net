using System.Threading.Channels;

namespace TicTacToeNet.TelegramBot.Services;

#pragma warning disable CA1812
internal sealed class TelegramUpdateQueue : ITelegramUpdateQueue
{
    private readonly Channel<string> _queue;

    public TelegramUpdateQueue(int capacity)
    {
        BoundedChannelOptions options = new(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false,
        };
        _queue = Channel.CreateBounded<string>(options);
    }

    public ValueTask QueueUpdateItemAsync(string updateData, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updateData);

        return _queue.Writer.WriteAsync(updateData, cancellationToken);
    }

    public ValueTask<string> GetUpdateItemAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }

    public IAsyncEnumerable<string> GetEnumerableAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
