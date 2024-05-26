using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Sirrene.Prop;
using Sirrene.Net;

namespace Sirrene.Net
{
    public class NicoComment : IDisposable
    {
        private bool disposedValue = false; // 重複する呼び出しを検知するには

        //Debug
        public bool IsDebug { get; set; }

        private DataJson _djs = null;         //DataJson
        private NicoVideoNet _nvn = null;     //WebClient
        private CommentInfo _cmi = null;      //Comment
        private CommentControl _cctl = null;  //CommentControl
        private Form1 _form = null;

        public NicoComment(Form1 fo, DataJson djs, CommentInfo cmi, NicoVideoNet nvn)
        {
            IsDebug = false;

            this._nvn = nvn;
            this._djs = djs;
            this._cmi = cmi;
            //this._cctl = cctl;
            this._form = fo;
        }

        ~NicoComment()
        {
            this.Dispose();
        }

        public async Task<(bool result, string err, int neterr)> GetCommentAsync(CookieContainer cookie)
        {
            string data = null;
            bool result = false;
            string err = null;
            int neterr = 0;

            FileInfo jsonfile = null;
            FileInfo xmlfile = null;

            var njp = new NicoJsonParser(_form);

            try
            {
                //dataを組み立てる
                string senddata = "{\"params\": " + _djs.ComParams + ",";
                senddata += "\"additionals\": {},";
                senddata += "\"threadKey\": \"" + _djs.ComThreadKey + "\"}";
                string url = _djs.ComServer + "/v1/threads";
                _form.AddLog("senddata: " + senddata, 9);
                _form.AddLog("url: " + url, 9);
                (data, err, neterr) = await _nvn.PostNicoCommentAsync(cookie, url, senddata);
                if (_form.IsBreak_flg)
                    return (result, err, neterr);

                if (!string.IsNullOrEmpty(err))
                {
                    _form.AddLog("PostNicoCommentAsync Error: " + err + "(" + neterr + ")", 1);
                }
                else
                {
                    (result , err) = WriteJson(_cmi.SaveFile + ".json", data);
                    if (result && err == null)
                    {
                        jsonfile = new FileInfo(_cmi.SaveFile + ".json");
                        while (true)
                        {
                            xmlfile = new FileInfo(_cmi.SaveFile + ".xml");
                            njp.NvCommentJson2xml(jsonfile, xmlfile, "user", false);
                            if (_form.IsBreak_flg)
                                break;
                            xmlfile = new FileInfo(_cmi.SaveFile + Props.OWNER_EXT);
                            njp.NvCommentJson2xml(jsonfile, xmlfile, "owner", false);
                            if (_form.IsBreak_flg)
                                break;
                            xmlfile = new FileInfo(_cmi.SaveFile + Props.EASY_EXT);
                            njp.NvCommentJson2xml(jsonfile, xmlfile, "easy", false);
                            if (_form.IsBreak_flg)
                                break;
                            if (_djs.IsOptional)
                            {
                                xmlfile = new FileInfo(_cmi.SaveFile + Props.OPTIONAL_EXT);
                                njp.NvCommentJson2xml(jsonfile, xmlfile, "optional", false);
                            }
                            if (_form.IsBreak_flg)
                                break;
                            if (_djs.IsNicos)
                            {
                                xmlfile = new FileInfo(_cmi.SaveFile + Props.NICOS_EXT);
                                njp.NvCommentJson2xml(jsonfile, xmlfile, "nicos", false);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception Ex) //その他のエラー
            {
                DebugWrite.Writeln(nameof(GetCommentAsync), Ex);
                err = Ex.Message;
            }

            return (result, err, neterr);
        }

        public (bool result, string err)  WriteJson(string dfile, string data)
        {
            string err = null;
            bool result = false;
            try
            {
                // ファイルに文字列データを書き込み
                File.WriteAllText(dfile, data);
                result = true;
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(WriteJson), Ex);
                err = Ex.Message;
            }

            return (result, err);
        }

        public static void BeginXmlDoc(StreamWriter sw)
        {
            sw.Write("<?xml version='1.0' encoding='UTF-8'?>\r\n");
            sw.Write("<packet>\r\n");
        }

        public static void EndXmlDoc(StreamWriter sw)
        {
            sw.Write("</packet>\r\n");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
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
