namespace TicTacToeNet.TelegramBot.Ai;

/// <summary>
/// Decides which empty cell the bot ('O') plays on.
/// </summary>
internal interface IAiMoveStrategy
{
    /// <summary>
    /// Picks a cell index (0..8) for the given board state.
    /// </summary>
    /// <param name="state">
    /// The 9-element board. Indices 0–8, row-major.
    /// Values: '-' = empty, 'X' = human, 'O' = bot.
    /// Must contain at least one '-' when called
    /// (the caller handles the full-board edge case before calling).
    /// Empty cells are derivable from the state — the strategy scans it itself.
    /// </param>
    /// <returns>An index where <c>state[index] == '-'</c>.</returns>
    int PickMove(char[] state);
}
