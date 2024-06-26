﻿using System;
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
    public partial class Form1 : Form
    {
        public static Props props;                   //設定

        private static bool IsBatchMode { get; set; } //引数指定で実行か？
        //0処理待ち 1録画準備 2録画中 3再接続 4中断 5変換処理中 9終了
        private static int ProgramStatus { get; set; } //プログラム状態
        private volatile bool IsStart_flg = false;
        public volatile bool IsBreak_flg = false;

        //dispose するもの
        private ExecProcess _eProcess = null;         //Process
        private RecHtml _rHtml = null;                //RecHtml
        private NicoComment _nComment = null;         //NicoComment
        private NicoDb _ndb = null;                   //NicoDb

        private CookieContainer cookiecontainer = null;
        private NicoVideoNet nvn = null;
        private ExecPsInfo epi = null;                //実行／保存ファイル情報
        private CommentInfo cmi = null;                //コメント情報

        private string videoId = null;

        private string accountdbfile;
        private readonly object lockObject = new object();  //情報表示用
        private readonly object lockObject2 = new object(); //実行ファイルのログ用
        private string LogFile;
        private string LogFile2;
        private string LogFile3;
        private string LogFile4;

        public Form1(string[] args)
        {
            InitializeComponent();
            this.Text = Ver.GetFullVersion();
            IsBatchMode = (args.Length > 0) ? true : false;
            if (IsBatchMode)
            {
                videoId = NicoVideoNet.GetVideoID(args[0]);
                if (string.IsNullOrEmpty(videoId))
                {
                    this.Close();
                }
            }
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            //設定データー読み込み
            accountdbfile = Path.Combine(Props.GetSettingDirectory(), "account.db");
            props = new Props();
            props.LoadData(accountdbfile);
            ClearHosoData();

            if (IsBatchMode) button1.PerformClick();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                //中断処理
                if (button1.Text == "ABORT")
                {
                    IsBreak_flg = true;
                    return;
                }

                LogFile = null;
                LogFile2 = null;
                LogFile3 = null;
                LogFile4 = null;

                //ニコニコに接続
                ClearHosoData();
                ClearLog();

                //フォルダやファイルのチェック
                var save_dir = String.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                if (!Directory.Exists(save_dir))
                {
                    AddLog("保存フォルダーが存在しません。", 2);
                    return;
                }

                var exec_file = props.ExecFile[0];
                exec_file = GetExecFile(exec_file);
                if (props.UseExternal != UseExternal.native)
                    if (!File.Exists(exec_file))
                    {
                        AddLog("実行ファイルがありません。", 2);
                        return;
                    }

                //動画ID
                if (!IsBatchMode)
                    videoId = NicoVideoNet.GetVideoID(textBox1.Text);
                if (string.IsNullOrEmpty(videoId))
                {
                    AddLog("動画URLまたは動画IDを指定してください。", 2);
                    textBox1.Text = string.Empty;
                    return;
                }

                LogFile = Props.GetLogfile(save_dir, videoId);
                LogFile2 = Props.GetExecLogfile(save_dir, videoId);
                LogFile3 = Props.GetDataJsonfile(save_dir, videoId);
                LogFile4 = Props.GetSessionfile(save_dir, videoId);

                AddLog("ダウンロード開始します。", 1);
                AddLog(string.Format("VideoID: {0}", videoId), 1);
                textBox1.Text = NicoVideoNet.GetNicoPageUrl(videoId);

#if TEST01
                string alias = "nico_01";
                string user = "aaa@aaa.com"; string pass = "vvvvv";
                string session = ""; string secure = "";
                using (var ddd = new Prop.Account("D:\\home\\tmp\\account.db"))
                {
                    ddd.CreateDbAccount();
                    if (ddd.WriteDbUser(alias, props.UserID, props.Password))
                        AddLog("メールID書き込みOK", 1);
                    if (ddd.ReadDbUser(alias, out user, out pass))
                        AddLog("user: " + user + " pass: " + pass, 1);
                    //if (ddd.WriteDbSession(alias, "ffffffffffff", "nnnnnnnnnnnn"))
                    //    AddLog("session書き込みOK", 1);
                    //if (ddd.ReadDbSession(alias, out session, out secure))
                    //    AddLog("session: " + session + " secure: " + secure, 1);
                }
                return;
#endif
                //録画開始
                Task.Run(() => Start_DL());

            }
            catch (Exception Ex)
            {
                AddLog(nameof(Button1_Click) + "() Error: \r\n" + Ex.Message, 2);
            }
        }
        public async void Start_DL()
        {
            cookiecontainer = new CookieContainer();

            JObject dataJson = null;              //動画情報(JObject)
            JObject sessionJson = null;           //セッション情報(JObject)
            RetryInfo rti = null;                 //リトライ情報

            nvn = new NicoVideoNet();
            try
            {
                if (props.IsLogin == IsLogin.always)
                {
                    bool flag = false;
                    //ニコニコにログイン
                    switch (props.LoginMethod.ToString())
                    {
                        case "login":
                            using (var db = new Prop.Account(accountdbfile))
                            {
                                var alias = "nico_01";
                                string user = null; string pass = null;
                                if (!nvn.IsLoginStatus)
                                {
                                    if (db.GetSession(alias, cookiecontainer))
                                    {
                                        //ニコニコにアクセスする
                                        (flag, _, _) = await nvn.IsLoginNicoAsync(cookiecontainer);
                                        if (flag)
                                        {
                                            //ログインしていればOK
                                            AddLog("Already logged in", 1);
                                            break;
                                        }
                                    }
                                    //ログイン処理
                                    AddLog("ログイン開始", 1);
                                    if (!db.ReadDbUser(alias, out user, out pass))
                                    {
                                        AddLog("Login Failed: can't read user or pass", 1);
                                        return;
                                    }
                                    (flag, _, _) = await nvn.LoginNico(cookiecontainer, props.UserID, props.Password);
                                    if (!flag)
                                    {
                                        AddLog("Login Failed: login error", 1);
                                        return;
                                    }
                                    else
                                    {
                                        AddLog("Login OK", 1);
                                        db.SetSession(alias, cookiecontainer);
                                    }
                                }
                                else
                                {
                                    AddLog("Already logged in", 1);
                                }
                            }
                            break;
                        case "cookie":
/*
                            //ブラウザのCookie読み込み処理
                            if (props.SelectedCookie != null)
                                AddLog(string.Format("Cookie: {0} {1}", props.SelectedCookie.BrowserName, props.SelectedCookie.ProfileName), 1);
                            if (!(await _nvn.SetNicoCookie(props.SelectedCookie)))
                            {
                                AddLog("Cookie読み込み失敗", 1);
                                return;
                            }
                            AddLog("Cookie読み込みOK", 1);
                            if (!await _nvn.IsLoginNicoAsync())
                            {
                                AddLog("ブラウザでログインし直してください", 1);
                                return;
                            }
                            Cookie = _nvn.GetCookieContainer();
 */
                            break;
                    } //switch
                }
                else
                {
                    AddLog("ログインなし", 1);
                }

                //動画情報を取得する
                string err;
                int neterr;
                (dataJson, err, neterr) = await nvn.GetNicoPageAsync(cookiecontainer, videoId);
                if (!string.IsNullOrEmpty(err))
                {
                    AddLog("この動画は存在しないか、削除された可能性があります。 (" + err + ")", 1);
                    End_DL(0); 
                    return;
                }
                AddDataJson(dataJson.ToString());
                var djs = new DataJson(videoId);
                bool flg;
                (flg, err) = djs.GetDataJson(dataJson);
                if (!flg)
                {
                    AddLog("Error: GetDataJson. (" + err + ")", 1);
                    return;
                }
                if (djs.IsPremium)
                    AddLog("Premium Account", 1);
                else
                    AddLog("Normal Account", 1);
                if (djs.IsPeakTime)
                    AddLog("PeakTime(Economy Time)", 1);
                else
                    AddLog("No PeakTime(Not Econmy Time)", 1);
                AddLog("新サーバー(DMS)を使用します。", 1);
                if (!djs.IsWatchVideo)
                    AddLog("動画がダウンロードできません。", 1);

                //保存ファイル名作成
                epi = new ExecPsInfo();
                epi.Sdir = string.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                epi.Exec = GetExecFile(props.ExecFile[0]);
                epi.Arg = props.ExecCommand[0];
                epi.Sfile = djs.SetRecFileFormat(props.SaveFile);
                //epi.Sfolder = djs.SetRecFolderFormat(props.SaveFolder);
                epi.Protocol = "hls";
                epi.Seq = 0;
                //ExecPsInfo.MakeRecDir(epi);
                AddLog("Genre: " + djs.Genre, 1);
                AddLog("TAG(" + djs.TagList.Count + ")" , 1);
                epi.SaveFile = ExecPsInfo.GetSaveFileSqlite3(epi) + epi.Ext;

                AddLog("VideoFile: "+epi.SaveFile, 9);
                EnableButton(false);
                IsStart_flg = true;
                IsBreak_flg = false;

                //コメント情報
                if (props.IsComment)
                {
                    AddLog("コメントをダウンロードします。", 1);
                    cmi = new CommentInfo();
                    cmi.Sdir = string.IsNullOrEmpty(props.SaveCommentDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveCommentDir;
                    cmi.Sfile = djs.SetRecFileFormat(props.SaveCommentFile);
                    cmi.SaveFile = ExecPsInfo.GetSaveFileSqlite3(epi);
                    AddLog("CommentFile: " + epi.SaveFile, 9);
                    //if (bci.IsTimeShift())
                    //    cctl = new CommentControl();
                    //else
                    //    cctl = null;
                    _nComment = new NicoComment(this, djs, cmi, nvn);
                    bool result = false;
                    (result, err, neterr) = await _nComment.GetCommentAsync(cookiecontainer);
                    if (!result || !string.IsNullOrEmpty(err))
                    {
                        AddLog("コメントがダウンロードできませんでした。", 1);
                    }
                    else
                    {
                        AddLog("コメントをダウンロードしました。", 1);
                    }
                }

                if (!IsStart_flg || IsBreak_flg)
                {
                    End_DL(1);
                    return;
                }

                if (!props.IsVideo || !djs.IsWatchVideo)
                {
                    End_DL(0);
                    return;
                }

                // Session作成
                String session = "";
                (session, err) = djs.MakeDmsSession(dataJson);
                if (!string.IsNullOrEmpty(session))
                {
                    if (!string.IsNullOrEmpty(err))
                    {
                        AddLog("MakeDmsSession Error: " + err, 1);
                        return;
                    }
                    AddSession(JObject.Parse(session).ToString());
                }
                // SessionをapiにPOST
                AddLog("Send PostDmsSession", 1);
                (sessionJson, err, neterr) = await nvn.PostNicoDmsSessionAsync(cookiecontainer, djs.Session_Uri, session, djs.AccessRightKey);
                if (!string.IsNullOrEmpty(err))
                {
                    AddLog("Send PostDmsSession Error: " + err + "(" + neterr + ")", 1);
                }
                else
                {
                    if (sessionJson["meta"] != null)
                    {
                        var msg = (string)sessionJson["meta"]["message"] +
                            "(" + sessionJson["meta"]["status"].ToString() + ")";
                        AddLog("Send PostDmsSession " + msg, 9);
                    }
                    AddSession("\r\nResponse:\r\n" + sessionJson.ToString());
                    (flg, err) = djs.GetDmsContentUri(sessionJson);
                    if (flg)
                    {
                        AddLog("Content_Uri: " + djs.Content_Uri, 9);
                    }
                    else
                    {
                        AddLog("Content_Uri Error: " + err, 1);
                    }
                }

                var ri = new RetryInfo();
                rti = ri;
                rti.Count = 3;

                //DEBUG
                AddLog("Get NicoMasterDms", 1);
                AddSession("\r\nMaster.m3u8:\r\n");
                string data;
                (data, err, neterr) = await nvn.GetNicoMasterDmsAsync(cookiecontainer, djs.Content_Uri);
                if (!string.IsNullOrEmpty(err))
                {
                    AddLog("GetNicoMasterDmsAsync Error: " + err + "(" + neterr + ")", 1);
                }
                else
                {
                    AddLog("GetNicoMasterDmsAsync", 1);
                }
                AddSession(data);

                End_DL(0);

/*
                //動画ダウンロード
                if (props.UseExternal == UseExternal.native)
                {
                    _rHtml = new RecHtml(this, djs, cookiecontainer, _ndb, rti);
                    _rHtml.ExecPs(djs.Content_Uri, epi.SaveFile);
                }
                else
                {
                    _eProcess = new ExecProcess(this, djs, rti);
                    var argument = ExecPsInfo.SetOption(epi, djs.Content_Uri, 0);
                    //_eProcess.ExecPs(_epi.Exec, argument);
                    _eProcess.ExecPs(epi.Exec, argument);
                }

                //5秒おきに状態を調べて処理する
                JObject dummy = null;
                int interval = 0;
                while (IsStart_flg == true && IsBreak_flg == false)
                {
                    await Task.Delay(5000);
                    if (_rHtml != null && _rHtml.PsStatus > 0 ||
                        _eProcess != null && _eProcess.PsStatus > 0)
                    {
                        IsStart_flg = false;
                        break;
                    }
                    interval += 5;
                    if (interval < 40)
                        continue;

                    //ハートビート
                    interval = 0;
                    (dummy, err, neterr) = await nvn.PostNicoDmcSessionAsync(cookiecontainer, djs.Heartbeat_Uri, djs.Heartbeat_Data);
                    if (!string.IsNullOrEmpty(err))
                    {
                        AddLog("Send Heartbeat Error: " + err + "(" + neterr + ")", 1);
                    }
                    else
                    {
                        if (dummy["meta"] != null)
                        {
                            var msg = (string)dummy["meta"]["message"] +
                                "(" + dummy["meta"]["status"].ToString() + ")";
                            AddLog("Send Heartbeat " + msg, 9);
                        }
                    }
                }

                //sqlite3 -> .mp4 に変換
                if (IsBreak_flg)
                {
                    End_DL(1);
                }
                else
                {
                    End_DL(0);
                    await Task.Run(() => StartExtract(epi.SaveFile));
                }
                */

                return;
            } // try
            catch (Exception Ex)
            {
                AddLog(nameof(Start_DL) + "() Error: \r\n" + Ex.Message, 2);
            }
        }

        public void End_DL(int flag)
        {
            try
            {
                if (_rHtml != null)
                {
                    _rHtml.BreakProcess("");
                }
                if (_nComment != null)
                {
                    _nComment.Dispose();
                }
                if (_eProcess != null)
                {
                    _eProcess.BreakProcess(epi.BreakKey);
                }
                if (_ndb != null)
                {
                    _ndb.Dispose();
                }
                if (flag == 1)
                    AddLog("ダウンロード中断しました。", 1);
                else
                    AddLog("ダウンロード終了しました。", 1);
                EnableButton(true);
                IsStart_flg = false;
                IsBreak_flg = false;

                return;
            } // try
            catch (Exception Ex)
            {
                AddLog(nameof(End_DL) + "() Error: \r\n" + Ex.Message, 2);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_rHtml != null)
            {
                _rHtml.Dispose();
                _rHtml = null;
            }
            if (_nComment != null)
            {
                _nComment.Dispose();
            }
            if (_eProcess != null)
            {
                _eProcess.Dispose();
                _eProcess = null;
            }
            _ndb?.Dispose();
        }

        private void 終了XToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void オプションOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                LogFile = null;
                LogFile2 = null;
                LogFile3 = null;
                LogFile4 = null;

                using (var fo2 = new Form2(this, accountdbfile))
                {
                    fo2.ShowDialog();
                }
            }
            catch (Exception Ex)
            {
                AddLog("オプションメニューが開けませんでした。\r\n" + Ex.Message, 2);
            }
        }

        private void 録画フォルダーを開くOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(props.SaveDir))
            {
                Process.Start(props.SaveDir);
            }
            else
            {
                Process.Start(System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            }
        }

        private async void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            try
            {
                ClearHosoData();
                ClearLog();

                var exec_file = props.ExecFile[0];
                exec_file = GetExecFile(exec_file);
                if (!File.Exists(exec_file))
                {
                    AddLog("実行ファイルがありません。", 2);
                    return;
                }

                var save_dir = String.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                if (!Directory.Exists(save_dir))
                {
                    AddLog("保存フォルダーが存在しません。", 2);
                    return;
                }

                LogFile = Props.GetLogfile(save_dir, "conv");
                LogFile2 = Props.GetExecLogfile(save_dir, "conv");
                LogFile3 = null;
                LogFile4 = null;

                for (int i = 0; i < files.Length; i++)
                {
                    AddLog("出力開始します。", 1);
                    await Task.Run(() => StartExtract(files[i]));
                }
            }
            catch (Exception Ex)
            {
                if (_ndb != null)
                {
                    _ndb.Dispose();
                }
                AddLog("ドラッグ＆ドロップできません。\r\n" + Ex.Message, 2);
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
    }

}