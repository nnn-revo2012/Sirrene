﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

using Sirrene.Net;
using Sirrene.Rec;

namespace Sirrene.Proc
{

    public class ExecProcess : AEexecProcess, IDisposable
    {

        private bool disposedValue = false; // 重複する呼び出しを検知するには

        private Process _ps = null;

        //Debug
        public bool IsDebug { get; set; }

        public ExecProcess(Form1 fo, DataJson djs, RetryInfo ri)
        {
            IsDebug = false;

            PsStatus = -1;
            this._djs = djs;
            this._ri = ri;
            this._form = fo;
        }

        ~ExecProcess()
        {
            this.Dispose();
        }

        public override void ExecPs(string exefile, string argument)
        {
            try
            {
                if (Form1.props.IsVideo)
                {
                    //ファイルの実行(ps)
                    Process _process1 = new Process();
                    _ps = _process1;
                    _ps.StartInfo.FileName = exefile;
                    _ps.StartInfo.Arguments = argument;

                    _ps.StartInfo.UseShellExecute = false;
                    _ps.StartInfo.CreateNoWindow = true;

                    // 標準出力を受信する
                    _ps.StartInfo.RedirectStandardOutput = true;
                    _ps.StartInfo.RedirectStandardError = true;
                    _ps.OutputDataReceived += ReceivedPs;
                    _ps.ErrorDataReceived += ReceivedErrorPs;

                    // 標準入力
                    _ps.StartInfo.RedirectStandardInput = true;

                    // プロセスが終了したときに Exited イベントを発生させる
                    _ps.EnableRaisingEvents = true;
                    // Windows フォームのコンポーネントを設定して、コンポーネントが作成されているスレッドと
                    // 同じスレッドで Exited イベントを処理するメソッドが呼び出されるようにする
                    _ps.SynchronizingObject = _form;
                    // プロセス終了時に呼び出される Exited イベントハンドラの設定
                    _ps.Exited += ExitedPs;

                    _ps.Start();
                    _ps.Refresh();
                    _ps.PriorityClass = ProcessPriorityClass.BelowNormal;

                    //中断ボタンに変更
                    _form.AddLog(string.Format("実行ファイル: {0}", _ps.StartInfo.FileName), 9);
                    _form.AddLog(string.Format("パラメーター: {0}", _ps.StartInfo.Arguments), 9);
                    _form.AddLog("プロセス実行中です。", 1);

                }
                PsStatus = 0; //実行中
                //EnableButton(false);

                if (Form1.props.IsVideo)
                {
                    _ps.BeginOutputReadLine();
                    _ps.BeginErrorReadLine();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExecPs), Ex);
            }
        }


        private void ReceivedPs(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string text = e.Data + "\r\n";
                    _form.AddExecLog(text);
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReceivedPs), Ex);
            }
        }

        private void ReceivedErrorPs(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    string text = e.Data + "\r\n";
                    _form.AddExecLog(text);
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ReceivedErrorPs), Ex);
            }
        }

        // プロセスの終了を捕捉する Exited イベントハンドラ
        private void ExitedPs(object sender, EventArgs e)
        {
            try
            {
                var proc = (Process)sender;

                _form.AddLog(string.Format("プロセス終了しました。コード: {0} ", proc.ExitCode), 1);
                PsStatus = (proc.ExitCode == 0) ? 1 : 2; //1:正常終了 2:異常終了
                //EnableButton(true);
                _ps.CancelOutputRead(); // 使い終わったら止める
                _ps.CancelErrorRead();
                if (_ps.HasExited)
                {
                    _ps.Dispose();
                    _ps = null;
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(ExitedPs), Ex);
            }
        }

        public void InputProcess(byte[] data)
        {
            try
            {
                if (_ps != null && data.Length > 0)
                {
                    _ps.StandardInput.BaseStream.Write(data, 0, data.Length);
                    _ps.StandardInput.Flush();
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(InputProcess), Ex);
            }
        }

        public void StopInput()
        {
            try
            {
                if (_ps != null)
                {
                    if (_ps.StandardInput != null)
                    {
                        _ps.StandardInput.Flush();
                        _ps.StandardInput.Close();
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugWrite.Writeln(nameof(StopInput), Ex);
            }
        }

        public override void BreakProcess(string breakkey)
        {
            if (_ps != null && !_ps.HasExited)
            {
                if (string.IsNullOrEmpty(breakkey))
                {
                    SendCtrlC(_ps);
                }
                else
                {
                    if (_ps.StandardInput != null)
                    {
                        _ps.StandardInput.WriteLine(breakkey);
                        _ps.StandardInput.Flush();
                    }
                }
                if (_ps != null)
                {
                    _ps.StandardInput.Close();
                }
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    _ps?.Dispose();
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
