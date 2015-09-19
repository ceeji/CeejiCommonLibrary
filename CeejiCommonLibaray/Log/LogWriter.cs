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
using System.Linq;
using System.Text;

namespace Ceeji.Log {
    /// <summary>
    /// 代表日志输出。此类是线程安全的。
    /// </summary>
    public abstract class LogWriter {
        /// <summary>
        /// 准备日志输出。如果不运行此方法，不允许进行日志输出。如果多次运行，则不会报错。
        /// </summary>
        internal void Prepare() {
            lock (lockObj) {
                if (mIsPrepared) {
                    return;
                }

                try {
                    this.OnPrepare();
                }
                catch (Exception ex) {
                    throw new LoggingException("LogWriter 发生了初始化异常。", ex);
                }

                mIsPrepared = true;
            }
        }

        /// <summary>
        /// 写日志。此方法保证不发生异常。
        /// </summary>
        /// <param name="time">事件发生的时间。</param>
        /// <param name="runningClass">事件所属的类。</param>
        /// <param name="runningMethod">事件所属的方法。</param>
        /// <param name="type">日志类型。</param>
        internal void WriteLog(DateTime time, string assembly, string runningClass, string runningMethod, LogType type, string msg, Exception exception) {
            try {
                lock (lockObj) {
                    OnWriteLog(time, assembly, runningClass, runningMethod, type, msg, exception);
                }
            }
            catch {
            }
        }

        /// <summary>
        /// 写日志。此方法的异常会被忽略。此方法保证不会并发调用。
        /// </summary>
        /// <param name="time"></param>
        /// <param name="runningClass"></param>
        /// <param name="runningMethod"></param>
        /// <param name="type"></param>
        /// <param name="msg"></param>
        /// <param name="exception"></param>
        protected abstract void OnWriteLog(DateTime time, string assembly, string runningClass, string runningMethod, LogType type, string msg, Exception exception);

        /// <summary>
        /// 指示是否已经准备好进行日志输出。
        /// </summary>
        internal bool IsPrepared {
            get {
                return this.mIsPrepared;
            }
        }

        /// <summary>
        /// 获取异常的详细信息。
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string GetExceptionDetailMessage(Exception ex) {
            if (ex == null)
                return "";
            return "==== Exception Descreption ====" + Environment.NewLine + ex.GetType().FullName + ": " + ex.Message + Environment.NewLine + "<< StackTrace >>" + Environment.NewLine + ex.StackTrace + Environment.NewLine + GetExceptionDetailMessage(ex.InnerException);
        }

        /// <summary>
        /// 在派生类中重写，实现准备日志。此方法保证只调用一次。如果准备失败，请抛出异常。
        /// </summary>
        protected abstract void OnPrepare();

        private bool mIsPrepared = false;
        private object lockObj = new object();
    }
}
