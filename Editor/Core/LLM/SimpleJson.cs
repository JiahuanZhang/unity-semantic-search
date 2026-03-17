using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SemanticSearch.Editor.Core.LLM
{
    /// <summary>
    /// 轻量 JSON 解析/生成工具，不依赖第三方库。
    /// 支持 object(Dictionary), array(List), string, number(double), bool, null。
    /// </summary>
    public static class SimpleJson
    {
        #region Serialize

        public static string Serialize(object obj)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, obj);
            return sb.ToString();
        }

        static void SerializeValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is string s)
            {
                SerializeString(sb, s);
                return;
            }

            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }

            if (value is IDictionary<string, object> dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    SerializeString(sb, kv.Key);
                    sb.Append(':');
                    SerializeValue(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }

            if (value is IList<object> list)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    SerializeValue(sb, list[i]);
                }
                sb.Append(']');
                return;
            }

            if (value is double d)
            {
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is float f)
            {
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is int i32)
            {
                sb.Append(i32.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is long i64)
            {
                sb.Append(i64.ToString(CultureInfo.InvariantCulture));
                return;
            }

            sb.Append(value.ToString());
        }

        static void SerializeString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        #endregion

        #region Deserialize

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            return Deserialize(json) as Dictionary<string, object>;
        }

        static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            switch (c)
            {
                case '{': return ParseObject(json, ref index);
                case '[': return ParseArray(json, ref index);
                case '"': return ParseString(json, ref index);
                case 't':
                case 'f': return ParseBool(json, ref index);
                case 'n': return ParseNull(json, ref index);
                default: return ParseNumber(json, ref index);
            }
        }

        static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            index++; // skip '{'
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                return dict;
            }

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':') index++;
                SkipWhitespace(json, ref index);
                object val = ParseValue(json, ref index);
                dict[key] = val;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',')
                {
                    index++;
                    continue;
                }
                break;
            }

            if (index < json.Length && json[index] == '}') index++;
            return dict;
        }

        static List<object> ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            index++; // skip '['
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']')
            {
                index++;
                return list;
            }

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                list.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',')
                {
                    index++;
                    continue;
                }
                break;
            }

            if (index < json.Length && json[index] == ']') index++;
            return list;
        }

        static string ParseString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"') return null;
            index++; // skip opening '"'

            var sb = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"') return sb.ToString();
                if (c == '\\' && index < json.Length)
                {
                    char esc = json[index++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 <= json.Length)
                            {
                                string hex = json.Substring(index, 4);
                                index += 4;
                                sb.Append((char)Convert.ToInt32(hex, 16));
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        static double ParseNumber(string json, ref int index)
        {
            int start = index;
            while (index < json.Length)
            {
                char c = json[index];
                if (c == ',' || c == '}' || c == ']' || char.IsWhiteSpace(c)) break;
                index++;
            }
            string numStr = json.Substring(start, index - start);
            double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double result);
            return result;
        }

        static bool ParseBool(string json, ref int index)
        {
            if (index + 4 <= json.Length && json[index] == 't' && json[index + 1] == 'r' && json[index + 2] == 'u' && json[index + 3] == 'e')
            {
                index += 4;
                return true;
            }
            if (index + 5 <= json.Length && json[index] == 'f' && json[index + 1] == 'a' && json[index + 2] == 'l' && json[index + 3] == 's' && json[index + 4] == 'e')
            {
                index += 5;
                return false;
            }
            throw new FormatException($"Invalid boolean at position {index}");
        }

        static object ParseNull(string json, ref int index)
        {
            if (index + 4 <= json.Length && json[index] == 'n' && json[index + 1] == 'u' && json[index + 2] == 'l' && json[index + 3] == 'l')
            {
                index += 4;
                return null;
            }
            throw new FormatException($"Invalid null at position {index}");
        }

        static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }

        #endregion

        #region Helper — 链式取值

        public static object GetPath(object root, params string[] keys)
        {
            object current = root;
            foreach (string key in keys)
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(key, out current)) return null;
                }
                else if (current is List<object> list)
                {
                    if (int.TryParse(key, out int idx) && idx >= 0 && idx < list.Count)
                        current = list[idx];
                    else
                        return null;
                }
                else
                {
                    return null;
                }
            }
            return current;
        }

        public static string GetString(object root, params string[] keys)
        {
            return GetPath(root, keys) as string;
        }

        public static double GetDouble(object root, params string[] keys)
        {
            object val = GetPath(root, keys);
            if (val is double d) return d;
            return 0;
        }

        public static List<object> GetArray(object root, params string[] keys)
        {
            return GetPath(root, keys) as List<object>;
        }

        #endregion

        #region Builder helpers

        public static Dictionary<string, object> Obj(params (string key, object value)[] pairs)
        {
            var dict = new Dictionary<string, object>();
            foreach (var (key, value) in pairs)
                dict[key] = value;
            return dict;
        }

        public static List<object> Arr(params object[] items)
        {
            return new List<object>(items);
        }

        #endregion
    }
}
