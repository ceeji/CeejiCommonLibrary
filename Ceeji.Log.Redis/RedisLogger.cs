using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using StackExchange.Redis;

namespace Ceeji.Log.Redis
{
    /// <summary>
    /// 代表将日志写入 基于 Redis 的 FastLog 的日志记录器。
    /// </summary>
    public class RedisLogger : LogWriter
    {
        /// <summary>
        /// 使用指定的 redisServerAddress、database 记录日志。
        /// </summary>
        /// <param name="redisServerAddress">Redis 服务器的地址。</param>
        /// <param name="database">数据库索引。</param>
        public RedisLogger(string redisServerAddress, int database, int maxCount = 20000, string pathForLog = null) {
            if (redisServerAddress == null || redisServerAddress == string.Empty || database < 0)
                throw new ArgumentException("输入参数不符合要求");

            this.RedisServerAddress = redisServerAddress;
            this.Database = database;
            this.mPathForLog = pathForLog;
            this.mMaxCount = maxCount;
        }
        protected override void OnWriteLog(DateTime time, string assembly, string runningClass, string runningMethod, LogType type, string msg, Exception exception) {
            try {
                var obj = new {
                    time = time,
                    assembly = assembly,
                    obj = runningClass,
                    method = runningMethod,
                    type = type.ToString(),
                    msg = msg,
                    exception = LogWriter.GetExceptionDetailMessage(exception)
                };

                var json = JsonConvert.SerializeObject(obj);
                provider.ListLeftPushAsync("fastlog", json);
                var count = provider.ListLength("fastlog") - mMaxCount;
                for (var i = 0; i < count; ++i) {
                    provider.ListRightPopAsync("fastlog");
                }
            }
            catch (Exception ex) {
                mLogWriter.WriteLine(LogWriter.GetExceptionDetailMessage(ex)); mLogWriter.Flush();
                this.OnPrepare();
            }
        }

        protected override void OnPrepare() {
            provider = new Caching.RedisBoostCachingProvider(RedisServerAddress);
            //mMultiplexer = ConnectionMultiplexer.Connect(RedisServerAddress + ",abortConnect=false", mLogWriter);
        }

        /// <summary>
        /// 以倒序的顺序返回指定范围内的日志记录。
        /// </summary>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<RedisLog> GetLogs(int start, int count) {
            var ret = provider.ListRange("fastlog", start, start + count - 1);

            foreach (var item in ret) {
                yield return JsonConvert.DeserializeObject<RedisLog>(item.ToString());
            }
        }

        /// <summary>
        /// 获取或设置 Redis 服务器地址。
        /// </summary>
        public string RedisServerAddress { get; set; }
        /// <summary>
        /// 获取或设置 Redis 服务器的位置。
        /// </summary>
        public int Database { get; set; }

        public class RedisLog {
            public DateTime time { get; set; }
            public string exception { get; set; }
            public string assembly { get; set; }
            public string obj { get; set; }
            public string method { get; set; }
            public string type { get; set; }
            public string msg { get; set; }
        }

        //private ConnectionMultiplexer mMultiplexer;
        private Caching.CachingProvider provider;
        private Newtonsoft.Json.JsonSerializer mSerializer = new Newtonsoft.Json.JsonSerializer();
        private string mPathForLog;
        private int mMaxCount = 40000;
        private StreamWriter mLogWriter;
    }
}
