using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Ceeji.Testing.Configuration {
    /// <summary>
    /// 代表测试时所使用的配置。
    /// </summary>
    public class TestingConfiguration : Ceeji.Configuration {
        /// <summary>
        /// 代表测试时所使用的配置。
        /// </summary>
        public TestingConfiguration() {
        }

        /// <summary>
        /// 代表测试时所使用的配置。
        /// </summary>
        public TestingConfiguration(List<ConfigItemPair<string, object>> source)
            : base(source) {
        }
    }

    /// <summary>
    /// 代表预定义的测试配置键名。
    /// </summary>
    public static class ConfigKeys {
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public const string DbConnectionString = "DbConnectionString";
        /// <summary>
        /// 代表 API 域名
        /// </summary>
        public const string APIHost = "APIHost";
        /// <summary>
        /// 代表 API 前缀
        /// </summary>
        public const string APIPrefix = "APIPrefix";
        /// <summary>
        /// 代表测试组
        /// </summary>
        public const string TestingGroup = "TestingGroup";
    }
}
