using System.Text.Json.Serialization;

namespace TicTacToeNet.TelegramBot.Clients;

internal static class TelegramBotApiTypes
{
    internal interface IEditTextMessage
    {
        string MessageId { get; set; }
    }

    internal class TextMessage
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }

        [JsonPropertyName("parse_mode")]
        public string ParseMode { get; set; } = "Markdown";
    }

    internal class ChatTextMessage : TextMessage
    {
        [JsonPropertyName("chat_id")]
        public required int ChatId { get; set; }
    }

    internal class GuestQueryAnswer<T> where T : GuestTextMessageResult
    {
        [JsonPropertyName("guest_query_id")]
        public required string GuestQueryId { get; set; }

        [JsonPropertyName("result")]
        public required T Result { get; set; }
    }

    internal class GuestTextMessage : GuestQueryAnswer<GuestTextMessageResult>
    { }

    internal class GuestTextMessageResult
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "article";

        [JsonPropertyName("id")]
        public required int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "TicTacToe game by @RubyTicTacBot";

        [JsonPropertyName("input_message_content")]
        public required GuestTextMessageContent Content { get; set; }
    }

    internal class GuestTextMessageContent
    {
        [JsonPropertyName("message_text")]
        public required string Text { get; set; }

        [JsonPropertyName("parse_mode")]
        public string ParseMode { get; set; } = "Markdown";
    }

    internal class EditChatTextMessage : ChatTextMessage, IEditTextMessage
    {
        [JsonPropertyName("message_id")]
        public required string MessageId { get; set; }
    }

    internal class EditInlineTextMessage : TextMessage, IEditTextMessage
    {
        [JsonPropertyName("inline_message_id")]
        public required string MessageId { get; set; }
    }

    internal sealed class GameMessageReplyMarkup
    {
        [JsonPropertyName("inline_keyboard")]
        public required GameInlineButton[][] InlineKeyboard { get; set; }
    }

    internal sealed class GameInlineButton
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }

        [JsonPropertyName("callback_data")]
        public required string CallbackData { get; set; }
    }

    internal sealed class ChatGameMessage : ChatTextMessage
    {
        [JsonPropertyName("reply_markup")]
        public required GameMessageReplyMarkup ReplyMarkup { get; set; }
    }

    internal sealed class GuestGameMessage : GuestQueryAnswer<GuestGameMessageResult>
    { }

    internal sealed class GuestGameMessageResult : GuestTextMessageResult
    {
        [JsonPropertyName("reply_markup")]
        public required GameMessageReplyMarkup ReplyMarkup { get; set; }
    }

    internal sealed class EditChatGameMessage : EditChatTextMessage, IEditTextMessage
    {
        [JsonPropertyName("reply_markup")]
        public required GameMessageReplyMarkup ReplyMarkup { get; set; }
    }

    internal sealed class EditInlineGameMessage : EditInlineTextMessage, IEditTextMessage
    {
        [JsonPropertyName("reply_markup")]
        public required GameMessageReplyMarkup ReplyMarkup { get; set; }
    }

    internal sealed class CallbackQueryAnswer
    {
        [JsonPropertyName("callback_query_id")]
        public required string Id { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("show_alert")]
        public bool ShowAlert { get; set; }

        [JsonPropertyName("cache_time")]
        public int CacheTime { get; set; }
    }
}
