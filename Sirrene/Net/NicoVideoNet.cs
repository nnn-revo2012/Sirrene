using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Xml;
using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Sirrene.Prop;

namespace Sirrene.Net
{

    static class TimeoutExtention
    {
        public static async Task Timeout(this Task task, int timeout)
        {
            var delay = Task.Delay(timeout);
            if (await Task.WhenAny(task, delay) == delay)
            {
                throw new TimeoutException();
            }
        }

        public static async Task<T> Timeout<T>(this Task<T> task, int timeout)
        {
            await ((Task)task).Timeout(timeout);
            return await task;
        }
    }

    public class NicoVideoNet : IDisposable
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

        public bool IsLoginStatus { get; private set; }

        public NicoVideoNet()
        {
            IsDebug = false;

            IsLoginStatus = false;

            var wc = new WebClientEx();
            _wc = wc;

            _wc.Encoding = Encoding.UTF8;
            _wc.Proxy = null;
            _wc.Headers.Add(HttpRequestHeader.UserAgent, Props.UserAgent);
            _wc.timeout = 30000;
        }

        ~NicoVideoNet()
        {
            this.Dispose();
        }


        public IList<KeyValuePair<string, string>> GetCookieList()
        {
            var result = new Dictionary<string, string>();
            var cc = _wc.cookieContainer;

            foreach (Cookie ck in cc.GetCookies(new Uri(Props.NicoDomain)))
                result.Add(ck.Name.ToString(), ck.Value.ToString());

            return result.ToList();
        }
        public CookieContainer GetCookieContainer()
        {
            return _wc.cookieContainer;
        }

        public void SetCookieContainer(CookieContainer cookie)
        {
            if (cookie != null)
                _wc.cookieContainer = cookie;
            return;
        }
        //*************** URL系 *******************

        //動画URLから動画IDをゲット(sm|nm|so00000000000)
        public static string GetVideoID(string videoUrl)
        {
            var stmp = Regex.Match(videoUrl, "((sm|nm|so)[0-9]+)").Groups[1].Value;
            if (string.IsNullOrEmpty(stmp)) stmp = null;
            return stmp;
        }

        //動画IDから動画URLをゲット
        public static string GetNicoPageUrl(string videoID)
        {
            if (string.IsNullOrEmpty(videoID)) return null;
            return Props.NicoVideoUrl + videoID;
        }

        //*************** HTTP系 *******************

        //ニコニコにログイン
        public async Task<(bool flag, string err, int neterr)> LoginNico(string mail, string pass)
        {
            bool flag = false;
            string err = null;
            int neterr = 0;

            try {
                var ps = new NameValueCollection();
                //ログイン認証(POST)
                ps.Add("mail_tel", mail);
                ps.Add("password", pass);

                _wc.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");
                _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");

                byte[] resArray = await _wc.UploadValuesTaskAsync(Props.NicoLoginUrl, ps).Timeout(_wc.timeout);
                var data = System.Text.Encoding.UTF8.GetString(resArray);
                flag = Regex.IsMatch(data, "user\\.login_status += +\\'login\\'", RegexOptions.Compiled) ? true : false;
                IsLoginStatus = flag;
                /*
                user.login_status = 'login';
                user.member_status = 'premium';
                user.ui_area = 'jp';
                user.ui_lang = 'ja-jp';
                */
                if (IsDebug)
                {
                    //responseヘッダーの数と内容を表示
                    var strtmp = string.Format("Login Headers: {0}\r\n\r\n", _wc.ResponseHeaders.Count);
                    for (int i = 0; i < _wc.ResponseHeaders.Count; i++)
                        strtmp += string.Format("{0}: {1}\r\n", _wc.ResponseHeaders.GetKey(i),
                            _wc.ResponseHeaders.Get(i));
                    MessageBox.Show(strtmp);
                }
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(LoginNico), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
                return (flag, err, neterr);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(LoginNico), Ex);
                err = Ex.Message;
                return (flag, err, neterr);
            }

