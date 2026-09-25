using System.Text.Json;
using static TicTacToeNet.TelegramBot.Clients.TelegramBotApiTypes;

namespace TicTacToeNet.TelegramBot.Clients;

internal interface ITelegramBotApiClient
{
    Task<JsonDocument?> GetUpdateDataAsync(int offset, CancellationToken cancellationToken);
    Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : ChatTextMessage;
    Task SendAnswerCallbackQuery(CallbackQueryAnswer message, CancellationToken cancellationToken);
    Task EditMessageAsync<T>(T message, CancellationToken cancellationToken) where T : IEditTextMessage;
    Task SendAnswerGuestQuery<T>(GuestQueryAnswer<T> message, CancellationToken cancellationToken) where T : GuestTextMessageResult;
}
