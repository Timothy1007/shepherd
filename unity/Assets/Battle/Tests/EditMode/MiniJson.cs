using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shepherd.Battle.Tests
{
    /// <summary>
    /// 測試專用的最小 JSON 解析器（不依賴 UnityEngine 或額外套件）。
    /// 物件 → Dictionary&lt;string, object&gt;、陣列 → List&lt;object&gt;、數字 → double。
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException("json");
            var p = new Parser(json);
            p.SkipWs();
            var value = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw p.Error("JSON 結尾有多餘內容");
            return value;
        }

        private sealed class Parser
        {
            private readonly string s;
            private int i;

            public Parser(string s)
            {
                this.s = s;
                if (s.Length > 0 && s[0] == '﻿') i = 1;
            }

            public bool End { get { return i >= s.Length; } }

            public FormatException Error(string msg)
            {
                return new FormatException(msg + "（位置 " + i + "）");
            }

            public void SkipWs()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public object ReadValue()
            {
                if (End) throw Error("JSON 提前結束");
                char c = s[i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                throw Error("無法辨識的字元 '" + c + "'");
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error("預期 " + word);
                i += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++;
                SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (End || s[i] != '"') throw Error("物件鍵必須是字串");
                    string key = ReadString();
                    SkipWs();
                    if (End || s[i] != ':') throw Error("缺少 ':'");
                    i++;
                    SkipWs();
                    if (d.ContainsKey(key)) throw Error("重複的鍵 " + key);
                    d[key] = ReadValue();
                    SkipWs();
                    if (End) throw Error("物件未結束");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Error("物件中預期 ',' 或 '}'");
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                i++;
                SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return list; }
                while (true)
                {
                    SkipWs();
                    list.Add(ReadValue());
                    SkipWs();
                    if (End) throw Error("陣列未結束");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw Error("陣列中預期 ',' 或 ']'");
                }
            }

            private string ReadString()
            {
                i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (End) throw Error("字串未結束");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("跳脫字元未結束");
                    char e = s[i++];
                    switch (e)
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
                            if (i + 4 > s.Length) throw Error("\\u 跳脫不完整");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw Error("未知的跳脫字元 \\" + e);
                    }
                }
            }

            private double ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && "0123456789+-.eE".IndexOf(s[i]) >= 0) i++;
                double v;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    throw Error("數字格式錯誤");
                return v;
            }
        }
    }
}
