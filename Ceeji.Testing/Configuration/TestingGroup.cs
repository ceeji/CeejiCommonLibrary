using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.Testing.Configuration {
    /// <summary>
    /// 代表一个测试组。在一个测试组中，前一个测试的输出将被作为后一个测试的输入，用于连贯测试。只有所有的测试都成功了，测试才是真的成功。
    /// </summary>
    public class TestingGroup {
        /// <summary>
        /// 创建 Ceeji.Testing.Configuration.TestingGroup 的新实例。
        /// </summary>
        public TestingGroup() {
            Points = new List<TestingPoint>();
        }

        /// <summary>
        /// 代表所有测试点。
        /// </summary>
        public List<TestingPoint> Points { get; set; }
        /// <summary>
        /// 代表在自动化测试中，每次运行测试所间隔的秒数。
        /// </summary>
        public int IntervalSeconds { get; set; }
    }

    /// <summary>
    /// 代表一个测试点。一个测试点由测试类型、参数、次数组成。
    /// </summary>
    public class TestingPoint {
        public TestingPoint() {
            Config = new TestingConfiguration();
        }

        /// <summary>
        /// 测试类型的完全限定名，必须是 TestingItem 的子类。
        /// </summary>
        public string Type {
            get {
                return mType.AssemblyQualifiedName;
            }
            set {
                var t = System.Type.GetType(value);
                if (!t.IsSubclassOf(typeof(TestingItem))) {
                    throw new ArgumentException("参数必须是 TestingItem 的子类");
                }

                this.mType = t;
            }
        }
        /// <summary>
        /// 代表测试配置。
        /// </summary>
        public TestingConfiguration Config { get; set; }
        /// <summary>
        /// 代表测试点的名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 代表每一次运行测试点时，要进行的测试的次数。
        /// </summary>
        public int Times { get; set; }
        /// <summary>
        /// 代表每一次运行测试时的超时时间（毫秒）。超时后，将直接视为失败。
        /// </summary>
        public long Timeout { get; set; }

        private Type mType;
    }
}
