﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Sirrene.Net;
using Sirrene.Rec;

namespace Sirrene.Proc
{
    public abstract class AEexecProcess
    {
        public volatile int PsStatus = -1; //実行ファイルの状態

        protected NicoDb _ndb = null;
        protected DataJson _djs = null;
        protected RetryInfo _ri = null;
        protected Form1 _form = null;

        public abstract void ExecPs(string exefile, string argument);
        public abstract void BreakProcess(string breakkey);
        public void SendCtrlC(Process ps)
        {
            if (AttachConsole((uint)ps.Id))
            {
                SetConsoleCtrlHandler(null, true);
                try
                {
                    if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                        return;
                }
                finally
                {
                    FreeConsole();
                    SetConsoleCtrlHandler(null, false);
                }
            }
        }

        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);
    }
}
