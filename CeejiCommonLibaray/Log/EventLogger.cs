/*************************************************************************
 *  [2014] - [2015] Wuhan LearningByDoing Co.,Ltd
 *  All Rights Reserved.
 * 
 *  Author: Ceeji Cheng <i@ceeji.net>
 *  If you have any quesions or suggestions about this code, send me mails.
 *************************************************************************
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ceeji.Log {
    /// <summary>
    /// 代表一个事件记录器。此记录器将记录下应用程序发生的错误。该线程和所有相关类、操作都是线程安全的。你也可以继承并实现新的 Ceeji.Log.LogWriter 个性化输出方式。
    /// </summary>
    public class EventLogger {
        /// <summary>
        /// 创建 Ceeji.EventLogger 的一个新实例。
        /// </summary>
        public EventLogger() {
            LogLevel = LogType.Debug;
        }

        /// <summary>
        /// 创建 Ceeji.EventLogger 的一个新实例。
        /// </summary>
        /// <param name="writers">要使用的输出方式。</param>
        public EventLogger(IEnumerable<LogWriter> writers) : this() {
            this.AddLogWriters(writers);
        }

        /// <summary>
        /// 获取所有的日志输出器。
        /// </summary>
        public LogWriter[] LogWriters {
            get {
                lock (mWriters) {
                    return mWriters.ToArray();
                }
            }
        }

        /// <summary>
        /// 添加一个日志记录器。
        /// </summary>
        /// <param name="writer"></param>
        public void AddLogWriter(LogWriter writer) {
            lock (mWriters) {
                mWriters.Add(writer);

                if (!writer.IsPrepared)
                    writer.Prepare();
            }
        }

        /// <summary>
        /// 添加一批日志记录器。
        /// </summary>
        /// <param name="writer"></param>
        public void AddLogWriters(IEnumerable<LogWriter> writers) {
            lock (mWriters) {
                mWriters.AddRange(writers);

                foreach (var writer in writers) {
                    if (!writer.IsPrepared)
                        writer.Prepare();
                }
            }
        }

        /// <summary>
        /// 删除一个日志记录器。如果本来就不存在，不会产生错误。
        /// </summary>
        public void RemoveWriter(LogWriter writer) {
            lock (mWriters) {
                mWriters.Remove(writer);
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Error(string message) {
            if (this.LogLevel > LogType.Error) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            Error(message, method.Name, method.DeclaringType.Name, Path.GetFileName(method.DeclaringType.Assembly.Location));
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="exception">要记录的消息。</param>
        public void Error(Exception exception, string msg = "") {
            if (this.LogLevel > LogType.Error) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Error(exception, msg, method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName);
            }
            else {
                Error(exception, msg, "", "", "");
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="exception">要记录的消息。</param>
        public void Fatal(Exception exception) {
            if (this.LogLevel > LogType.Fatal) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Fatal(exception, "", method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName);
            }
            else {
                Fatal(exception, "", "", "", "");
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Fatal(string message) {
            if (this.LogLevel > LogType.Fatal) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Fatal(null, message, method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName);
            }
            else {
                Fatal(null, message, "", "", "");
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Warning(string message) {
            if (this.LogLevel > LogType.Warning) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Warning(message, method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName);
            }
            else {
                Warning(message, "", "", "");
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Warning(Exception ex, string message = null) {
            if (this.LogLevel > LogType.Warning) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Warning(message ?? "", method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName, ex);
            }
            else {
                Warning(message ?? "", "", "", "", ex);
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Info(string message) {
            if (this.LogLevel > LogType.Info) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Info(message, method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName);
            }
            else {
                Info(message, "", "", "");
            }
        }

        /// <summary>
        /// 记录信息。此方法对性能有略微影响，一般情况下都可以使用。如果结合使用 LogFilter 功能，则不必担心循环中大量的 Debug 信息输出，则此方式记录日志可能是性价比最高、最好的方式。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Debug(string message) {
            if (this.LogLevel > LogType.Debug) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            if (method != null) {
                var assName = "";
                try {
                    assName = (method.DeclaringType == null ? "" : (Path.GetFileName(method.DeclaringType.Assembly.Location)));
                }
                catch { assName = ""; };
                Debug(message, method.Name ?? "", method.DeclaringType != null ? (method.DeclaringType.Name ?? "") : "", assName);
            }
            else {
                Debug(message, "", "", "");
            }
        }

        /// <summary>
        /// 记录日志。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        public void Log(LogType type, string message) {
            if (this.LogLevel > type) {
                return;
            }

            // 查找堆栈跟踪信息
            StackFrame stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            Log(type, message, method.Name, method.DeclaringType.Name, Path.GetFileName(method.DeclaringType.Assembly.Location));
        }

        /// <summary>
        /// 最低日志级别。低于此级别的将不被记录。
        /// </summary>
        public LogType LogLevel { get; set; }

        /// <summary>
        /// 记录信息。请在 method 参数提供 MethodInfo.GetCurrentMethod(). 此方法在中等性能要求的程序中使用。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="method">提供 MethodInfo.GetCurrentMethod().</param>
        public void Debug(string message, MethodInfo method) {
            Debug(message, method.Name, method.DeclaringType.Name, Path.GetFileName(method.DeclaringType.Assembly.Location));
        }

        /// <summary>
        /// 记录信息。请在 method 参数提供 MethodInfo.GetCurrentMethod(). 此方法在中等性能要求的程序中使用。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="method">提供 MethodInfo.GetCurrentMethod().</param>
        public void Warning(string message, MethodInfo method) {
            Warning(message, method.Name, method.DeclaringType.Name, Path.GetFileName(method.DeclaringType.Assembly.Location));
        }

        /// <summary>
        /// 记录信息。请在 method 参数提供 MethodInfo.GetCurrentMethod(). 此方法在中等性能要求的程序中使用。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="method">提供 MethodInfo.GetCurrentMethod().</param>
        public void Error(string message, MethodInfo method) {
            Error(message, method.Name, method.DeclaringType.Name, Path.GetFileName(method.DeclaringType.Assembly.Location));
        }

        /// <summary>
        /// 记录信息。请在 method 参数提供 MethodInfo.GetCurrentMethod(). 此方法在中等性能要求的程序中使用。
        /// </summary>
        /// <param name="message">要记录的消息。</param>
        /// <param name="method">提供 MethodInfo.GetCurrentMethod().</param>
        public void Error(Exception ex, MethodInfo method) {
            Error(ex, method.Name, method.DeclaringType.Name, Path.GetFileName(method.DeclaringType.Assembly.Location));
        }

        /// <summary>
        /// 记录信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Info(string message, string method = null, string classes = null, string assmblyName = null) {
            writeLog(DateTime.Now, classes, method, LogType.Info, message, null, assmblyName);
        }

        /// <summary>
        /// 记录调试信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message, string method = null, string classes = null, string assmblyName = null) {
            writeLog(DateTime.Now, classes, method, LogType.Debug, message, null, assmblyName);
        }

        /// <summary>
        /// 记录警告信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Warning(string message, string method = null, string classes = null, string assmblyName = null, Exception ex = null) {
            writeLog(DateTime.Now, classes, method, LogType.Warning, message, ex, assmblyName);
        }

        /// <summary>
        /// 记录日志信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Log(LogType type, string message, string method = null, string classes = null, string assmblyName = null) {
            writeLog(DateTime.Now, classes, method, type, message, null, assmblyName);
        }

        /// <summary>
        /// 记录错误信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Error(string message, string method = null, string classes = null, string assmblyName = null) {
            writeLog(DateTime.Now, classes, method, LogType.Error, message, null, assmblyName);
        }

        /// <summary>
        /// 记录错误信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Fatal(Exception exception, string message, string method = null, string classes = null, string assmblyName = null) {
            writeLog(DateTime.Now, classes, method, LogType.Fatal, message ?? "发生致命错误，程序即将退出。", exception, assmblyName);
        }

        /// <summary>
        /// 记录错误信息。此方法在极端重视性能的情况下使用。
        /// </summary>
        /// <param name="message"></param>
        public void Error(Exception ex, string msg, string method, string classes, string assmblyName = null) {
            writeLog(DateTime.Now, classes, method, LogType.Error, msg ?? "发生异常。", ex, assmblyName);
        }

        private void writeLog(DateTime time, string runningClass, string runningMethod, LogType type, string msg, Exception exception, string assmblyName) {
            lock (mWriters) {
                foreach (var writer in mWriters) {
                    writer.WriteLog(time, assmblyName, runningClass, runningMethod, type, msg, exception);   
                }

                // System.Diagnostics.Debug.WriteLine(SingleFileLogger.GetFormattedLine(time, assmblyName, runningClass, runningMethod, type, msg, exception));
            }
        }

        private List<LogWriter> mWriters = new List<LogWriter>();
    }
}
