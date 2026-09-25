using System.Globalization;
using TicTacToeNet.TelegramBot.Clients;
using TicTacToeNet.TelegramBot.Services;
using static TicTacToeNet.TelegramBot.Clients.TelegramBotApiTypes;

namespace TicTacToeNet.TelegramBot;

#pragma warning disable CA1812
internal sealed partial class TelegramBackgroundService(
    ITelegramUpdateQueue telegramUpdateQueue,
    ITelegramBotApiClient apiClient,
    ILogger<TelegramBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var response = await apiClient.GetUpdateDataAsync(offset, stoppingToken);
            if (response is null)
            {
                continue;
            }

            using (response)
            {
                var result = response.RootElement.GetProperty("result");
                if (result.GetArrayLength() == 0)
                {
                    continue;
                }

                var update = result[0];
                offset = update.GetProperty("update_id").GetInt32() + 1;

                if (update.TryGetProperty("message", out var message))
                {
                    var text = message.GetProperty("text").GetString() ?? string.Empty;

                    if (text.Contains("/start", StringComparison.Ordinal))
                    {
                        var name = string.Empty;
                        if (message.TryGetProperty("from", out var messageSender)
                            && messageSender.TryGetProperty("first_name", out var senderName))
                        {
                            name = senderName.GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(name))
                            {
                                name = $" {name}";
                            }
                        }

                        var welcomeMessage = new ChatTextMessage
                        {
                            ChatId = message.GetProperty("chat").GetProperty("id").GetInt32(),
                            Text = $"Hi{name}! Want to play a game?"
                        };
                        await apiClient.SendMessageAsync(welcomeMessage, stoppingToken);
                        continue;
                    }
                    else if (text.Contains("/newgame", StringComparison.Ordinal))
                    {
                        var gameMessage = new ChatGameMessage
                        {
                            ChatId = message.GetProperty("chat").GetProperty("id").GetInt32(),
                            Text = "Let's play",
                            ReplyMarkup = new()
                            {
                                InlineKeyboard = [
                                    [
                                        new() { Text = "-", CallbackData = "0" },
                                        new() { Text = "-", CallbackData = "1" },
                                        new() { Text = "-", CallbackData = "2" },],
                                    [
                                        new() { Text = "-", CallbackData = "3" },
                                        new() { Text = "-", CallbackData = "4" },
                                        new() { Text = "-", CallbackData = "5" },],
                                    [
                                        new() { Text = "-", CallbackData = "6" },
                                        new() { Text = "-", CallbackData = "7" },
                                        new() { Text = "-", CallbackData = "8" },],]
                            }
                        };
                        await apiClient.SendMessageAsync(gameMessage, stoppingToken);
                        continue;
                    }
                }
                else if (update.TryGetProperty("guest_message", out var guestMessage))
                {
                    var entities = guestMessage.GetProperty("entities");
                    var mentionsEntities = entities.EnumerateArray()
                        .Where(e => e.GetProperty("type").GetString() == "mention")
                        .ToArray();
                    if (mentionsEntities.Length != 2)
                    {
                        var formatMessage = new GuestTextMessage
                        {
                            GuestQueryId = guestMessage.GetProperty("guest_query_id").GetString()!,
                            Result = new()
                            {
                                Id = 15,
                                Content = new()
                                {
                                    Text = """
                                    The message is not clear to me. Please use the following format
                                    ```
                                    @RubyTicTacBot /newgame with @<username>
                                    ```
                                    """
                                },
                            },
                        };
                        await apiClient.SendAnswerGuestQuery(formatMessage, stoppingToken);
                        continue;
                    }

                    var text = guestMessage.GetProperty("text").GetString()!;
                    var mentions = mentionsEntities
                        .Select(m => text.Substring(
                            m.GetProperty("offset").GetInt32(),
                            m.GetProperty("length").GetInt32()))
                        .ToArray();

                    var mentionedUsername = mentions[0].Equals("@RubyTicTacBot", StringComparison.OrdinalIgnoreCase)
                        ? mentions[1]
                        : mentions[0];

                    if (mentionedUsername.Equals("@RubyTicTacBot", StringComparison.OrdinalIgnoreCase)
                        || !text.Contains("/newgame", StringComparison.Ordinal))
                    {
                        var formatMessage = new GuestTextMessage
                        {
                            GuestQueryId = guestMessage.GetProperty("guest_query_id").GetString()!,
                            Result = new()
                            {
                                Id = 15,
                                Content = new()
                                {
                                    Text = """
                                    The message is not clear to me. Please use the following format
                                    ```
                                    @RubyTicTacBot /newgame with @<username>
                                    ```
                                    """
                                },
                            },
                        };
                        await apiClient.SendAnswerGuestQuery(formatMessage, stoppingToken);
                        continue;
                    }

                    mentionedUsername = mentionedUsername.Substring(1);
                    var senderUsername = guestMessage
                        .GetProperty("from")
                        .GetProperty("username")
                        .GetString();

                    var currentTurnUser = mentionedUsername;
                    var nextTurnUser = senderUsername;
#pragma warning disable CA5394 // Do not use insecure randomness
                    var userSelect = Random.Shared.Next(2);
#pragma warning restore CA5394 // Do not use insecure randomness
                    if (userSelect == 0)
                    {
                        (currentTurnUser, nextTurnUser) = (nextTurnUser, currentTurnUser);
                    }

                    var baseCallbackData = $"{currentTurnUser}|{nextTurnUser}|---------|";
                    if (baseCallbackData.Length > 63)
                    {
                        logger.LogWarning("Too long usernames: {Sender} {Mentioned}", senderUsername, mentionedUsername);
                        var formatMessage = new GuestTextMessage
                        {
                            GuestQueryId = guestMessage.GetProperty("guest_query_id").GetString()!,
                            Result = new()
                            {
                                Id = 11,
                                Content = new()
                                {
                                    Text = """
                                    Sorry, I can't start a game at the moment
                                    """
                                },
                            },
                        };
                        await apiClient.SendAnswerGuestQuery(formatMessage, stoppingToken);
                        continue;
                    }

                    var gameMessage = new GuestGameMessage
                    {
                        GuestQueryId = guestMessage.GetProperty("guest_query_id").GetString()!,
                        Result = new()
                        {
#pragma warning disable CA5394 // Do not use insecure randomness
                            Id = Random.Shared.Next(1000, 100_000),
#pragma warning restore CA5394 // Do not use insecure randomness
                            Content = new()
                            {
                                Text = $"""
                                Let's play
                                @{currentTurnUser}, your turn
                                """
                            },
                            ReplyMarkup = new()
                            {
                                InlineKeyboard = [
                                    [
                                        new() { Text = "-", CallbackData = baseCallbackData + "0" },
                                        new() { Text = "-", CallbackData = baseCallbackData + "1" },
                                        new() { Text = "-", CallbackData = baseCallbackData + "2" },],
                                    [
                                        new() { Text = "-", CallbackData = baseCallbackData + "3" },
                                        new() { Text = "-", CallbackData = baseCallbackData + "4" },
                                        new() { Text = "-", CallbackData = baseCallbackData + "5" },],
                                    [
                                        new() { Text = "-", CallbackData = baseCallbackData + "6" },
                                        new() { Text = "-", CallbackData = baseCallbackData + "7" },
                                        new() { Text = "-", CallbackData = baseCallbackData + "8" },],]
                            }
                        },
                    };

                    await apiClient.SendAnswerGuestQuery(gameMessage, stoppingToken);
                }
                else if (update.TryGetProperty("callback_query", out var callback))
                {
                    // if message.chat exists -> private message
                    // if inline_message_id -> guest mode message. For guest mode need to keep state.

                    if (callback.TryGetProperty("message", out var messageForCallback)
                        && messageForCallback.TryGetProperty("reply_markup", out var replyMarkup)
                        && replyMarkup.TryGetProperty("inline_keyboard", out var replyKeyboard))
                    {
                        var state = new char[9];
                        for (var outer = 0; outer < 3; outer++)
                        {
                            var replyKeyboardRow = replyKeyboard[outer];
                            for (var inner = 0; inner < 3; inner++)
                            {
                                var replyButton = replyKeyboardRow[inner];
                                var index = int.Parse(replyButton.GetProperty("callback_data").GetString() ?? string.Empty, CultureInfo.InvariantCulture);
                                var value = replyButton.GetProperty("text").GetString()![0];
                                state[index] = value;
                            }
                        }

                        var selectedIndex = int.Parse(callback.GetProperty("data").GetString() ?? string.Empty, CultureInfo.InvariantCulture);

                        if (state[selectedIndex] != '-')
                        {
                            var callbackAnswerForFilledCell = new CallbackQueryAnswer
                            {
                                Id = callback.GetProperty("id").GetString() ?? string.Empty,
                                Text = "The cell is already filled",
                                ShowAlert = true,
                                CacheTime = 30,
                            };
                            await apiClient.SendAnswerCallbackQuery(callbackAnswerForFilledCell, stoppingToken);

                            continue;
                        }

                        var callbackAnswerForOk = new CallbackQueryAnswer
                        {
                            Id = callback.GetProperty("id").GetString() ?? string.Empty,
                        };
                        await apiClient.SendAnswerCallbackQuery(callbackAnswerForOk, stoppingToken);

                        state[selectedIndex] = 'X';
                        var stateSum = CheckWin(state);
                        if (stateSum == 'X')
                        {
                            var wonMessage = new EditChatTextMessage()
                            {
                                ChatId = messageForCallback.GetProperty("chat").GetProperty("id").GetInt32(),
                                MessageId = messageForCallback.GetProperty("message_id").GetInt32().ToString(CultureInfo.InvariantCulture),
                                Text = $"""
                                You won
                                ```
                                {state[0]} {state[1]} {state[2]}
                                {state[3]} {state[4]} {state[5]}
                                {state[6]} {state[7]} {state[8]}
                                ```
                                """,
                            };
                            await apiClient.EditMessageAsync(wonMessage, stoppingToken);

                            continue;
                        }

                        var emptyCellsIndexes = new List<int>(9);
                        for (var i = 0; i < 9; i++)
                        {
                            if (state[i] == '-')
                            {
                                emptyCellsIndexes.Add(i);
                            }
                        }

                        if (emptyCellsIndexes.Count == 0)
                        {
                            var drawMessage = new EditChatTextMessage()
                            {
                                ChatId = messageForCallback.GetProperty("chat").GetProperty("id").GetInt32(),
                                MessageId = messageForCallback.GetProperty("message_id").GetInt32().ToString(CultureInfo.InvariantCulture),
                                Text = $"""
                                Draw
                                ```
                                {state[0]} {state[1]} {state[2]}
                                {state[3]} {state[4]} {state[5]}
                                {state[6]} {state[7]} {state[8]}
                                ```
                                """,
                            };
                            await apiClient.EditMessageAsync(drawMessage, stoppingToken);

                            continue;
                        }

                        if (emptyCellsIndexes.Count == 1)
                        {
                            state[emptyCellsIndexes[0]] = 'O';
                            stateSum = CheckWin(state);
                            if (stateSum == '-')
                            {
                                var drawMessage = new EditChatTextMessage()
                                {
                                    ChatId = messageForCallback.GetProperty("chat").GetProperty("id").GetInt32(),
                                    MessageId = messageForCallback.GetProperty("message_id").GetInt32().ToString(CultureInfo.InvariantCulture),
                                    Text = $"""
                                    It's a draw
                                    ```
                                    {state[0]} {state[1]} {state[2]}
                                    {state[3]} {state[4]} {state[5]}
                                    {state[6]} {state[7]} {state[8]}
                                    ```
                                    """,
                                };
                                await apiClient.EditMessageAsync(drawMessage, stoppingToken);

                                continue;
                            }

                            var wonMessage = new EditChatTextMessage()
                            {
                                ChatId = messageForCallback.GetProperty("chat").GetProperty("id").GetInt32(),
                                MessageId = messageForCallback.GetProperty("message_id").GetInt32().ToString(CultureInfo.InvariantCulture),
                                Text = $"""
                                Bot won
                                ```
                                {state[0]} {state[1]} {state[2]}
                                {state[3]} {state[4]} {state[5]}
                                {state[6]} {state[7]} {state[8]}
                                ```
                                """,
                            };
                            await apiClient.EditMessageAsync(wonMessage, stoppingToken);

                            continue;
                        }

#pragma warning disable CA5394 // Do not use insecure randomness
                        var botSelectedIndex = Random.Shared.Next(emptyCellsIndexes.Count);
#pragma warning restore CA5394 // Do not use insecure randomness
                        state[emptyCellsIndexes[botSelectedIndex]] = 'O';
                        stateSum = CheckWin(state);
                        if (stateSum == 'O')
                        {
                            var wonMessage = new EditChatTextMessage()
                            {
                                ChatId = messageForCallback.GetProperty("chat").GetProperty("id").GetInt32(),
                                MessageId = messageForCallback.GetProperty("message_id").GetInt32().ToString(CultureInfo.InvariantCulture),
                                Text = $"""
                                Bot won
                                ```
                                {state[0]} {state[1]} {state[2]}
                                {state[3]} {state[4]} {state[5]}
                                {state[6]} {state[7]} {state[8]}
                                ```
                                """,
                            };
                            await apiClient.EditMessageAsync(wonMessage, stoppingToken);

                            continue;
                        }

                        var gameMessage = new EditChatGameMessage()
                        {
                            ChatId = messageForCallback.GetProperty("chat").GetProperty("id").GetInt32(),
                            MessageId = messageForCallback.GetProperty("message_id").GetInt32().ToString(CultureInfo.InvariantCulture),
                            Text = "Let's play",
                            ReplyMarkup = new()
                            {
                                InlineKeyboard = [
                                    [
                                        new() { Text = state[0].ToString(), CallbackData = "0" },
                                        new() { Text = state[1].ToString(), CallbackData = "1" },
                                        new() { Text = state[2].ToString(), CallbackData = "2" },],
                                    [
                                        new() { Text = state[3].ToString(), CallbackData = "3" },
                                        new() { Text = state[4].ToString(), CallbackData = "4" },
                                        new() { Text = state[5].ToString(), CallbackData = "5" },],
                                    [
                                        new() { Text = state[6].ToString(), CallbackData = "6" },
                                        new() { Text = state[7].ToString(), CallbackData = "7" },
                                        new() { Text = state[8].ToString(), CallbackData = "8" },],]
                            }
                        };
                        await apiClient.EditMessageAsync(gameMessage, stoppingToken);
                    }
                    else if (callback.TryGetProperty("inline_message_id", out var inlineMessageId))
                    {
                        var data = callback.GetProperty("data").GetString()!;
                        if (data.Split('|') is [var currentTurnUser, var nextTurnUser, var stateString, var indexString])
                        {
                            var querySender = callback.GetProperty("from").GetProperty("username").GetString()!;
                            if (!currentTurnUser.Equals(querySender, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!nextTurnUser.Equals(querySender, StringComparison.OrdinalIgnoreCase))
                                {
                                    var callbackAnswerForNonPlayer = new CallbackQueryAnswer
                                    {
                                        Id = callback.GetProperty("id").GetString() ?? string.Empty,
                                        Text = "You are not part of this game",
                                        ShowAlert = true,
                                        CacheTime = 30,
                                    };
                                    await apiClient.SendAnswerCallbackQuery(callbackAnswerForNonPlayer, stoppingToken);
                                    continue;
                                }

                                var callbackAnswerForDoubleTurn = new CallbackQueryAnswer
                                {
                                    Id = callback.GetProperty("id").GetString() ?? string.Empty,
                                    Text = "Please wait for your turn",
                                    ShowAlert = true,
                                    CacheTime = 30,
                                };
                                await apiClient.SendAnswerCallbackQuery(callbackAnswerForDoubleTurn, stoppingToken);
                                continue;
                            }

                            var selectedIndex = int.Parse(indexString, CultureInfo.InvariantCulture);
                            if (stateString[selectedIndex] != '-')
                            {
                                var callbackAnswerForFilledCell = new CallbackQueryAnswer
                                {
                                    Id = callback.GetProperty("id").GetString() ?? string.Empty,
                                    Text = "The cell is already filled",
                                    ShowAlert = true,
                                    CacheTime = 30,
                                };
                                await apiClient.SendAnswerCallbackQuery(callbackAnswerForFilledCell, stoppingToken);
                                continue;
                            }

                            var callbackAnswerForOk = new CallbackQueryAnswer
                            {
                                Id = callback.GetProperty("id").GetString() ?? string.Empty,
                            };
                            await apiClient.SendAnswerCallbackQuery(callbackAnswerForOk, stoppingToken);

                            var state = stateString.ToCharArray();
                            var countOfEmptyCells = state.Count(c => c == '-');
                            var currentTurnMark = countOfEmptyCells % 2 == 0 ? 'O' : 'X';
                            state[selectedIndex] = currentTurnMark;
                            var stateSum = CheckWin(state);

                            if (stateSum == currentTurnMark)
                            {
                                var wonMessage = new EditInlineTextMessage()
                                {
                                    MessageId = inlineMessageId.GetString()!,
                                    Text = $"""
                                    @{currentTurnUser} won
                                    ```
                                    {state[0]} {state[1]} {state[2]}
                                    {state[3]} {state[4]} {state[5]}
                                    {state[6]} {state[7]} {state[8]}
                                    ```
                                    """,
                                };
                                await apiClient.EditMessageAsync(wonMessage, stoppingToken);

                                continue;
                            }
                            else if (countOfEmptyCells == 1)
                            {
                                var drawMessage = new EditInlineTextMessage()
                                {
                                    MessageId = inlineMessageId.GetString()!,
                                    Text = $"""
                                    It's a draw
                                    ```
                                    {state[0]} {state[1]} {state[2]}
                                    {state[3]} {state[4]} {state[5]}
                                    {state[6]} {state[7]} {state[8]}
                                    ```
                                    """,
                                };
                                await apiClient.EditMessageAsync(drawMessage, stoppingToken);

                                continue;
                            }

                            var baseCallbackData = $"{nextTurnUser}|{currentTurnUser}|{string.Join("", state)}|";
                            var gameMessage = new EditInlineGameMessage()
                            {
                                MessageId = inlineMessageId.GetString()!,
                                Text = $"""
                                Let's play
                                @{nextTurnUser}, your turn
                                """,
                                ReplyMarkup = new()
                                {
                                    InlineKeyboard = [
                                    [
                                        new() { Text = state[0].ToString(), CallbackData = baseCallbackData + "0" },
                                        new() { Text = state[1].ToString(), CallbackData = baseCallbackData + "1" },
                                        new() { Text = state[2].ToString(), CallbackData = baseCallbackData + "2" },],
                                    [
                                        new() { Text = state[3].ToString(), CallbackData = baseCallbackData + "3" },
                                        new() { Text = state[4].ToString(), CallbackData = baseCallbackData + "4" },
                                        new() { Text = state[5].ToString(), CallbackData = baseCallbackData + "5" },],
                                    [
                                        new() { Text = state[6].ToString(), CallbackData = baseCallbackData + "6" },
                                        new() { Text = state[7].ToString(), CallbackData = baseCallbackData + "7" },
                                        new() { Text = state[8].ToString(), CallbackData = baseCallbackData + "8" },],]
                                }
                            };
                            await apiClient.EditMessageAsync(gameMessage, stoppingToken);
                        }
                    }
                }
            }
        }

        Environment.Exit(0);
    }

    private static char CheckWin(char[] state)
    {
        // 0 = 1 = 2
        if (state[0] != '-' && state[0] == state[1] && state[1] == state[2])
        {
            return state[0];
        }

        // 3 = 4 = 5
        if (state[3] != '-' && state[3] == state[4] && state[4] == state[5])
        {
            return state[3];
        }

        // 6 = 7 = 8
        if (state[6] != '-' && state[6] == state[7] && state[7] == state[8])
        {
            return state[6];
        }

        // 0 = 3 = 6
        if (state[0] != '-' && state[0] == state[3] && state[3] == state[6])
        {
            return state[0];
        }

        // 1 = 4 = 7
        if (state[1] != '-' && state[1] == state[4] && state[4] == state[7])
        {
            return state[1];
        }

        // 2 = 5 = 8
        if (state[2] != '-' && state[2] == state[5] && state[5] == state[8])
        {
            return state[2];
        }

        // 0 = 4 = 8
        if (state[0] != '-' && state[0] == state[4] && state[4] == state[8])
        {
            return state[0];
        }

        // 2 = 4 = 6
        if (state[2] != '-' && state[2] == state[4] && state[4] == state[6])
        {
            return state[2];
        }

        return '-';
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {workerId} got {data}")]
    private static partial void LogWorkerData(ILogger logger, string workerId, string data);
}
