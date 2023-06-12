using System;
using System.Threading.Tasks;
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
        public string AccountType { set; get; }
        public bool IsPremium { set; get; }
        public bool IsPeakTime { set; get; }
        public bool IsEconomy { set; get; }
        public bool IsWatchVideo { set; get; }


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
        public bool GetData(JObject datajson)
        {
            if (datajson["viewer"] != null)
            {
                if ((bool)datajson["viewer"]["isPremium"])
                    this.IsPremium = true;
            }

            if (datajson["system"] != null)
            {
                if ((bool)datajson["system"]["isPeakTime"])
                    this.IsPeakTime = true;
            }

            if (datajson["media"]["delivery"] != null)
            {
                if (!datajson["media"]["delivery"].HasValues)
                    this.IsWatchVideo = false;
            }

            this.IsEconomy = this.IsPeakTime;
            if (this.IsPeakTime && !this.IsPremium)
                if (IsWatchVideo)
                    if (datajson["media"]["delivery"]["movie"] != null)
                    {
                        if ((bool)(datajson["media"]["delivery"]["movie"]["audios"][0]["isAvailable"]) &&
                            (bool)(datajson["media"]["delivery"]["movie"]["videos"][0]["isAvailable"]))
                            this.IsEconomy = false;
                    }

            if (datajson["video"] != null)
            {
                this.Title = (string)datajson["video"]["title"];
            }

            return true;
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
