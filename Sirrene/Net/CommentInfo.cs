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

namespace Sirrene.Net
{
    public class CommentInfo
    {
        public string Sdir { get; set; }
        public string Sfile { get; set; }
        public string Sfolder { get; set; }
        public int Seq { get; set; }
        public string SaveFile { get; set; }
        public string Sqlite3File { get; set; }
        public string Ext { get { return ".xml"; } }
        public string Xml { get { return ".xml"; } }
        public string Ext2 { get; set; }

        //同名ファイル名がないかチェック
        private static bool IsExistFile(string file, int seq, string ext1, string ext2)
        {
            var fn1 = file + seq.ToString() + ext1;
            var fn2 = file + seq.ToString() + ext2;

            return (!File.Exists(fn1) && !File.Exists(fn2)) ? false : true;
        }

        //Sqlite3用の保存ファイル名
        public static string GetSaveFileSqlite3(CommentInfo cmi)
        {
            return Path.Combine(cmi.Sdir, cmi.Sfile);
        }

        //Sqlite3の保存ファイルにシーケンスNoをつける
        public static string GetSaveFileSqlite3Num(CommentInfo cmi, string ext = null)
        {
            int idx = cmi.Sqlite3File.IndexOf(".sqlite3");
            if (idx < 0) return null;

            var ext2 = cmi.Ext;
            if (ext2 != null) ext2 = ext;
            var ff = cmi.Sqlite3File.Substring(0, idx) + "-";

            //同名ファイル名がないかチェック
            while (IsExistFile(ff, cmi.Seq, ext2, cmi.Xml)) ++cmi.Seq;

            return ff + cmi.Seq.ToString();
        }

        //保存ディレクトリーがなければ作る
        public static bool MakeRecDir(CommentInfo cmi)
        {
            var result = false;

            var s = Path.Combine(cmi.Sdir, cmi.Sfolder);
            if (!Directory.Exists(s))
            {
                //フォルダー作成
                Directory.CreateDirectory(s);
                result = true;
            }
            else
            {
                result = true;
            }
            return result;
        }
    }


    public class CommentControl
    {
        public int status { set; get; }
        public string _waybackkey { set; get; }
        public long _when { set; get; }                    //when
        public long _last_res { set; get; }                //last_res
        public List<List<string>> _come_list { set; get; }
        public List<string> _come_text { set; get; }

        public CommentControl()
        {
            status = 0;
            _waybackkey = null;
            _last_res = 0L;
            _come_list = new List<List<string>>();
            _come_text = new List<string>();
        }
    }

}
