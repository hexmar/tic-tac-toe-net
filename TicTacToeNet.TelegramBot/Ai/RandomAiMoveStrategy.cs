namespace TicTacToeNet.TelegramBot.Ai;

#pragma warning disable CA1812
internal sealed class RandomAiMoveStrategy : IAiMoveStrategy
{
    public int PickMove(char[] state)
    {
        var empty = new List<int>(9);
        for (var i = 0; i < state.Length; i++)
        {
            if (state[i] == '-')
            {
                empty.Add(i);
            }
        }

#pragma warning disable CA5394 // Do not use insecure randomness
        return empty[Random.Shared.Next(empty.Count)];
#pragma warning restore CA5394 // Do not use insecure randomness
    }
}
