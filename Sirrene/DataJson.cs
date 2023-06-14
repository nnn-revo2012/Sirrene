using System;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Net;

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
        public string User_Id { set; get; }
        public bool IsPremium { set; get; }
        public bool IsPeakTime { set; get; }
        public bool IsEconomy { set; get; }
        public bool IsWatchVideo { set; get; }
        public string Session_Url { set; get; }
        public string Session_Data { set; get; }


        public DataJson(string videoid)
        {
            this.VideoId = videoid;
            this.Status = null;
            this.Error = null;

            this.IsPremium = false;
            this.IsPeakTime = false;
            this.IsEconomy = false;
            this.IsWatchVideo = true;
            this.Title = "";

        }
        public (bool result, string err) GetDataJson(JObject datajson)
        {
            var result = false;
            var err = "";

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
                    if (!datajson["media"]["delivery"].HasValues)
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
                            if (datajson["media"]["delivery"]["movie"] != null)
                            {
                                if ((bool)(datajson["media"]["delivery"]["movie"]["audios"][0]["isAvailable"]) &&
                                    (bool)(datajson["media"]["delivery"]["movie"]["videos"][0]["isAvailable"]))
                                    this.IsEconomy = false;
                            }
                    }

                if (datajson["video"] != null)
                {
                    this.Title = (string)datajson["video"]["title"];
                }
                else
                {
                    err = "JSON data video not found.";
                    return (result, err);
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

        public (string result, string err) MakeSession(JObject datajson)
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
                var token = ((string)session["token"]).Replace("\"", "\\\"");
                var signature = (string)session["signature"];
                var user_id = (string)session["serviceUserId"];
                var videos = session["videos"].ToString();
                var audios = session["audios"].ToString();

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
                sb.Append("        \"lifetime\": 120000");
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
                sb.Append("      \"content_key_timeout\": 600000,");
                sb.Append("      \"service_id\": \"nicovideo\",");
                sb.Append("      \"service_user_id\": \"" + user_id + "\"");
                sb.Append("    },");
                sb.Append("    \"client_info\": {");
                sb.Append("      \"player_id\": \"" + player_id + "\"");
                sb.Append("    },");
                //sb.Append("    \"priority\": 0.6");
                sb.Append("  }");
                sb.Append("}");

                result = sb.ToString();
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(MakeSession), Ex);
                err = Ex.Message;
                result = sb.ToString();
                return (result, err);
            }
            return (result, err);
        }

        //指定フォーマットに基づいて録画サブディレクトリー名を作る
        public string SetRecFolderFormat(string s)
        {
            return SetRecFileFormat(s);
        }

        //指定フォーマットに基づいて録画ファイル名を作る
        public string SetRecFileFormat(string s)
        {
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
            String result = s;
            try
            {
                if (result.Contains("%LOW%"))
                {
                    result = result.Replace("%LOW%", ReplaceWords("low_"));
                    result = result.Replace("%ID%", ReplaceWords(this.VideoId));
                    result = result.Replace("%id%", "[" + ReplaceWords(this.VideoId)) + "]";
                }
                else
                {
                    var low = "";
                    if (this.IsEconomy)
                        low = "low_";
                    result = result.Replace("%ID%", ReplaceWords(this.VideoId) + low);
                    result = result.Replace("%id%", "[" + ReplaceWords(this.VideoId) + "]" + low);
                }
                result = result.Replace("%TITLE%", ReplaceWords(this.Title));
                result = result.Replace("%title%", ReplaceWords(this.Title.Replace("　", " ")));
                //result = result.Replace("%CAT%", ReplaceWords(this.Provider_Id));
                //result = result.Replace("%cat%", ReplaceWords(this.Community_Title));
                //result = result.Replace("%TAGn%", ReplaceWords(this.Community_Id));
                //result = result.Replace("%tagn%", ReplaceWords(this.Title));
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(SetRecFileFormat), Ex);
                return result;
            }

            return result;
        }

        private string ReplaceWords(string s)
        {
            var result = s.Replace("\\", "￥");
            result = result.Replace("/", "?");
            result = result.Replace(":", "：");
            result = result.Replace("*", "＊");
            result = result.Replace("?", "？");
            result = result.Replace("\"", "”");
            result = result.Replace("<", "＜");
            result = result.Replace(">", "＞");
            result = result.Replace("|", "｜");

            result = result.Replace("）", ")");
            result = result.Replace("（", "(");

            //result = result.Replace("　", " ");
            //result = result.Replace("\u3000", " ");

            return result;
        }

    }
}
