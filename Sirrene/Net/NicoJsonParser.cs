using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using Sirrene.Prop;

namespace Sirrene.Net
{
    public class NicoJsonParser
    {
        private const string ENCODING = "UTF-8";
        //private readonly Logger log;
        private int chatCount;
        private Form1 _form = null;

        public NicoJsonParser(Form1 fo)
        {
            //log = logger;
            this._form = fo;
        }

        public int GetChatCount()
        {
            return chatCount;
        }

        public bool NvCommentJson2xml(FileInfo json, FileInfo xml, string kind, bool append)
        {
            return NvCommentJson2xml(json, xml, kind, append, false);
        }

        private bool NvCommentJson2xml(FileInfo json, FileInfo xml, string kind, bool append, bool localconv)
        {
            try
            {
                var jsonData = File.ReadAllText(json.FullName);
                var mson = JObject.Parse(jsonData);
                var xmlContent = MakeNvCommentXml(mson, kind, localconv);

                if (xmlContent != null)
                {
                    using (var sw = new StreamWriter(xml.FullName, append, Encoding.UTF8))
                    {
                        sw.Write(xmlContent);
                    }
                    return true;
                }
                return false;
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(NvCommentJson2xml), Ex);
                //err = Ex.Message;
                return false;
            }
        }

        private string MakeNvCommentXml(JObject json, string kind, bool localconv)
        {
            var sb = new StringBuilder();
            var pw = new StringWriter(sb);
            chatCount = 0;

            // ヘッダ
            pw.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            // パケット開始
            pw.WriteLine("<packet>");

            var m_status = json["meta"]["status"];
            if (m_status == null)
            {
                _form.AddLog("JSON status: null", 9);
                return null;
            }
            else if (!m_status.ToString().Equals("200"))
            {
                _form.AddLog("JSON status: "+m_status.ToString(), 9);
                return null;
            }

            bool outflag = true;
            int p = 0;
            var m_threads = json["data"]["threads"] as JArray;
            var m_global = json["data"]["globalComments"] as JArray;

            int ctype = 0;
            if (m_threads.Count > 3)
            {
                if (m_threads[2]["fork"].ToString().Equals("main"))
                    ctype = 1;
                else if (m_threads[2]["fork"].ToString().Equals("easy"))
                    ctype = 2;
            }

            if (kind.Equals("owner"))
            {
                p = 0;
                //if (outflag) log.Println($"{kind}_comment p={p}");

            }
            else if (kind.Equals("user"))
            {
                p = 1;
                if (ctype == 1)
                    p = 2;
                //if (outflag) log.Println($"{kind}_comment p={p}");
            }
            else if (kind.Equals("easy"))
            {
                p = 2;
                if (ctype == 1)
                    p = 3;
                //if (outflag) log.Println($"{kind}_comment p={p}");
            }
            else if (kind.Equals("optional"))
            {
                p = 1;
                //if (outflag) log.Println($"{kind}_comment p={p}");
            }
            else if (kind.Equals("nicos"))
            {
                p = 3;
                //if (outflag) log.Println($"{kind}_comment p={p}");
            }
            else
            {
                p = 1;
                if (ctype == 1)
                    p = 2;
                //if (outflag) log.Println($"{kind}_comment p={p}");
            }

            var m_data = m_threads[p] as JObject;
            int num_res = 0;
            try
            {
                for (int i = 0; i < m_threads.Count; i++)
                {
                    num_res += int.Parse(m_threads[i]["commentCount"].ToString());
                }
            }
            catch (FormatException Ex)
            {
                _form.AddLog($"commentCout Error: {Ex.ToString()}", 9);
            }

            if (outflag)
            {
                _form.AddLog($"thread_id: {m_data["id"]}", 9);
                _form.AddLog($"fork: {m_data["fork"]}", 9);
                _form.AddLog($"commentCount: {m_data["commentCount"]}", 9);
            }

            string s;
            if (outflag)
            {
                s = $"<thread thread=\"{m_data["id"]}\" />";
                pw.WriteLine(s);
                s = $"<global_num_res thread=\"{m_global[0]["id"]}\" num_res=\"{num_res}\"/>";
                pw.WriteLine(s);
                s = $"<leaf thread=\"{m_data["id"]}\" count=\"{m_data["commentCount"]}\"/>";
                pw.WriteLine(s);
            }

            var m_comments = m_data["comments"] as JArray;
            if (outflag)
            {
                _form.AddLog($"comments: {m_comments.Count}", 9);
            }

            foreach (var elem in m_comments)
            {
                s = $"<chat thread=\"{m_data["id"]}\"";
                foreach (var e in elem.Children<JProperty>())
                {
                    string key = e.Name;
                    JToken value = e.Value;

                    if (key.Equals("no"))
                    {
                        s += $" no=\"{value}\"";
                    }
                    else if (key.Equals("vposMs"))
                    {
                        string vpos = value.ToString();
                        if (vpos.Length > 1)
                            vpos = vpos.Substring(0, vpos.Length - 1);
                        else
                            vpos = "0";
                        s += $" vpos=\"{vpos}\"";
                    }
                    else if (key.Equals("postedAt"))
                    {
                        s += $" date=\"{GetIso8601Date2UnixTime(value.ToString())}\" date_usec=\"0\"";
                    }
                    else if (key.Equals("nicoruCount"))
                    {
                        if (!value.ToString().Equals("0"))
                            s += $" nicoru=\"{value}\"";
                    }
                    else if (key.Equals("userId"))
                    {
                        if (value.ToString().StartsWith("nvc:"))
                            s += " anonymity=\"1\"";
                        s += $" user_id=\"{value}\"";
                    }
                    else if (key.Equals("isPremium"))
                    {
                        if (value.ToObject<bool>())
                            s += " premium=\"1\"";
                    }
                    else if (key.Equals("commands"))
                    {
                        var commands = value as JArray;
                        if (commands.Count > 0)
                        {
                            s += " mail=\"";
                            for (int j = 0; j < commands.Count; j++)
                            {
                                s += commands[j].ToString();
                                if (j < commands.Count - 1)
                                    s += " ";
                            }
                            s += "\"";
                        }
                    }
                    else if (key.Equals("score"))
                    {
                        s += $" score=\"{value}\"";
                    }
                    else if (key.Equals("source"))
                    {
                        s += $" source=\"{value}\"";
                    }
                }
                s += ">" + XmlContents((JObject)elem, "body") + "</chat>";
                if (outflag)
                {
                    pw.WriteLine(s);
                    chatCount++;
                }
            }

            pw.WriteLine("</packet>");
            pw.Close();
            _form.AddLog($"{kind} comments: {chatCount}", 1);

            return sb.ToString();
        }

