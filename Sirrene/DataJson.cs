using System;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Sirrene.Prop;
using Sirrene.Net;
using Sirrene.Proc;
using Sirrene.Rec;

namespace Sirrene
{
    public class DataJson
    {
        public string Status { set; get; }
        public string Error { set; get; }

        public string VideoId { set; get; }
        public string Title { set; get; }
        public string Description { set; get; }
        public string Provider_Type { set; get; }
        public string Provider_Name { set; get; }
        public string Provider_Id { set; get; }
        public string Community_Title { set; get; }
        public string Community_Id { set; get; }
        public string Community_Thumbnail { set; get; }
        public string Genre { set; get; }
        public List<string> TagList { set; get; }
        public string User_Id { set; get; }
        public bool IsPremium { set; get; }
        public bool IsPeakTime { set; get; }
        public bool IsEconomy { set; get; }
        public bool IsEncrypt { set; get; }
        public bool IsWatchVideo { set; get; }
        public string Session_Uri { set; get; }
        public string Content_Uri { set; get; }
        public string Heartbeat_Uri { set; get; }
        public string Heartbeat_Data { set; get; }

        public DataJson(string videoid)
        {
            this.VideoId = videoid;
            this.Status = null;
            this.Error = null;

            this.IsPremium = false;
            this.IsPeakTime = false;
            this.IsEconomy = false;
            this.IsEncrypt = false;
            this.IsWatchVideo = true;
            this.Title = "";
            this.Description = "";
            this.Genre = "";
            this.TagList = new List<string>();
            this.Session_Uri = null;
            this.Content_Uri = null;
            this.Heartbeat_Uri = null;

        }
        public (bool result, string err) GetDataJson(JObject datajson)
        {
            var result = false;
            var err = "";
            JToken delivery = null;

            try
            {
                if (datajson["viewer"] != null)
                {
                    if ((bool)datajson["viewer"]["isPremium"])
                        this.IsPremium = true;
                }
                else
                {
                    err = "JSON data viewer not found.";
                    return (result, err);
                }

                if (datajson["system"] != null)
                {
                    if ((bool)datajson["system"]["isPeakTime"])
                        this.IsPeakTime = true;
                }
                else
                {
                    err = "JSON data system not found.";
                    return (result, err);
                }

                if (datajson["media"]["delivery"] != null)
                {
                    if (datajson["media"]["delivery"].HasValues)
                        delivery = datajson["media"]["delivery"];
                    else
                        this.IsWatchVideo = false;
                }
                else
                {
                    err = "JSON data media delivery not found.";
                    return (result, err);
                }

                this.IsEconomy = this.IsPeakTime;
                if (this.IsPeakTime)
                    if (this.IsPremium)
                    {
                        this.IsEconomy = false;
                    }
                    else
                    {
                        if (IsWatchVideo)
                            if (delivery["movie"] != null)
                            {
                                if ((bool)(delivery["movie"]["audios"][0]["isAvailable"]) &&
                                    (bool)(delivery["movie"]["videos"][0]["isAvailable"]))
                                    this.IsEconomy = false;
                            }
                    }

                if (IsWatchVideo)
                    if (delivery["encryption"] != null &&
                        delivery["encryption"].HasValues)
                    {
                        this.IsEncrypt = true;
                        this.IsWatchVideo = false;
                    }

                if (datajson["video"] != null)
                {
                    this.Title = (string)datajson["video"]["title"];
                    if (!string.IsNullOrEmpty(this.Title))
                        this.Title = WebUtility.HtmlDecode(this.Title);
                    this.Description = (string)datajson["video"]["description"];
                    if (!string.IsNullOrEmpty(this.Description))
                        this.Description = WebUtility.HtmlDecode(this.Description);
                }
                else
                {
                    err = "JSON data video not found.";
                    return (result, err);
                }

                this.Genre = (string)(datajson["genre"]["label"]);
                if (!string.IsNullOrEmpty(this.Genre))
                    this.Genre = WebUtility.HtmlDecode(this.Genre);
                var items = (JArray)datajson["tag"]["items"];
                string ddd;
                foreach (var tag in items)
                {
                    ddd = (string)tag["name"];
                    if (!string.IsNullOrEmpty(ddd))
                    {
                        ddd = WebUtility.HtmlDecode(ddd);
                        TagList.Add(ddd);
                    }
                }

                result = true;
                return (result, err);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetDataJson), Ex);
                err = Ex.Message;
                return (result, err);
            }
        }

        public (string result, string err) MakeDmcSession(JObject datajson)
        {
            var result = "";
            var err = "";
            JToken session = null;
            StringBuilder sb = new StringBuilder();

            try
            {
                if (datajson["media"]["delivery"] != null)
                {
                    if (datajson["media"]["delivery"].HasValues)
                        session = datajson["media"]["delivery"]["movie"]["session"];
                }
                else
                {
                    err = "JSON data media delivery not found.";
                    return (result, err);
                }

                var recipe_id = (string)session["recipeId"];
                var player_id = (string)session["playerId"];
                var content_id = (string)session["contentId"];
                var lifetime = session["heartbeatLifetime"].ToString();
                var timeout = session["contentKeyTimeout"].ToString();
                var priority = session["priority"].ToString();
                var token = ((string)session["token"]).Replace("\"", "\\\"");
                var signature = (string)session["signature"];
                var user_id = (string)session["serviceUserId"];
                var videos = session["videos"].ToString();
                var audios = session["audios"].ToString();
                this.Session_Uri = session["urls"][0]["url"].ToString();

                sb.Append("{");
                sb.Append("  \"session\": {");
                sb.Append("    \"recipe_id\": \"" + recipe_id + "\",");
                sb.Append("    \"content_id\": \"" + content_id + "\",");
                sb.Append("    \"content_type\": \"movie\",");
                sb.Append("    \"content_src_id_sets\": [");
                sb.Append("      {");
                sb.Append("        \"content_src_ids\": [");
                sb.Append("          {");
                sb.Append("            \"src_id_to_mux\": {");
                sb.Append("              \"video_src_ids\": ");
                sb.Append("              " + videos);
                sb.Append("              ,");
                sb.Append("              \"audio_src_ids\": ");
                sb.Append("              " + audios);
                sb.Append("              ");
                sb.Append("            }");
                sb.Append("          }");
                sb.Append("        ]");
                sb.Append("      }");
                sb.Append("    ],");
                sb.Append("    \"timing_constraint\": \"unlimited\",");
                sb.Append("    \"keep_method\": {");
                sb.Append("      \"heartbeat\": {");
                sb.Append("        \"lifetime\": " + lifetime);
                sb.Append("      }");
                sb.Append("    },");
                sb.Append("    \"protocol\": {");
                sb.Append("      \"name\": \"http\",");
                sb.Append("      \"parameters\": {");
                sb.Append("        \"http_parameters\": {");
                sb.Append("          \"parameters\": {");
                sb.Append("            \"hls_parameters\": {");
                sb.Append("              \"use_well_known_port\": \"yes\",");
                sb.Append("              \"use_ssl\": \"yes\",");
                sb.Append("              \"transfer_preset\": \"\",");
                sb.Append("              \"segment_duration\": 6000");
                sb.Append("            }");
                sb.Append("          }");
                sb.Append("        }");
                sb.Append("      }");
                sb.Append("    },");
                sb.Append("    \"content_uri\": \"\",");
                sb.Append("    \"session_operation_auth\": {");
                sb.Append("      \"session_operation_auth_by_signature\": {");
                sb.Append("        \"token\": \"" + token + "\",");
                sb.Append("        \"signature\": \"" + signature + "\"");
                sb.Append("      }");
                sb.Append("    },");
                sb.Append("    \"content_auth\": {");
                sb.Append("      \"auth_type\": \"ht2\",");
                sb.Append("      \"content_key_timeout\": " + timeout + ",");
                sb.Append("      \"service_id\": \"nicovideo\",");
                sb.Append("      \"service_user_id\": \"" + user_id + "\"");
                sb.Append("    },");
                sb.Append("    \"client_info\": {");
                sb.Append("      \"player_id\": \"" + player_id + "\"");
                sb.Append("    },");
                sb.Append("    \"priority\": " + priority);
                sb.Append("  }");
                sb.Append("}");

                result = sb.ToString();
                var obj = JsonConvert.DeserializeObject(result);
                result = JsonConvert.SerializeObject(obj, Formatting.None);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(MakeDmcSession), Ex);
                err = Ex.Message;
                result = sb.ToString();
                return (result, err);
            }
            return (result, err);
        }

        public (bool result, string err) GetDmcContentUri(JObject session_json)
        {
            var result = false;
            var err = "";
            this.Content_Uri = null;

            try
            {
                if (session_json["data"] != null)
                {
                    this.Content_Uri = (string)session_json["data"]["session"]["content_uri"];
                    this.Heartbeat_Uri = this.Session_Uri + "/" +
                        (string)session_json["data"]["session"]["id"] +
                        "?_format=json&_method=PUT";
                    var obj = JsonConvert.DeserializeObject(session_json["data"].ToString());
                    this.Heartbeat_Data = JsonConvert.SerializeObject(obj, Formatting.None);

                    result = true;
                }
                else
                {
                    err = "content_uri not found.";
                }
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetDmcContentUri), Ex);
                err = Ex.Message;
                return (result, err);
            }
            return (result, err);
        }


        //指定フォーマットに基づいて録画サブディレクトリー名を作る
        public string SetRecFolderFormat(string s)
        {
            //return SetRecFileFormat(s, false);
            return s;
        }


        private string VideoTitle, nicoCategory, Tag, VideoID;
        private int numTag;
        private string[] nicoTagList;
        private string VideoBaseName;

        public string SetRecFileFormat(string s)
        {
            VideoTitle = SafeFileName(this.Title, false);
            Tag = this.VideoId;
            VideoID = "[" + this.VideoId + "]";
            VideoBaseName = VideoID + VideoTitle;
            nicoCategory = this.Genre;
            nicoTagList = this.TagList.ToArray();
            numTag = this.TagList.Count;
            //result = replaceFilenamePattern(file, true, false);
            return VideoBaseName;
        }

        /*
        %LOW% →economy時 low_
        %ID% →動画ID　%LOW%がなくeconomy時 動画IDlow_
        %id% →[動画ID]　%LOW%がなくeconomy時 [動画ID]low_
        %TITLE% →動画タイトル
        %title% →全角空白を半角空白に変えた動画タイトル
        %CAT% →(もしあれば)カテゴリータグ (属性 category="1" のタグ)(半角記号を全角化)
        %cat% →全角記号を削除した%CAT%
        %TAGn% →(n+1)番めのタグ (半角記号を全角化)
        %tagn% →全角記号を削除した%TAGn%
        */

        /** ConvertWorker.json 2766
         * replaceFilenamePattern(File source)
         * @param file
         * @return
         *  %ID% -> Tag, %id% -> [Tag](VideoIDと同じ) %TITLE% -> VideoTitle,
         *  %CAT% -> もしあればカテゴリータグ, %TAG1% ->２番めの動画タグ
         *  %TAGn% (n=2,3,...10) n+1番目のタグ
         */
        private FileInfo replaceFilenamePattern(FileInfo file, bool economy, bool dmc)
        {
            string videoFilename = file.FullName;
            if (VideoTitle == null)
            {
                string filename = file.Name;
                // filename = filename.Replace("%title%", "").Replace("%TITLE%", "");
                // Maybe bug, if contains
                SetVideoTitleIfNull(filename);
            }
            if (nicoCategory == null)
                nicoCategory = "";

            string canonical =
                VideoTitle.Replace("　", " ").Replace(" +", " ").Trim()
                .Replace("．", ".");
            string lowString = economy ? Props.LOW_PREFIX : "";
            string surfix = videoFilename.Contains("%LOW%") ? "" : lowString;
            videoFilename =
                videoFilename.Replace("%ID%", Tag + surfix)  // %ID% -> 動画ID
                .Replace("%id%", VideoID + surfix)   // %id% -> [動画ID]
                .Replace("%LOW%", lowString)    // %LOW% -> economy時 low_
                .Replace("%TITLE%", VideoTitle)    // %TITLE% -> 動画タイトル
                .Replace("%title%", canonical)    // %title% -> 動画タイトル（空白大文字を空白小文字に）
                .Replace("%CAT%", nicoCategory)        // %CAT% -> もしあればカテゴリータグ
                .Replace("%cat%", EraseMultiByteMark(nicoCategory));    // %cat% -> 全角記号削除

            for (int i = 1; i < numTag; i++)
            {
                string tag = nicoTagList[i];
                videoFilename = videoFilename.Replace("%TAG" + i + "%", tag)
                    .Replace("%tag" + i + "%", EraseMultiByteMark(tag));
            }

            FileInfo target = new FileInfo(videoFilename);
            DirectoryInfo parent = target.Directory;
            if (!parent.Exists)
            {
                parent.Create();
                // log.Println("folder created: " + parent.FullName);
                if (!parent.Exists)
                {
                    // log.Println("フォルダが作成できません:" + parent.FullName);
                    // log.Println("置換失敗 " + videoFilename);
                    target = file;
                }
            }
            return target;
        }

        // ConvertWorker.json 2818
        private string safeAsciiFileName(string str, bool is_unicode)
        {
            return ToSafeWindowsName(str, "SHIFT-JIS", is_unicode);
        }

        // ConvertWorker.json 5035
        private void SetVideoTitleIfNull(string path)
        {
            string videoTitle = VideoTitle;
            if (videoTitle == null)
            {
                videoTitle = GetTitleFromPath(path, VideoID, Tag);
                // 過去ログ時刻を削除

                string regex = "\\[" + Props.STR_FMT_REGEX + "\\]";
                videoTitle = Regex.Replace(videoTitle, regex, "");
                // int index = videoTitle.LastIndexOf("[");
                // 過去ログは[YYYY/MM/DD_HH:MM:SS]が最後に付く
                // if (index >= 0)
                // {
                //     videoTitle = videoTitle.Substring(0, index);
                // }
                //log.Println("Title<" + videoTitle + ">");
                VideoTitle = videoTitle;
                //SetVidTitile(tid, Tag, VideoTitle, Tag.Contains(LOW_PREFIX));
            }
        }

        /* ConvertWorker.json 5215
         * videoIDの位置は無関係に削除
         * 拡張子があればその前まで
        */
        private string GetTitleFromPath(string path, string videoID, string tag)
        {
            if (path.Contains(videoID))
            {
                path = path.Replace(videoID, ""); // Remove videoID regardless of its position
            }
            else if (path.Contains(tag))
            {
                path = path.Replace(tag, "");
                if (path.StartsWith("_"))
                {
                    path = path.Substring(1);
                }
            }
            // If there's an extension, truncate until just before it
            if (path.LastIndexOf(".") > path.LastIndexOf(Path.DirectorySeparatorChar))
            {
                path = path.Substring(0, path.LastIndexOf("."));
            }
            return path;
        }

        // ConvertWorker.json 2758
        private void SetVidTitle(int tid, string tag, string title, bool isEco)
        {
            string ecoPrefix = "";
            if (isEco || tag.Contains(Props.LOW_PREFIX))
            {
                ecoPrefix = Props.ECO_PREFIX;
            }
            //SendText("@vid" + " " + ecoPrefix + "(" + tid + ")" + tag + "_" + title);
        }

        // NicoClient.json
        private static Regex safeFileName_SPACE = new Regex(" {2}");
        public static string SafeFileName(string str, bool is_unicode)
        {
            string result;
            if (string.IsNullOrEmpty(str))
                return "";
            result = WebUtility.HtmlDecode(str);
            if (!string.IsNullOrEmpty(result))
            {
                // MS-DOSシステム(ffmpeg.exe)で扱える形に(UTF-8のまま)
                result = ToSafeWindowsName(result, "SHIFT-JIS", is_unicode);
            }
            return result;
        }

        private static string EraseMultiByteMark(string str)
        {
            str = Regex.Replace(str, "[／￥？＊：｜“＜＞．＆；]", "");
            if (string.IsNullOrEmpty(str))
                str = "null";
            return str;
        }

        private static string ToSafeWindowsName(string str, string encoding, bool is_unicode)
        {
            if (!is_unicode)
                str = ToSafeString(str, encoding);
            // ファイルシステムで扱える形に
            str = str.Replace('/', '／')
                     .Replace('\\', '￥')
                     .Replace('?', '？')
                     .Replace('*', '＊')
                     .Replace(':', '：')
                     .Replace('|', '｜')
                     .Replace('\"', '”')
                     .Replace('<', '＜')
                     .Replace('>', '＞')
                     .Replace('.', '．');

            str = safeFileName_SPACE.Replace(str, " ");
            str = str.Trim();
            return str;
        }

        private static string ToSafeString(string str, string encoding)
        {
            var sb = new StringBuilder(64);
            foreach (char c in str)
            {
                byte[] b = Encoding.GetEncoding(encoding).GetBytes(c.ToString());
                int len = b.Length;
                if (len == 1 && b[0] == '?')
                {
                    b[0] = (byte)'-';
                    sb.Append("-");
                }
                else
                {
                    sb.Append(c);
                }
            }
            string dest = sb.ToString();
            return dest;
        }

    }

}
