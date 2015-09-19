using Ceeji.Testing.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.Testing {
    /// <summary>
    /// 代表一次测试的数据和结果。
    /// </summary>
    public class TestingData {
        public TestingData() {
            EndTime = DateTime.MinValue;
            Completed = false;
            ErrorType = Testing.ErrorType.NoError;
            Exception = null;
            InputData = new TestingConfiguration();
            OutputData = new TestingConfiguration();
        }

        private Guid id = Guid.NewGuid();

        /// <summary>
        /// 指示测试已经完成，并且成功。
        /// </summary>
        public void SetOK() {
            this.OK = true;
            this.ErrorType = Testing.ErrorType.NoError;
            this.ErrorDescription = null;
        }

        /// <summary>
        /// 指示测试已经完成，并且失败。
        /// </summary>
        public void SetFailure(string desc) {
            this.OK = false;
            this.ErrorType = Testing.ErrorType.ResultFailure;
            this.ErrorDescription = desc;
        }

        /// <summary>
        /// 指示测试已经完成，并且出错。
        /// </summary>
        public void SetException(Exception ex, string desc = null) {
            this.OK = false;
            this.ErrorType = Testing.ErrorType.TestingOccurException;
            this.ErrorDescription = desc;
            this.Exception = ex;
        }

        /// <summary>
        /// 测试的开始时间。
        /// </summary>
        public DateTime StartTime { get; set; }
        /// <summary>
        /// 测试的结束时间。
        /// </summary>
        public DateTime EndTime {
            get {
                return this.mEndTime;
            }
            set {
                this.mEndTime = value;
            }
        }
        /// <summary>
        /// 测试的总用时。
        /// </summary>
        public TimeSpan TotalTime { get; set; }
        /// <summary>
        /// 指示测试是否已经完成。
        /// </summary>
        public bool Completed { get; set; }
        /// <summary>
        /// 指示测试是否成功。
        /// </summary>
        public bool OK { get; set; }
        /// <summary>
        /// 指示测试所用的输入数据。
        /// </summary>
        public TestingConfiguration InputData { get; set; }
        /// <summary>
        /// 指示测试生成的输出数据。
        /// </summary>
        public TestingConfiguration OutputData { get; set; }
        /// <summary>
        /// 指示测试配置。
        /// </summary>
        public TestingConfiguration Config { get; set;  }
        /// <summary>
        /// 错误类型。
        /// </summary>
        public ErrorType ErrorType { get; set; }
        /// <summary>
        /// 存储在测试过程中引发的异常。
        /// </summary>
        public Exception Exception { get; set; }
        /// <summary>
        /// 存储错误的具体描述。
        /// </summary>
        public string ErrorDescription { get; set; }
        /// <summary>
        /// 标示所关联的测试点。
        /// </summary>
        public TestingPoint Point { get; set; }
        /// <summary>
        /// 代表这是第几次测试 (从 0 开始)。
        /// </summary>
        public int Time { get; set; }
        /// <summary>
        /// 指定测试的超时时间。
        /// </summary>
        public TimeSpan Timeout { get; set; }

        private DateTime mEndTime;
    }

    public enum ErrorType {
        /// <summary>
        /// 没有错误
        /// </summary>
        NoError,
        /// <summary>
        /// 测试方法引发了异常
        /// </summary>
        TestingOccurException,
        /// <summary>
        /// 测试结果失败
        /// </summary>
        ResultFailure,
        /// <summary>
        /// 超时
        /// </summary>
        Timeout
    }
}
