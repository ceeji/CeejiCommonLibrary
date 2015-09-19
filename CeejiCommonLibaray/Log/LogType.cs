using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Log {
    /// <summary>
    /// 代表日志类型。
    /// </summary>
    public enum LogType {
        Info = 2,
        Debug = 1,
        Warning = 4,
        /// <summary>
        /// 错误，一般是异常
        /// </summary>
        Error = 8,
        /// <summary>
        /// 致命错误，一般是未处理的异常
        /// </summary>
        Fatal = 16
    }
}
