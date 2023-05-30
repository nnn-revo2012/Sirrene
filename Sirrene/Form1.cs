using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

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
        private volatile bool start_flg = false;
        private static int ProgramStatus { get; set; } //プログラム状態

        //dispose するもの
        private ExecProcess _eProcess = null;         //Process
        private RecHtml _rHtml = null;                //RecHtml
        private NicoDb _ndb = null;                   //NicoDb

        //private DataApiDataInfo dadi = null;          //動画情報
        private string dataapidata = null;            //動画情報(string)
        private ExecPsInfo epi = null;                //実行／保存ファイル情報
        private RetryInfo rti = null;                 //リトライ情報

        private string videoId = null;

        private string accountdbfile;
        private readonly object lockObject = new object();  //情報表示用
        private readonly object lockObject2 = new object(); //実行ファイルのログ用
        private string LogFile;
        private string LogFile2;
        private string LogFile3;

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

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //中断処理
                if (button1.Text == "中断")
                {
                    if (_rHtml != null)
                    {
                        _rHtml.BreakProcess("");
                    }
                    if (_eProcess != null)
                    {
                        _eProcess.BreakProcess(epi.BreakKey);
                    }
                    if (_ndb != null)
                    {
                        _ndb.Dispose();
                    }
                    AddLog("中断しました。", 1);
                    EnableButton(true);
                    start_flg = false;
                    return;
                }

                LogFile = null;
                LogFile2 = null;
                LogFile3 = null;

                //ニコ生に接続
                ClearHosoData();
                ClearLog();

                /*
                                //フォルダやファイルのチェック
                                var save_dir = String.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments): props.SaveDir;
                                if (!Directory.Exists(save_dir))
                                {
                                    AddLog("保存フォルダーが存在しません。", 2);
                                    return;
                                }

                                var exec_file = props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())];
                                exec_file = GetExecFile(exec_file);
                                if (props.UseExternal != UseExternal.native)
                                    if (!File.Exists(exec_file))
                                    {
                                        AddLog("実行ファイルがありません。", 2);
                                        return;
                                    }
                */

                var save_dir = "D:\\home\\tmp";
                var exec_file = "";

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
                LogFile3 = Props.GetDataPropsfile(save_dir, videoId);

                AddLog("録画開始します。", 1);
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
                Task.Run(() => StartRec());

            }
            catch (Exception Ex)
            {
                AddLog(nameof(button1_Click) + "() Error: \r\n" + Ex.Message, 2);
            }
        }
        public async void StartRec()
        {
            try
            {
                var _nvn = new NicoVideoNet();

                if (props.IsLogin == IsLogin.always)
                {
                    //ニコニコにログイン
                    switch (props.LoginMethod.ToString())
                    {
                        case "login":
                            using (var db = new Prop.Account(accountdbfile))
                            {
                                var alias = "nico_01";
                                string user = null;string pass = null;
                                if (!_nvn.IsLoginStatus)
                                {
                                    if (db.GetSession(alias, _nvn.GetCookieContainer()))
                                    {
                                        //ニコニコにアクセスする
                                        if (await _nvn.IsLoginNicoAsync())
                                        {
                                            //ログインしていればOK
                                            AddLog("Logged in", 1);
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
                                    if (!(await _nvn.LoginNico(props.UserID, props.Password)))
                                    {
                                        AddLog("Login Failed: login error", 1);
                                        return;
                                    }
                                    else
                                    {
                                        AddLog("Login OK", 1);
                                        db.SetSession(alias, _nvn.GetCookieContainer());
                                    }
                                }
                                else
                                {
                                    AddLog("Logged in", 1);
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
*/
                            break;
                    }
                }
                else
                {
                    AddLog("ログインなし", 1);
                }

                //番組情報を取得する
                dataapidata = await _nvn.GetNicoPageAsync(videoId);
                AddDataProps(bci.Data_Props);
                if (bci.Status != "ok")
                {
                    AddLog("放送情報が取得できませんでした。", 1);
                    AddLog("Status: " + bci.Error, 1);
                    return;
                }
                AddLog("Account: " + bci.AccountType, 1);
                var ws_ver = Regex.Match(bci.WsUrl, @"/wsapi/([^/]*)/").Groups[1].Value;
                if (ws_ver == "v2")
                    AddLog("WebSocket v2", 1);
                else if (ws_ver == "v1")
                {
                    AddLog("WebSocket v1", 1);
                    return;
                }
                else
                {
                    AddLog("WebSocket不明", 1);
                    return;
                }

                //ＴＳ開始時間
                int ii;
                if (int.TryParse(textBox2.Text, out ii))
                    bci.StartTs_Time = ii;

                if (props.Protocol == Protocol.rtmp)
                {
                    if (bci.IsTimeShift() || bci.Provider_Type != "official")
                    {
                        AddLog("RTMP録画は公式の生放送のみです。", 1);
                        return;
                    }
                }

                //保存ファイル名作成
                epi = new ExecPsInfo();
                epi.Sdir = string.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                epi.Exec = GetExecFile(props.ExecFile[Props.ParseProtocol(props.Protocol.ToString())]);
                epi.Arg = props.ExecCommand[Props.ParseProtocol(props.Protocol.ToString())];
                epi.Sfile = bci.SetRecFileFormat(props.SaveFile);
                epi.Sfolder = bci.SetRecFolderFormat(props.SaveFolder);
                epi.Protocol = props.Protocol.ToString();
                epi.Seq = 0;
                ExecPsInfo.MakeRecDir(epi);

                if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                {
                    var file = ExecPsInfo.GetSaveFileSqlite3(epi);
                    if (bci.IsTimeShift()) file += Props.TIMESHIFT;
                    file += ".sqlite3";
                    epi.SaveFile = file;
                    _ndb = new NicoDb(this, epi.SaveFile);
                    _ndb.CreateDbAll();

                    _ndb.WriteDbKvsProps(bci.Data_Props);
                }

/*
                //コメント情報
                if (props.IsComment)
                {
                    cmi = new CommentInfo(bci.User_Id);
                    cmi.OpenTime = bci.Open_Time;
                    cmi.BeginTime = bci.Begin_Time;
                    cmi.EndTime = bci.End_Time;
                    if (bci.IsTimeShift())
                        cctl = new CommentControl();
                    else
                        cctl = null;
                    _nNetComment = new NicoNetComment(this, bci, cmi, _nvn, _ndb, cctl);
                }
*/
                var ri = new RetryInfo();
                rti = ri;
                rti.Count = props.Retry;

                if (props.Protocol == Protocol.hls && props.UseExternal == UseExternal.native)
                    _rHtml = new RecHtml(this, bci, _nNetComment, _nvn.GetCookieContainer(), _ndb, rti);
                else
                    _eProcess = new ExecProcess(this, bci, _nNetComment, rti);
                _nNetStream = new NicoNetStream(this, bci, cmi, epi, _nNetComment, _eProcess, _rHtml, rti);

                AddLog("webSocketUrl: " + bci.WsUrl, 9);
                AddLog("frontendId: " + bci.FrontEndId, 9);
                //bci.FrontEndId = "90";

                //放送情報を表示
                DispHosoData(bci);

                //WebSocket接続開始
                _nNetStream.Connect();

                //1秒おきに状態を調べて処理する
                start_flg = true;
                while (start_flg == true)
                {
                    //await CheckStatus();
                    await Task.Delay(1000);
                }

            }
            catch (Exception Ex)
            {
                AddLog(nameof(StartRec) + "() Error: \r\n" + Ex.Message, 2);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_rHtml != null)
            {
                _rHtml.Dispose();
                _rHtml = null;
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

    }

}