        private static string XmlContents(JObject jo, string key)
        {
            // return empty string when value is null or empty
            string val = jo[key]?.ToString();
            if (string.IsNullOrEmpty(val))
            {
                val = "";
            }
            else
            {
                val = SafeReference(val);
                val = EvalUnicodeDescr(val)
                    .Replace("\\n", "\n")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }
            return val;
        }

        private static string EvalUnicodeDescr(string val)
        {
            var p = new Regex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Singleline);
            var m = p.Matches(val);
            var sb = new StringBuilder();
            int lastIndex = 0;
            foreach (Match match in m)
            {
                sb.Append(val.Substring(lastIndex, match.Index - lastIndex));
                sb.Append(UnicodeReplace(match.Groups[1].Value));
                lastIndex = match.Index + match.Length;
            }
            sb.Append(val.Substring(lastIndex));
            return sb.ToString();
        }

        private static string UnicodeReplace(string xDigits)
        {
            try
            {
                int codepoint = Convert.ToInt32(xDigits, 16);
                if (codepoint < 0x20 && codepoint != 0x09 && codepoint != 0x0a && codepoint != 0x0d)
                {
                    //Logger.MainLog.Println($"warning: illegal unicode description found: u+{codepoint}");
                    //Logger.MainLog.Println("change to space u+0020");
                    codepoint = 0x20;
                }
                char c = (char)codepoint;
                return c.ToString();
            }
            catch (FormatException e)
            {
                //Logger.MainLog.PrintStackTrace(e);
                return '\u200C'.ToString();
            }
        }

        private static long GetIso8601Date2UnixTime(string iso8601DateString)
        {
            long unixTimeSeconds = -1L;
            // DateTimeOffsetを使って日付文字列を解析
            DateTimeOffset dateTimeOffset;
            if (DateTimeOffset.TryParse(iso8601DateString, out dateTimeOffset))
            {
                // Unixエポック (1970-01-01 00:00:00 UTC) からの経過秒数を計算
                unixTimeSeconds = dateTimeOffset.ToUnixTimeSeconds();
            }
            return unixTimeSeconds;
        }

        public static String SafeReference(String str)
        {
            if (string.IsNullOrEmpty(str))
                return null;
            str = str.Replace("&", "&amp;");
            str = str.Replace("<", "&lt;");
            str = str.Replace(">", "&gt;");
            //str = str.Replace("\"", "&quot;");
            return str;
        }
    }

}
