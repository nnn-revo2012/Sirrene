﻿using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace Sirrene.Prop
{

    public enum IsLogin { always, none, };
    public enum LoginMethod { login, cookie, };
    public enum UseExternal { native, ext1, };

    public class Props
    {
        //定数設定
        public static readonly string UserAgent = "Mozilla/5.0 (" + Ver.GetAssemblyName() + "; " + Ver.Version + ")";
        //public static readonly string UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36";
        public static readonly string NicoDomain = "https://www.nicovideo.jp/";

        public static readonly string NicoVideoUrl = NicoDomain + "watch/";
        public static readonly string NicoOrigin  = "https://www.nicovideo.jp";
        public static readonly string NicoUserUrl = "https://www.nicovideo.jp/user/";

        public static readonly string NicoLoginUrl = "https://account.nicovideo.jp/login/redirector?show_button_twitter=1&site=niconico&show_button_facebook=1&next_url=%2F";

        public static readonly string DmsSessionUrl = "https://nvapi.nicovideo.jp/v1/watch/%%VIDEOID%%/access-rights/hls?actionTrackId=%%TRACKID%%";

        public static readonly IDictionary<string, string> Quality =
            new Dictionary<string, string>()
        {
            {"1080", "1080p"},
            {"720",  "720p"},
            {"540",  "540p"},
            {"480",  "480p"},
            {"360",  "360p"},
            {"144",  "144p(低画質)"},
        };

        public static readonly string STR_FMT_REGEX = "[0-9]{4}(／[0-3][0-9]){2}_[0-2][0-9](：[0-5][0-9]){2}";
        public static readonly string LOW_PREFIX = "low_"; // Define LOW_PREFIX and DMC_PREFIX if needed
        public static readonly string DMC_PREFIX = "dmc_";
        public static readonly string ECO_PREFIX = "eco_";
        public static readonly string OWNER_EXT = "[Owner].xml";    // 投稿者コメントサフィックス
        public static readonly string OPTIONAL_EXT = "{Optional}.xml";  // オプショナルスレッドサフィックス
        public static readonly string EASY_EXT = "{Easy}.xml";  // かんたんコメントサフィックス
        public static readonly string NICOS_EXT = "{Nicos}.xml";	//ニコスコメントサフィックス

        public bool IsDebug { get; set; }

        public IsLogin IsLogin { get; set; }
        public LoginMethod LoginMethod { get; set; }
        public string UserID { get; set; }
        public string Password { get; set; }
        public bool IsAllCookie { get; set; }
        public string SaveDir { get; set; }
        public string SaveFolder { get; set; }
        public string SaveFile { get; set; }
        public string QuarityType { get; set; }
        public UseExternal UseExternal { get; set; }
        public string[] ExecFile { get; set; }
        public string[] ExecCommand { get; set; }
        public string[] BreakCommand { get; set; }
        public bool IsLogging { get; set; }
        public bool IsComment { get; set; }
        public bool IsVideo { get; set; }
        public string SaveCommentDir { get; set; }
        public string SaveCommentFolder { get; set; }
        public string SaveCommentFile { get; set; }


        public Props()
        {
            ExecFile = new string[2];
            ExecCommand = new string[2];
            BreakCommand = new string[2];
        }
        public static int ParseUseExternal(string str)
        {
            return (int)(UseExternal)Enum.Parse(typeof(UseExternal), str);
        }

        public static bool IsUseExternal(string str)
        {
            if (string.IsNullOrEmpty(str))
                return false;
            else
                return Enum.IsDefined(typeof(UseExternal), str);
        }

        public bool LoadData(string accountdbfile)
        {
            try
            {
                using (var db = new Account(accountdbfile))
                {
                    var alias = "nico_01";
                    string user = ""; string pass = "";
                    db.ReadDbUser(alias, out user, out pass);
                    this.UserID = user;
                    this.Password = pass;
                }
                this.IsLogin =
                    (IsLogin)Enum.Parse(typeof(IsLogin), Properties.Settings.Default.IsLogin);
                this.LoginMethod =
                    (LoginMethod)Enum.Parse(typeof(LoginMethod), Properties.Settings.Default.LoginMethod);
                //this.SelectedCookie = Properties.Settings.Default.SelectedCookie;
                //this.IsAllCookie = Properties.Settings.Default.IsAllCookie;
                this.SaveDir = Properties.Settings.Default.SaveDir;
                this.SaveFolder = Properties.Settings.Default.SaveFolder;
                this.SaveFile = Properties.Settings.Default.SaveFile;
                this.UseExternal =
                    (UseExternal)Enum.Parse(typeof(UseExternal), Properties.Settings.Default.UseExternal);
                this.ExecFile = Properties.Settings.Default.ExecFile.Split(';');
                this.ExecCommand = Properties.Settings.Default.ExecCommand.Split(';');
                this.BreakCommand = Properties.Settings.Default.BreakCommand.Split(';');
                this.IsLogging = Properties.Settings.Default.IsLogging;
                this.IsComment = Properties.Settings.Default.IsComment;
                this.IsVideo = Properties.Settings.Default.IsVideo;
                this.SaveCommentDir = Properties.Settings.Default.SaveCommentDir;
                this.SaveCommentFolder = Properties.Settings.Default.SaveCommentFolder;
                this.SaveCommentFile = Properties.Settings.Default.SaveCommentFile;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(LoadData), Ex);
                return false;
            }
            return true;
        }

        public bool SaveData(string accountdbfile, bool acc_flg)
        {
            try
            {
                Properties.Settings.Default.IsLogin = this.IsLogin.ToString().ToLower();
                Properties.Settings.Default.LoginMethod = this.LoginMethod.ToString().ToLower();
                //Properties.Settings.Default.SelectedCookie = this.SelectedCookie;
                //Properties.Settings.Default.IsAllCookie = this.IsAllCookie;
                Properties.Settings.Default.SaveDir = this.SaveDir;
                Properties.Settings.Default.SaveFolder = this.SaveFolder;
                Properties.Settings.Default.SaveFile = this.SaveFile;
                Properties.Settings.Default.UseExternal = this.UseExternal.ToString().ToLower();
                Properties.Settings.Default.ExecFile = String.Join(";", this.ExecFile);
                Properties.Settings.Default.ExecCommand = String.Join(";", this.ExecCommand);
                Properties.Settings.Default.BreakCommand = String.Join(";", this.BreakCommand);
                Properties.Settings.Default.IsLogging = this.IsLogging;
                Properties.Settings.Default.IsComment = this.IsComment;
                Properties.Settings.Default.IsVideo = this.IsVideo;
                Properties.Settings.Default.SaveCommentDir = this.SaveCommentDir;
                Properties.Settings.Default.SaveCommentFolder = this.SaveCommentFolder;
                Properties.Settings.Default.SaveCommentFile = this.SaveCommentFile;
                Properties.Settings.Default.Save();
                if (acc_flg == true)
                {
                    using (var db = new Account(accountdbfile))
                    {
                        var alias = "nico_01";
                        db.CreateDbAccount();
                        db.WriteDbUser(alias, this.UserID, this.Password);
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(SaveData), Ex);
                return false;
            }
            return true;
        }

        public bool ReloadData(string accountdbfile)
        {
            Properties.Settings.Default.Reload();
            return this.LoadData(accountdbfile);
        }

        public bool ResetData(string accountdbfile)
        {
            var result = false;
            Properties.Settings.Default.Reset();
            result = this.LoadData(accountdbfile);
            this.UserID = "";
            this.Password = "";
            return result;
        }

        //設定ファイルの場所をGet
        public static string GetSettingDirectory()
        {
            //設定ファイルの場所
            var config = ConfigurationManager.OpenExeConfiguration(
                ConfigurationUserLevel.PerUserRoamingAndLocal);
            return Path.GetDirectoryName(config.FilePath);
        }

        //アプリケーションの場所をGet
        public static string GetApplicationDirectory()
        {
            //アプリケーションの場所
            var tmp = System.Reflection.Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(tmp);
        }

        //ログファイル名をGet
        public static string GetLogfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }

        //実行ログファイル名をGet
        public static string GetExecLogfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_exec_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }

        //dataJsonファイル名をGet
        public static string GetDataJsonfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_dataJson_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }
        public static string GetSessionfile(string dir, string filename)
        {
            var tmp = Path.GetFileNameWithoutExtension(filename) + "_session_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, tmp);
        }

        public static string GetDirSepString()
        {
            return Path.DirectorySeparatorChar.ToString();
        }

        public static string GetVideoUrl(string videoid)
        {
            return NicoVideoUrl + videoid;
        }

        public static string GetUserUrl(string userid)
        {
            return NicoUserUrl + userid;
        }

        private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public static long GetUnixTime(DateTime localtime)
        {
            DateTime utc = localtime.ToUniversalTime();
            return (long)(((TimeSpan)(utc - UNIX_EPOCH)).TotalSeconds);
        }

        public static DateTime GetUnixToDateTime(long unix)
        {
            return UNIX_EPOCH.AddSeconds(unix).ToLocalTime();
        }

        public static long GetLongParse(string ttt)
        {
            double dd = -1.0D;
            double.TryParse(ttt, out dd);
            return (long )dd;

        }
        //特殊文字をエンコードする
        public static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            s = s.Replace("&", "&amp;");
            s = s.Replace("<", "&lt;");
            s = s.Replace(">", "&gt;");
             s = s.Replace("\"", "&quot;");

            return s;
        }

    }
}
