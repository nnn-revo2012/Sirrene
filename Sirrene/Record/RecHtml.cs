using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Sirrene.Net;
using Sirrene.Proc;
using Sirrene.Prop;

namespace Sirrene.Rec
{
    public class RetryInfo
    {
        public bool IsRetry { set; get; }
        public int Count { set; get; }

        public RetryInfo()
        {
            this.IsRetry = false;
            this.Count = 3;
        }
    }

    public class PlayListInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }
        public int ErrNo { set; get; }
        public string MasterUrl { set; get; }
        public string BaseUrl { set; get; }
        public ICollection<PlayerInfo> Player { private set; get; }
        public long SeqNo { set; get; }
        public long LastSeqNo { set; get; }
        public bool EndList { set; get; }
        public double Position { set; get; }

        public PlayListInfo()
        {
            this.Status = "";
            this.Error = "";
            this.ErrNo = 0;
            this.Player = new List<PlayerInfo>();
            this.SeqNo = -1;
            this.LastSeqNo = -1;
            this.EndList = false;
            this.Position = 0.0;
        }
    }

    public class PlayerInfo
    {
        public int Bandwidth { set; get; }
        public string pUrl { set; get; }
    }

    public class SegmentInfo
    {
        public string Status { set; get; }
        public string Error { set; get; }
        public int ErrNo { set; get; }
        public string BaseUrl { set; get; }
        public ICollection<Segment> Seg { private set; get; }
        public long SeqNo { set; get; }
        public bool EndList { set; get; }
        public double Position { set; get; }

        public SegmentInfo()
        {
            this.Status = "";
            this.Error = "";
            this.ErrNo = 0;
            this.Seg = new List<Segment>();
            this.SeqNo = -1;
            this.EndList = false;
        }
    }

    public class Segment
    {
        public double ExtInfo { set; get; }
        public string sUrl { set; get; }
        public string sFile { set; get; }
    }

    public class RecHtml : AEexecProcess, IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private WebClientEx _wc = null;

        private class WebClientEx : WebClient
        {
            public CookieContainer cookieContainer = new CookieContainer();
            public int timeout;

            protected override WebRequest GetWebRequest(Uri address)
            {
                var wr = base.GetWebRequest(address);

                HttpWebRequest hwr = wr as HttpWebRequest;
                if (hwr != null)
                {
                    hwr.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; //圧縮を有効化
                    hwr.CookieContainer = cookieContainer; //Cookie
                    hwr.Timeout = timeout;
                }
                return wr;
            }
        }

        //Debug
        public bool IsDebug { get; set; }

        public RecHtml(Form1 fo, DataJson djs, CookieContainer cc, NicoDb ndb, RetryInfo ri)
        {
            IsDebug = false;

            PsStatus = -1;
            this._ndb = ndb;
            this._djs = djs;
            this._ri = ri;
            this._form = fo;


            var timeout = 20000;
            var wc = new WebClientEx();
            _wc = wc;

            _wc.Encoding = Encoding.UTF8;
            _wc.Proxy = null;
            _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
            _wc.cookieContainer = cc;
            _wc.timeout = timeout;     
            if (IsDebug)
            {
                foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                    Debug.WriteLine(ck.Name.ToString() + ": " + ck.Value.ToString());
                for (int i = 0; i < _wc.Headers.Count; i++)
                    Debug.WriteLine(_wc.Headers.GetKey(i).ToString() + ": " + _wc.Headers.Get(i));
            }
        }

        ~RecHtml()
        {
            this.Dispose();
        }

        public override void ExecPs(string masterfile, string outfile)
        {
            try
            {
                if (Form1.props.IsVideo)
                    _form.AddLog("プロセス実行中です。", 1);
                PsStatus = 0; //実行中

                if (Form1.props.IsVideo)
                    Task.Run(() => HtmlRecord(masterfile, outfile));
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }

        private async Task HtmlRecord(string masterfile, string outfile)
        {
            long waittime = 3000;
            int delaytime = 1000;

            try
            {
                var stime = string.Empty;
                if (_ndb.CountDbMedia() > 0)
                {
                    var lp = _ndb.GetDbMediaLastPos();
                    if (lp > 0.0)
                        stime = "&start=" + lp.ToString();
                }

                _form.AddLog("MasterFile: " + masterfile + stime + "\r\n", 9);

                // masterファイルをGet
                var sw = new Stopwatch();
                sw.Start();
                var pli = await GetMasterM3u8Async(masterfile + stime);
                sw.Stop();
                if (pli.Status != "Ok" || pli.Player.Count() <= 0)
                {
                    _form.AddLog("GetMasterM3u8 Error: " + pli.Error + "", 1);
                    EndPs(2); //Retry
                }
                var seqno = _ndb.GetDbMediaLastSeqNo();
                if (seqno > 0)
                {
                    pli.SeqNo = seqno;
                    pli.Position = _ndb.GetDbMediaLastPos();
                }
                await Task.Delay(100);
                while (PsStatus == 0)
                {
                    if (pli.EndList)
                    {
                         EndPs(1);
                         break;
                    }
                    // playerファイルをGet
                    var sgi = await GetPlayerM3u8Async(pli.Player.FirstOrDefault().pUrl);
                    if (sgi.Status != "Ok" || sgi.Seg.Count() <= 0)
                    {
                        _form.AddLog("GetPlayerM3u8 Error: " + sgi.Error + "", 1);
                        EndPs(2); //Retry
                        break;
                    }
                    if (pli.SeqNo < 0)
                    {
                        pli.SeqNo = sgi.SeqNo;
                        pli.Position = sgi.Position;
                    }
                    await Task.Delay(100);

                    // 指定秒ごとにSegmentファイルを取得
                    var sc = 0;
                    foreach (var item in sgi.Seg)
                    {
                        if (PsStatus > 0) break;
                        if (sgi.SeqNo >= pli.SeqNo)
                        {
                            sw.Restart();
                            if (!await GetSegmentAsync(item, pli, sgi))
                                EndPs(2); //Retry
                            sw.Stop();
                            _form.AddLog("SeqNo=" + sgi.SeqNo.ToString() + " " + sw.ElapsedMilliseconds.ToString() + "mSec", 1);
                            sgi.SeqNo++;
                            sc++;
                            sgi.Position += item.ExtInfo;
                            if (PsStatus > 0) break;
                            if (sw.ElapsedMilliseconds > waittime)
                            {
                                await Task.Delay(200);
                            }
                            else
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(waittime - sw.ElapsedMilliseconds));
                            }
                        }
                        else
                        {
                            if (PsStatus > 0) break;
                            sgi.SeqNo++;
                            sgi.Position += item.ExtInfo;
                        }
                    }
                    if (PsStatus > 0) break;
                    if (sc <= 1)
                    {
                        _form.AddLog("Wait " + delaytime.ToString() + "mSec", 1);
                        await Task.Delay(delaytime);
                    }
                    pli.SeqNo = sgi.SeqNo;
                    pli.Position = sgi.Position;
                    if (sgi.EndList)
                    {
                        pli.EndList = true;
                        EndPs(1);
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }

        public void EndPs(int status)
        {
            //1:正常終了 2:再接続 3:再接続(長)
            PsStatus = status;
            if (status >= 2)
            {
                _ri.IsRetry = true;
            }
        }

        public override void BreakProcess(string breakkey)
        {
            EndPs(1);
        }

        //master.m3u8からplayer.m3u8のURLを取得
        public async Task<PlayListInfo> GetMasterM3u8Async(string url)
        {
            _form.AddLog("GetMasterFile", 9);
            var pli = new PlayListInfo();
            pli.Status = "Error";
            pli.Error = "PARAMERROR";
            if (string.IsNullOrEmpty(url)) return pli;

            try
            {
                pli.MasterUrl = url;
                var idx = url.IndexOf("master.m3u8");
                if (idx >= 0) pli.BaseUrl = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url).Timeout(_wc.timeout);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    int bw;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        _form.AddLog(line + "", 9);
                        if (line.Contains("#EXT-X-STREAM-INF"))
                        {
                            var pi = new PlayerInfo();
                            if (int.TryParse(Regex.Match(line, @"[:,]BANDWIDTH=(\d+)").Groups[1].Value, out bw))
                                pi.Bandwidth = bw;
                            line = sr.ReadLine();
                            _form.AddLog(line + "", 9);
                            if (!string.IsNullOrEmpty(line))
                            {
                                pi.pUrl = pli.BaseUrl + line;
                                pli.Player.Add(pi);
                            }
                        }
                    }
                }
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetMasterM3u8Async), Ex);
                pli.Error = Ex.Status.ToString();
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        pli.Error = ((int)errres.StatusCode).ToString();
                }
                return pli;
            }
            catch (Exception Ex) //その他のエラー
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetMasterM3u8Async), Ex);
                pli.Error = Ex.ToString();
                return pli;
            }
            pli.Status = "Ok";
            pli.Error = "";
            return pli;
        }

        //player.m3u8からsegment情報を取得
        public async Task<SegmentInfo> GetPlayerM3u8Async(string url)
        {
            _form.AddLog("GetPlayerFile", 9);
            var sgi = new SegmentInfo();
            sgi.Status = "Error";
            sgi.Error = "PARAMERROR";
            if (string.IsNullOrEmpty(url)) return sgi;

            try
            {
                var idx = url.IndexOf("playlist.m3u8");
                if (idx >= 0) sgi.BaseUrl = url.Substring(0, idx);
                var str = await _wc.DownloadStringTaskAsync(url).Timeout(_wc.timeout);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str), false))
                using (var sr = new StreamReader(stream, Encoding.UTF8))
                {
                    int sn;
                    double ei;
                    string line;
                    while ((line = sr.ReadLine()) != null) // 1行ずつ読み出し。
                    {
                        _form.AddLog(line + "", 9);
                        var ttt = line.Split(':');
                        switch (ttt[0])
                        {
                            case "#EXT-X-MEDIA-SEQUENCE":
                                if (int.TryParse(ttt[1], out sn))
                                    sgi.SeqNo = sn;
                                break;
                            case "#CURRENT-POSITION":
                                if (double.TryParse(ttt[1], out ei))
                                    sgi.Position = ei;
                                break;
                            case "#DMC-CURRENT-POSITION":
                                if (double.TryParse(ttt[1], out ei))
                                    sgi.Position = ei;
                                break;
                            case "#EXT-X-ENDLIST":
                                sgi.EndList = true;
                                break;
                            case "#EXTINF":
                                var sg = new Segment();
                                if (double.TryParse(ttt[1].Split(',')[0], out ei))
                                    sg.ExtInfo = ei;
                                line = sr.ReadLine();
                                _form.AddLog(line + "", 9);
                                if (!string.IsNullOrEmpty(line))
                                {
                                    sg.sFile = line.Split('?')[0];
                                    sg.sUrl = sgi.BaseUrl + line;
                                    sgi.Seg.Add(sg);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetPlayerM3u8Async), Ex);
                sgi.Error = Ex.Status.ToString();
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        sgi.Error = ((int)errres.StatusCode).ToString();
                }
                return sgi;
            }
            catch (Exception Ex) //その他のエラー
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetPlayerM3u8Async), Ex);
                sgi.Error = Ex.ToString();
                return sgi;
            }
            sgi.Status = "Ok";
            sgi.Error = "";
            return sgi;
        }

        //segmentファイルを取得
        public async Task<bool> GetSegmentAsync(Segment seg, PlayListInfo pli, SegmentInfo sgi)
        {
            //_form.AddLog("GetSegmentFile", 9);
            byte[] data = null;
            int ll;
            if (string.IsNullOrEmpty(seg.sUrl)) return false;

            try
            {
                data = await _wc.DownloadDataTaskAsync(seg.sUrl).Timeout(_wc.timeout);
                if (int.TryParse(_wc.ResponseHeaders.Get("Content-Length"), out ll))
                {
                    if (ll != data.Length)
                        _form.AddLog("Seg " + sgi.SeqNo.ToString() + ": Size Error ", 1);
                }
                ll = data.Length;
                //_form.AddLog("Input: " + seg.sUrl + "", 9);
                //_form.AddLog("SeqNo=" + sgi.SeqNo.ToString() + " Size: " + data.Length.ToString() + " Content-Length: " + ll.ToString() + "", 9);

                //データーをSqlite3に書き込み
                _ndb.WriteDbMedia(seg, pli, sgi, data, ll, 0);

            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetSegmentAsync), Ex);
                int errno = 0;
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        errno = (int)errres.StatusCode;
                }
                _form.AddLog("GetSegment Error: " + Ex.Status.ToString() + " (" + errno + ")", 1);
                return false;
            }
            catch (Exception Ex) //その他のエラー
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(GetSegmentAsync), Ex);
                return false;
            }
            return true;
        }

        //速度変更
        public async Task<bool> SetPlayControlAsync(string speed, PlayListInfo pli)
        {
            var result = false;
            _form.AddLog("SetPlayControlAsync", 9);

            try
            {
                var ttt = pli.MasterUrl.Split('?')[1].Split('&').FirstOrDefault(s => s.StartsWith("ht2_nicolive="));
                var url = pli.BaseUrl + "play_control.json?" + ttt + "&play_speed="+speed;
                _form.AddLog(url + "", 9);
                var str = await _wc.DownloadStringTaskAsync(url).Timeout(_wc.timeout);
                var res = JObject.Parse(str);
                if (res["meta"]["status"].ToString() == "200")
                    result = true;
                //{ "meta":{ "status":200,"message":"ok"},"data":{ "play_control":{ "play_speed":0.25} } }
                _form.AddLog(str + "", 9);
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(SetPlayControlAsync), Ex);
                int errno = 0;
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var errres = (HttpWebResponse)Ex.Response;
                    if (errres != null)
                        errno = (int)errres.StatusCode;
                }
                _form.AddLog("SetPlayControlAsync Error: " + Ex.Status.ToString() + " (" + errno + ")", 1);
                return result;
            }
            catch (Exception Ex) //その他のエラー
            {
                //HttpRequestException
                DebugWrite.Writeln(nameof(SetPlayControlAsync), Ex);
                return result;
            }
            return result;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _wc?.Dispose();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            //GC.SuppressFinalize(this);
        }

    }
}
