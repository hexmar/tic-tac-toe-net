namespace TicTacToeNet.TelegramBot;

#pragma warning disable CA1812
internal sealed partial class PrimesBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Dictionary<int, int> primes = new(100_000);

        for (var i = 3; i <= 1_000_000; i += 2)
        {
            var isPrime = true;
            foreach (var primefilterKey in primes.Keys)
            {
                var primeFilter = primes[primefilterKey];
                while (primeFilter < i)
                {
                    primeFilter += primefilterKey;
                }

                primes[primefilterKey] = primeFilter;

                if (primeFilter == i)
                {
                    isPrime = false;
                    break;
                }
            }

            if (isPrime)
            {
                primes.Add(i, i);
            }
        }

        primes.Add(2, 2);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {workerId} got {data}")]
    private static partial void LogWorkerData(ILogger logger, string workerId, string data);
}
