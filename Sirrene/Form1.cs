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
        private JObject dataJson = null;            //動画情報(JObject)
        private ExecPsInfo epi = null;                //実行／保存ファイル情報
        private RetryInfo rti = null;                 //リトライ情報
        private CookieContainer cookiecontainer = new CookieContainer();

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

        private void Button1_Click(object sender, EventArgs e)
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
                Task.Run(() => StartRec());

            }
            catch (Exception Ex)
            {
                AddLog(nameof(Button1_Click) + "() Error: \r\n" + Ex.Message, 2);
            }
        }
        public async void StartRec()
        {
            cookiecontainer = null;
            try
            {
                if (props.IsLogin == IsLogin.always)
                {
                    bool flag = false;
                    //ニコニコにログイン
                    switch (props.LoginMethod.ToString())
                    {
                        case "login":
                            using (var _nvn = new NicoVideoNet())
                            using (var db = new Prop.Account(accountdbfile))
                            {
                                var alias = "nico_01";
                                string user = null; string pass = null;
                                if (!_nvn.IsLoginStatus)
                                {
                                    if (db.GetSession(alias, _nvn.GetCookieContainer()))
                                    {
                                        //ニコニコにアクセスする
                                        (flag, _, _) = await _nvn.IsLoginNicoAsync();
                                        if (flag)
                                        {
                                            //ログインしていればOK
                                            AddLog("Already logged in", 1);
                                            cookiecontainer = _nvn.GetCookieContainer();
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
                                    (flag, _, _) = await _nvn.LoginNico(props.UserID, props.Password);
                                    if (!flag)
                                    {
                                        AddLog("Login Failed: login error", 1);
                                        return;
                                    }
                                    else
                                    {
                                        AddLog("Login OK", 1);
                                        db.SetSession(alias, _nvn.GetCookieContainer());
                                        cookiecontainer = _nvn.GetCookieContainer();
                                    }
                                }
                                else
                                {
                                    AddLog("Already logged in", 1);
                                    cookiecontainer = _nvn.GetCookieContainer();
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
                using (var _nvn = new NicoVideoNet())
                {
                    _nvn.SetCookieContainer(cookiecontainer);
                    (dataJson, err, neterr) = await _nvn.GetNicoPageAsync(videoId);
                }
                if (err != null)
                {
                    AddLog("この動画は存在しないか、削除された可能性があります。 (" + err + ")", 1);
                    return;
                }
                AddDataJson(dataJson.ToString());
                var djs = new DataJson(videoId);
                djs.GetData(dataJson);
                if (djs.IsPremium)
                        AddLog("Premium Account", 1);
                    else
                        AddLog("Normal Account", 1);
                if (djs.IsPeakTime)
                    AddLog("PeakTime", 1);
                else
                    AddLog("No PeakTime", 1);
                if (djs.IsEconomy)
                    AddLog("Economy Time", 1);
                else
                    AddLog("No Economy Time", 1);
                if (!djs.IsWatchVideo)
                    AddLog("Can't Watch Video", 1);

                //保存ファイル名作成
                epi = new ExecPsInfo();
                epi.Sdir = string.IsNullOrEmpty(props.SaveDir) ? System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : props.SaveDir;
                epi.Exec = GetExecFile(props.ExecFile[0]);
                epi.Arg = props.ExecCommand[0];
                epi.Sfile = djs.SetRecFileFormat(props.SaveFile);
                epi.Sfolder = djs.SetRecFolderFormat(props.SaveFolder);
                epi.Protocol = "hls";
                epi.Seq = 0;
                //ExecPsInfo.MakeRecDir(epi);

                if (props.UseExternal == UseExternal.native)
                {
                    var file = ExecPsInfo.GetSaveFileSqlite3(epi);
                    file += ".sqlite3";
                    epi.SaveFile = file;
                    _ndb = new NicoDb(this, epi.SaveFile);
                    //_ndb.CreateDbAll();

                    //_ndb.WriteDbKvsProps(djs.Data_Props);
                }
                AddLog("File: "+epi.SaveFile, 1);

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

                // Sessionを作成
                // Sessionを送る
                // Sessionからcontent_uriを取得

                var ri = new RetryInfo();
                rti = ri;
                rti.Count = 3;

                //if (props.UseExternal == UseExternal.native)
                //    _rHtml = new RecHtml(this, djs, cookiecontainer, _ndb, rti);
                //else
                //    _eProcess = new ExecProcess(this, djs, rti);

                //放送情報を表示
                //DispHosoData(bci);

                //1秒おきに状態を調べて処理する
                start_flg = true;
                while (start_flg == true)
                {
                    //await CheckStatus();
                    await Task.Delay(1000);
                }

            } // try
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

        private void オプションOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                LogFile = null;
                LogFile2 = null;

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
    }

}