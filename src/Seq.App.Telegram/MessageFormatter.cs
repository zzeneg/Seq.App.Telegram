using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog;

namespace Seq.App.Telegram
{
    public class MessageFormatter
    {
        static readonly Regex PlaceholdersRegex = new Regex("(\\[(?<key>[^\\[\\]]+?)(\\:(?<format>[^\\[\\]]+?))?\\])", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public MessageFormatter(ILogger log, string baseUrl, string messageTemplate)
        {
            Log = log;
            MessageTemplate = messageTemplate ?? "[RenderedMessage] [link]([BaseUrl]/#/events?filter=@Id%3D%3D'[EventId]'&show=expanded)";
            BaseUrl = baseUrl;
        }

        public ILogger Log { get; }
        public string MessageTemplate { get; }
        public string BaseUrl { get; }

        public string GenerateMessageText(Event<LogEventData> evt)
        {
            return $"{SubstitutePlaceholders(MessageTemplate, evt)}";
        }

        string SubstitutePlaceholders(string messageTemplateToUse, Event<LogEventData> evt)
        {
            var data = evt.Data;

            var placeholders = data.Properties?.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            AddValueIfKeyDoesntExist(placeholders, "Level", data.Level);
            AddValueIfKeyDoesntExist(placeholders, "EventType", evt.EventType);
            AddValueIfKeyDoesntExist(placeholders, "RenderedMessage", data.RenderedMessage);
            AddValueIfKeyDoesntExist(placeholders, "Exception", data.Exception);
            AddValueIfKeyDoesntExist(placeholders, "Id", data.Id);
            AddValueIfKeyDoesntExist(placeholders, "LocalTimestamp", data.LocalTimestamp);
            AddValueIfKeyDoesntExist(placeholders, "Timestamp", evt.TimestampUtc);
            AddValueIfKeyDoesntExist(placeholders, "EventId", evt.Id);
            AddValueIfKeyDoesntExist(placeholders, "BaseUrl", BaseUrl);

            return PlaceholdersRegex.Replace(messageTemplateToUse, m =>
            {
                var key = m.Groups["key"].Value.ToLower();
                var format = m.Groups["format"].Value;
                return placeholders.ContainsKey(key) ? FormatValue(placeholders[key], format) : m.Value;
            });
        }

        string FormatValue(object value, string format)
        {
            var rawValue = value?.ToString() ?? "(Null)";

            if (string.IsNullOrWhiteSpace(format))
                return rawValue;
            try
            {
                return string.Format(format, rawValue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not format message: {value} {format}", value, format);
            }

            return rawValue;
        }

        static void AddValueIfKeyDoesntExist(IDictionary<string, object> placeholders, string key, object value)
        {
            if (!placeholders.ContainsKey(key))
                placeholders.Add(key, value);
        }
    }
}
