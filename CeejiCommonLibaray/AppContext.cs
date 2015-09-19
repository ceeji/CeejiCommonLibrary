using Ceeji.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ceeji {
    /// <summary>
    /// 与运行环境有关的一些数据的静态方法。
    /// </summary>
    public static class AppContext {
        /// <summary>
        /// 获取当前程序的运行路径。此路径以 \ 结尾。
        /// </summary>
        public static string BasePath {
            get {
                var dir = AppDomain.CurrentDomain.BaseDirectory;

                if (dir.EndsWith(directorySeparator.ToString(), StringComparison.Ordinal)) {
                    return dir;
                }
                else {
                    return dir + directorySeparator;
                }
            }
        }

        /// <summary>
        /// 获取当前正在执行的【可执行文件】的文件名。
        /// </summary>
        public static string CurrentExecutingFileName {
            get {
                return Process.GetCurrentProcess().MainModule.FileName;
            }
        }

        /// <summary>
        /// 重新启动当前文件。如果等待时间大于 0，则该文件在等待期间并不会被占用，可以被更新。
        /// </summary>
        public static void Restart(int waitTimeInSeconds = 0) {
            if (waitTimeInSeconds == 0) {
                Process.Start(new ProcessStartInfo() { FileName = CurrentExecutingFileName });
            }
            else {
                var cmd = "ping 127.0.0.1 -n " + (waitTimeInSeconds + 1).ToString() + " > nul\r\nstart \"\" \"" + CurrentExecutingFileName + "\"";
                var path = GetTempFileName(".bat");

                File.WriteAllText(path, cmd, Encoding.Default);
                Process.Start(new ProcessStartInfo() { FileName = "cmd", Arguments = "/C \"" + path + "\"", UseShellExecute = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// 创建临时文件，此文件拥有指定的文件扩展名，并保证不和其他磁盘文件冲突。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.IO.IOException">当在内置的尝试次数内无法找到不存在的临时文件名或磁盘不可写时抛出此错误。</exception>
        public static string GetTempFileName(string ext = ".tmp") {
            if (ext == null) throw new ArgumentNullException(nameof(ext));

            var tmpPath = System.IO.Path.GetTempPath();
            var time = 0;
            var max_time = 15;
            do {
                if (max_time <= time) break;
                time++;

                try {
                    ext = ext.StartsWith(".") ? ext : "." + ext;
                    string fileName = tmpPath + RandomHelper.NextWeakRandomString(8) + ext;

                    using (File.Open(fileName, FileMode.CreateNew, FileAccess.ReadWrite)) {
                        return fileName;
                    }
                }
                catch { }
            }
            while (true);

            throw new IOException($"磁盘上无法创建任何新的临时文件（在 {max_time} 次尝试之内。）");
        }

        /// <summary>
        /// 使用特定的 ID 检测是否具有相同 ID 的程序正在运行。
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool HasAnotherIntance(string id) {
            bool mutexCreated = false;
            mutex = new System.Threading.Mutex(true, @"Local\ceeji_" + HashHelper.MD5(id).Substring(0, 10), out mutexCreated);

            if (!mutexCreated) {
                return true;
            }

            return false;
        }

        private static System.Threading.Mutex mutex;
        private static readonly char directorySeparator = System.IO.Path.DirectorySeparatorChar;
    }
}
