using Ceeji.Testing.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ceeji.Testing {
    /// <summary>
    /// 代表一个测试器，用来使用指定的配置执行自动化测试，支持并行测试。
    /// </summary>
    public class Tester {
        /// <summary>
        /// 创建 <see cref="Ceeji.Testing.Tester"/>，代表一个测试器，用来使用指定的配置执行自动化测试，支持并行测试。
        /// </summary>
        public Tester() {
        }

        /// <summary>
        /// 获取或设置测试要使用的配置。
        /// </summary>
        public TestingConfiguration Config {
            get {
                return this.mConfig;
            }
            set {
                if (this.Status == TestingStatus.Running)
                    throw new InvalidOperationException("不能在运行测试时修改测试配置。");

                this.mConfig = value;
            }
        }

        /// <summary>
        /// 代表测试状态。
        /// </summary>
        public TestingStatus Status {
            get {
                return this.mStatus;
            }
            private set {
                this.mStatus = value;
                this.OnTestingStatusChanged();
            }
        }

        private void OnTestingItemEnd(TestingData data) {
            if (this.TestingItemEnd != null)
                this.TestingItemEnd(this, data);
        }

        private void OnTestingItemBegin(TestingData e) {
            if (this.TestingItemBegin != null)
                this.TestingItemBegin(this, e);
        }

        private void OnTestingStatusChanged() {
            if (this.TestingStatusChanged != null)
                this.TestingStatusChanged(this, new EventArgs());

            if (this.Status == TestingStatus.Stop && mResetEventForEnd != null) {
                mResetEventForEnd.Set();
                mResetEventForEnd = null;
            }
        }

        /// <summary>
        /// 进行测试。
        /// </summary>
        /// <param name="groupName">要进行测试的测试组名。</param>
        /// <param name="keep">指示只执行一次测试，还是持续进行测试。</param>
        public void Test(string groupName, bool keep) {
            mResetEventForEnd = new ManualResetEventSlim(false);
            BeginTest(groupName, keep);
            mResetEventForEnd.Wait();
        }

        /// <summary>
        /// 开始测试。
        /// </summary>
        /// <param name="groupName">要进行测试的测试组名。</param>
        /// <param name="keep">指示只执行一次测试，还是持续进行测试。</param>
        public void BeginTest(string groupName, bool keep) {
            // 开始进行测试
            if (this.Config == null)
                throw new ArgumentNullException("Config");

            var group = this.Config[ConfigKeys.TestingGroup] as TestingGroup;
            if (group == null)
                throw new ArgumentException("配置项缺乏 ConfigKeys.TestingGroup");

            this.Status = TestingStatus.Running;
            ThreadPool.QueueUserWorkItem(status => {
                TestingConfiguration input = new TestingConfiguration();
                foreach (var point in group.Points) {
                    for (var i = 0; i < point.Times; ++i) {
                        var item = Activator.CreateInstance(Type.GetType(point.Type)) as TestingItem;
                        var data = new TestingData() {
                            Config = this.Config,
                            InputData = input != null ? new TestingConfiguration(point.Config.Concat(input).ToList()) : point.Config,
                            Point = point,
                            Time = i,
                            Timeout = new TimeSpan(point.Timeout * 10000),
                            StartTime = DateTime.Now
                        };
                        this.OnTestingItemBegin(data);
                        item.Test(data);
                        this.OnTestingItemEnd(data);

                        if (data.OutputData != null) {
                            input.AddRange(data.OutputData);
                        }
                    }
                }

                this.Status = TestingStatus.Stop;
            });
        }

        private ManualResetEventSlim mResetEventForEnd;

        /// <summary>
        /// 当测试状态改变时发生。
        /// </summary>
        public event EventHandler TestingStatusChanged;
        /// <summary>
        /// 当测试点开始时发生。
        /// </summary>
        public event EventHandler<TestingData> TestingItemBegin;
        /// <summary>
        /// 当测试点结束时发生。
        /// </summary>
        public event EventHandler<TestingData> TestingItemEnd;
        private TestingStatus mStatus = TestingStatus.Stop;
        private TestingConfiguration mConfig;
    }

    /// <summary>
    /// 代表测试状态。
    /// </summary>
    public enum TestingStatus {
        /// <summary>
        /// 测试正在运行
        /// </summary>
        Running,
        /// <summary>
        /// 测试已经停止
        /// </summary>
        Stop
    }
}