            return (flag, err, neterr);
        }

        //ログインしているかどうか取得
        public async Task<(bool flag, string err, int neterr)> IsLoginNicoAsync()
        {
            bool flag = false;
            string err = null;
            int neterr = 0;

            try
            {
                _wc.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");
                _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");

                var hs = await _wc.DownloadStringTaskAsync(Props.NicoDomain).Timeout(_wc.timeout);
                flag = Regex.IsMatch(hs, "user\\.login_status += +\\'login\\'", RegexOptions.Compiled) ? true : false;
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(IsLoginNicoAsync), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
                return (flag, err, neterr);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(IsLoginNicoAsync), Ex);
                err = Ex.Message;
                return (flag, err, neterr);
            }
            return (flag, err, neterr);            
        }

        //動画ページから動画情報を取得
        public async Task<(JObject data, string err, int neterr)> GetNicoPageAsync(string nicoUrl)
        {
            JObject data = null;
            string err = null;
            int neterr = 0;
            try
            {
                var nicoid = GetVideoID(nicoUrl);
                if (string.IsNullOrEmpty(nicoid)) return (data, "null", neterr);

                _wc.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");
                _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");

                var hs = await _wc.DownloadStringTaskAsync(Props.NicoVideoUrl + nicoid).Timeout(_wc.timeout);
                if (string.IsNullOrEmpty(hs)) return (data, "null", neterr);
                var ttt = WebUtility.HtmlDecode(Regex.Match(hs, "data-api-data=\"([^\"]*)\"", RegexOptions.Compiled).Groups[1].Value);
                if (string.IsNullOrEmpty(ttt))
                {
                    if (hs.IndexOf("window.NicoGoogleTagManagerDataLayer = [];") > 0)
                        return (data, "Not login and not found data-api-data. Need login.", 0);
                    else
                        return (data, "Not found data-api-data", 0);
                }
                data = JObject.Parse(ttt.Replace("&quot", "\""));
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetNicoPageAsync), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
                return (data, err, neterr);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetNicoPageAsync), Ex);
                err = Ex.Message;
                return (data, err, neterr);
            }
            return (data, err, neterr);
        }

        public async Task<(string data, string err, int neterr)> GetNicoCrossDomainAsync(string url)
        {
            string data = null;
            string err = null;
            int neterr = 0;
            try
            {
                if (string.IsNullOrEmpty(url)) return (data, "null", neterr);

                _wc.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");
                _wc.Headers.Add(HttpRequestHeader.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");

                int index = url.IndexOf("/", "https://".Length);
                var host_url = url.Substring(0, index);
                data = await _wc.DownloadStringTaskAsync(host_url).Timeout(_wc.timeout);
                if (string.IsNullOrEmpty(data)) return (data, "null", neterr);
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(GetNicoCrossDomainAsync), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
                return (data, err, neterr);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetNicoPageAsync), Ex);
                err = Ex.Message;
                return (data, err, neterr);
            }

            return (data, err, neterr);
        }

        public async Task<(JObject data, string err, int neterr)> PostNicoSessionAsync(string url, string senddata)
        {
            JObject data = null;
            string err = null;
            int neterr = 0;
            try
            {
                if (string.IsNullOrEmpty(url)) return (data, "url is null", neterr);
                if (string.IsNullOrEmpty(senddata)) return (data, "senddata is null", neterr);

                _wc.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                _wc.Headers.Add(HttpRequestHeader.Accept, "application/json");
                _wc.Headers.Add(HttpRequestHeader.AcceptLanguage, "ja,en-US;q=0.9,en;q=0.8");
                _wc.Headers.Add("Origin", "https://www.nicovideo.jp");
                _wc.Headers.Add(HttpRequestHeader.Referer, "https://www.nicovideo.jp/");

                var session_url = url + "?_format=json";
                var result = await _wc.UploadStringTaskAsync(session_url, "POST", senddata).Timeout(_wc.timeout);
                if (string.IsNullOrEmpty(result)) return (data, "result is null", neterr);
                data = JObject.Parse(result);
            }
            catch (WebException Ex)
            {
                DebugWrite.WriteWebln(nameof(PostNicoSessionAsync), Ex);
                if (Ex.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse errres = (HttpWebResponse)Ex.Response;
                    neterr = (int)errres.StatusCode;
                    err = neterr.ToString() + " " + errres.StatusDescription;
                }
                else
                    err = Ex.Message;
                return (data, err, neterr);
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetNicoPageAsync), Ex);
                err = Ex.Message;
                return (data, err, neterr);
            }

            return (data, err, neterr);
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
