using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace Ceeji.Log {
    /// <summary>
    /// 一个基本的单文件日志记录器。
    /// </summary>
    public class SingleFileLogger : LogWriter {
        /// <summary>
        /// 创建 SingleFileLogger（一个基本的单文件日志记录器）。
        /// </summary>
        /// <param name="path"></param>
        private SingleFileLogger() {
        }

        /// <summary>
        /// 创建 SingleFileLogger（一个基本的单文件日志记录器）。
        /// </summary>
        /// <param name="path">日志文件的路径。</param>
        /// <param name="shareWithOthers">在日志使用过程中，是否将日志共享给其他使用者。如果开启，会降低性能。并且，所有使用者都必须打开此模式。</param>
        public SingleFileLogger(string path, bool shareWithOthers = false) {
            mPath = path;
            isShared = shareWithOthers;
        }

        protected override void OnWriteLog(DateTime time, string assembly, string runningClass, string runningMethod, LogType type, string msg, Exception exception) {
            if (!isPrepared)
                OnPrepare();

            var mWriter = getWriter();

            try {
                if (mWriter != null) {
                    var fprmattedMessage = GetFormattedLine(time, assembly, runningClass, runningMethod, type, msg, exception);

                    mWriter.WriteLine(fprmattedMessage);
                    mWriter.Flush();
                }
            }
            catch {
            }
            finally {
                if (isShared) {
                    mWriter.Close();
                }
            }
        }

        internal static string GetFormattedLine(DateTime time, string assembly, string runningClass, string runningMethod, LogType type, string msg, Exception exception) {
            try {
                assembly = assembly == null ? "" : assembly;
                runningClass = runningClass == null ? "" : runningClass;
                msg = msg == null ? "" : msg;

                var formattedTime = string.Format("{0}-{1}-{2} {3}:{4}:{5}:{6}", time.Year.ToString(), time.Month.ToString().PadLeft(2, '0'), time.Day.ToString().PadLeft(2, '0'), time.Hour.ToString().PadLeft(2, '0'), time.Minute.ToString().PadLeft(2, '0'), time.Second.ToString().PadLeft(2, '0'), time.Millisecond.ToString().PadLeft(3, '0'));
                var fprmattedMessage = string.Format("[{0,8}]\t[{1}]\t[{2}]\t[{3}]\t[{4}]\t{5}\t{6}", type.ToString(), formattedTime, assembly, runningClass, runningMethod, msg, exception == null ? "" : GetExceptionDetailMessage(exception));
                return fprmattedMessage;
            }
            catch {
                return "";
            }
        }

        private StreamWriter getWriter() {
            try {
                if (!isShared && this.mWriter != null) {
                    return this.mWriter;
                }

                var dir = mPath.Substring(0, mPath.Length - Path.GetFileName(mPath).Length);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var times = 0;
                do {
                    try {
                        mStream = File.Open(mPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                        mWriter = new StreamWriter(mStream);
                        return mWriter;
                    }
                    catch {
                        Thread.Sleep(100);
                        times++;
                    }
                }
                while (times < 12);

                return null;
            }
            catch {
                return null;
            }
        }

        protected override void OnPrepare() {
            if (isShared)
                return;

            try {
                if (isPrepared)
                    return;

                getWriter();

                if (mWriter != null)
                    isPrepared = true;
            }
            catch {
            }
        }

        /// <summary>
        /// 获取或设置文件路径。
        /// </summary>
        public string FilePath {
            get {
                return this.mPath;
            }
            set {
                if (this.mPath != null) {
                    throw new InvalidOperationException();
                }
                this.mPath = value;
            }
        }

        private string mPath;
        private FileStream mStream;
        private StreamWriter mWriter;
        private bool isPrepared = false;
        private bool isShared = false;
    }
}
