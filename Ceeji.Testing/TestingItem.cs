using Ceeji.Testing.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ceeji.Testing {
    /// <summary>
    /// 代表一个测试项目。
    /// </summary>
    public abstract class TestingItem {
        /// <summary>
        /// 执行测试，并返回测试结果。
        /// </summary>
        /// <param name="config"></param>
        public void Test(TestingData data) {
            var thread = new Thread(new ParameterizedThreadStart((obj) => {
                var watch = new Stopwatch();
                try {
                    var item = (obj as TestingItem);
                    watch.Start();
                    item.TestDelegate(data);
                    watch.Stop();
                }
                catch (ThreadAbortException ex) {
                    // 超时。
                    watch.Stop();
                    data.Exception = ex;
                    data.OK = false;
                    data.ErrorType = ErrorType.Timeout;
                }
                catch (Exception ex) {
                    // 测试引发了异常。
                    watch.Stop();
                    data.Exception = ex;
                    data.OK = false;
                    data.ErrorType = ErrorType.TestingOccurException;
                }
                finally {
                    data.EndTime = DateTime.Now;
                    data.TotalTime = new TimeSpan((long)(((double)watch.ElapsedTicks / Stopwatch.Frequency) * 1000 * 10000));
                    data.Completed = true;
                }
            }));
            data.StartTime = DateTime.Now;
            thread.Start(this);
            if (!thread.Join(data.Timeout)) {
                thread.Abort();
                thread.Join();
            }
        }


        /// <summary>
        /// 在派生类中，实现测试方法。
        /// </summary>
        /// <param name="result"></param>
        public abstract Action<TestingData> TestDelegate { get; }
    }
}
