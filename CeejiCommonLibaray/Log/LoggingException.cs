using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Log {
    /// <summary>
    /// 代表在日志中发生的错误。
    /// </summary>
    public class LoggingException : Exception {
        /// <summary>
        /// 创建 LoggingException 的新实例。
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="innerException"></param>
        public LoggingException(string msg, Exception innerException = null) : base(msg, innerException) {
        }
    }
}
