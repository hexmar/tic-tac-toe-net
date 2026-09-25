using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TicTacToeNet.TelegramBot.Configurations;
using static TicTacToeNet.TelegramBot.Clients.TelegramBotApiTypes;

namespace TicTacToeNet.TelegramBot.Clients;

internal class TelegramBotApiClient(
    HttpClient client,
    ILogger<TelegramBotApiClient> logger)
    : ITelegramBotApiClient
{
    public async Task<JsonDocument?> GetUpdateDataAsync(int offset, CancellationToken cancellationToken)
    {
        try
        {
            var offsetQuery = offset > 0 ? $"&offset={offset}" : string.Empty;
            var timeout = Convert.ToInt32(client.Timeout.TotalSeconds) - 10;
            var response = await client.GetAsync($"getUpdates?limit=1&timeout={timeout}{offsetQuery}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var jsonDoc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

                return jsonDoc;
            }
            else
            {
                using var scope = logger.BeginScope("Error content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken));
                logger.LogWarning("Get update ended with {Code} code", response.StatusCode);

                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Get update failed");

            return null;
        }
    }

    public async Task SendMessageAsync<T>(T message, CancellationToken cancellationToken) where T : ChatTextMessage
    {
        try
        {
            var response = await client.PostAsJsonAsync("sendMessage", message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                using var scope = logger.BeginScope("Error content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken));
                logger.LogWarning("Send text message ended with {Code} code", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Send text message failed");
        }
    }

    public async Task SendAnswerCallbackQuery(CallbackQueryAnswer message, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.PostAsJsonAsync("answerCallbackQuery", message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                using var scope = logger.BeginScope("Error content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken));
                logger.LogWarning("Send callback answer ended with {Code} code", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Send callback answer failed");
        }
    }

    public async Task EditMessageAsync<T>(T message, CancellationToken cancellationToken) where T : IEditTextMessage
    {
        try
        {
            var response = await client.PostAsJsonAsync("editMessageText", message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                using var scope = logger.BeginScope("Error content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken));
                logger.LogWarning("Edit text message ended with {Code} code", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Edit text message failed");
        }
    }

    public async Task SendAnswerGuestQuery<T>(GuestQueryAnswer<T> message, CancellationToken cancellationToken) where T : GuestTextMessageResult
    {
        try
        {
            var response = await client.PostAsJsonAsync("answerGuestQuery", message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                using var scope = logger.BeginScope("Error content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken));
                logger.LogWarning("Send guest answer ended with {Code} code", response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Send guest answer failed");
        }
    }
}
