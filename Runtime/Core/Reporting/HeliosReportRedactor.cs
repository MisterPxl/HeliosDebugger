using System;
using System.Collections.Generic;
using System.Text;

namespace HeliosDebugger
{
    public interface IHeliosReportRedactor
    {
        string Redact(string text);
    }

    public sealed class HeliosReportRedactor : IHeliosReportRedactor
    {
        private static readonly string[] DefaultSensitiveKeys =
        {
            "authorization",
            "access_token",
            "refresh_token",
            "client_secret",
            "api_key",
            "api-key",
            "password",
            "secret",
            "token"
        };

        private readonly List<string> _sensitiveKeys;
        private readonly string _replacement;

        public HeliosReportRedactor()
            : this(DefaultSensitiveKeys, "<redacted>")
        {
        }

        public HeliosReportRedactor(IEnumerable<string> sensitiveKeys, string replacement)
        {
            if (sensitiveKeys == null)
                throw new ArgumentNullException("sensitiveKeys");

            _sensitiveKeys = new List<string>();
            foreach (string sensitiveKey in sensitiveKeys)
            {
                if (!string.IsNullOrWhiteSpace(sensitiveKey))
                    _sensitiveKeys.Add(sensitiveKey.Trim());
            }

            _sensitiveKeys.Sort(CompareLongestFirst);
            _replacement = replacement ?? string.Empty;
        }

        public string Redact(string text)
        {
            if (string.IsNullOrEmpty(text) || _sensitiveKeys.Count == 0)
                return text ?? string.Empty;

            StringBuilder result = new StringBuilder(text.Length);
            int cursor = 0;

            while (cursor < text.Length)
            {
                SensitiveMatch match = FindNextMatch(text, cursor);
                if (match.Index < 0)
                {
                    result.Append(text, cursor, text.Length - cursor);
                    break;
                }

                int delimiterIndex = match.Index + match.Key.Length;
                if (delimiterIndex < text.Length &&
                    (text[delimiterIndex] == '"' || text[delimiterIndex] == '\'') &&
                    match.Index > 0 &&
                    text[match.Index - 1] == text[delimiterIndex])
                {
                    delimiterIndex++;
                }

                while (delimiterIndex < text.Length && IsHorizontalWhitespace(text[delimiterIndex]))
                    delimiterIndex++;

                if (delimiterIndex >= text.Length ||
                    (text[delimiterIndex] != '=' && text[delimiterIndex] != ':'))
                {
                    int copyLength = match.Index + match.Key.Length - cursor;
                    result.Append(text, cursor, copyLength);
                    cursor += copyLength;
                    continue;
                }

                int valueStart = delimiterIndex + 1;
                while (valueStart < text.Length && IsHorizontalWhitespace(text[valueStart]))
                    valueStart++;

                result.Append(text, cursor, valueStart - cursor);
                if (valueStart >= text.Length || text[valueStart] == '\r' || text[valueStart] == '\n')
                {
                    cursor = valueStart;
                    continue;
                }

                char quote = text[valueStart];
                if (quote == '"' || quote == '\'')
                {
                    result.Append(quote);
                    int quotedValueStart = valueStart + 1;
                    int quotedValueEnd = FindClosingQuote(text, quotedValueStart, quote);
                    result.Append(_replacement);

                    if (quotedValueEnd < text.Length)
                    {
                        result.Append(quote);
                        cursor = quotedValueEnd + 1;
                    }
                    else
                    {
                        cursor = FindLineEnd(text, quotedValueStart);
                    }
                }
                else
                {
                    int valueEnd = IsAuthorizationKey(match.Key)
                        ? FindLineEnd(text, valueStart)
                        : FindUnquotedValueEnd(text, valueStart);
                    result.Append(_replacement);
                    cursor = valueEnd;
                }
            }

            return result.ToString();
        }

        private SensitiveMatch FindNextMatch(string text, int startIndex)
        {
            int bestIndex = -1;
            string bestKey = null;

            for (int keyIndex = 0; keyIndex < _sensitiveKeys.Count; keyIndex++)
            {
                string key = _sensitiveKeys[keyIndex];
                int searchIndex = startIndex;
                while (searchIndex < text.Length)
                {
                    int index = text.IndexOf(key, searchIndex, StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                        break;

                    int endIndex = index + key.Length;
                    if (IsBoundary(text, index - 1) && IsBoundary(text, endIndex))
                    {
                        if (bestIndex < 0 || index < bestIndex ||
                            (index == bestIndex && key.Length > bestKey.Length))
                        {
                            bestIndex = index;
                            bestKey = key;
                        }
                        break;
                    }

                    searchIndex = index + key.Length;
                }
            }

            return new SensitiveMatch(bestIndex, bestKey);
        }

        private static bool IsBoundary(string text, int index)
        {
            return index < 0 || index >= text.Length ||
                   (!char.IsLetterOrDigit(text[index]) && text[index] != '_');
        }

        private static bool IsHorizontalWhitespace(char character)
        {
            return character == ' ' || character == '\t';
        }

        private static int FindClosingQuote(string text, int startIndex, char quote)
        {
            bool escaped = false;
            for (int i = startIndex; i < text.Length; i++)
            {
                char character = text[i];
                if (character == '\r' || character == '\n')
                    return text.Length;

                if (character == quote && !escaped)
                    return i;

                if (character == '\\' && !escaped)
                    escaped = true;
                else
                    escaped = false;
            }

            return text.Length;
        }

        private static int FindLineEnd(string text, int startIndex)
        {
            int index = startIndex;
            while (index < text.Length && text[index] != '\r' && text[index] != '\n')
                index++;
            return index;
        }

        private static int FindUnquotedValueEnd(string text, int startIndex)
        {
            int index = startIndex;
            while (index < text.Length)
            {
                char character = text[index];
                if (character == '\r' || character == '\n' || character == '&' ||
                    character == ';' || character == ',' || character == '}' ||
                    character == ']')
                {
                    break;
                }

                index++;
            }

            return index;
        }

        private static bool IsAuthorizationKey(string key)
        {
            return string.Equals(key, "authorization", StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareLongestFirst(string left, string right)
        {
            return right.Length.CompareTo(left.Length);
        }

        private struct SensitiveMatch
        {
            public SensitiveMatch(int index, string key)
            {
                Index = index;
                Key = key;
            }

            public int Index;
            public string Key;
        }
    }
}
