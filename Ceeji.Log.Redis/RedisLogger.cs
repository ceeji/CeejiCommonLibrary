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
        public RedisLogger(string redisServerAddress, int database, int maxCount = 40000, string pathForLog = null) {
            if (redisServerAddress == null || redisServerAddress == string.Empty || database < 0)
                throw new ArgumentException("输入参数不符合要求");

            this.RedisServerAddress = redisServerAddress;
            this.Database = database;
            this.mPathForLog = pathForLog;
            this.mMaxCount = maxCount;

            Console.WriteLine($"Starting RedisLogger: {RedisServerAddress}:6379, database {database}");
        }

        public static string JsonSerialize(RedisLog obj) {
            try {
                /*if (isWindows)
                    return Jil.JSON.Serialize(obj, options);
                else*/
                    return JsonConvert.SerializeObject(obj);
            }
            catch {
                return JsonConvert.SerializeObject(obj);
            }
        }

        public static RedisLog JsonDeserialize(string input, out bool success) {
            success = true;

            try {
                /*if (isWindows) {
                    var log = Jil.JSON.Deserialize<RedisLog>(input, options);
                    log.time = log.time.ToLocalTime();
                    return log;
                }*/

                return JsonConvert.DeserializeObject<RedisLog>(input);
            }
            catch (Exception ex) {
                // 解析失败，直接返回
                success = false;
                return new RedisLog();
            }
        }

        // private static Jil.Options options;
        public static bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        protected override async void OnWriteLog(DateTime time, string assembly, string runningClass, string runningMethod, LogType type, string msg, Exception exception) {
            try {
                var obj = new RedisLog() {
                    time = time,
                    assembly = assembly,
                    obj = runningClass,
                    method = runningMethod,
                    type = type.ToString(),
                    msg = msg,
                    exception = GetExceptionDetailMessage(exception)
                };

                var json = JsonSerialize(obj);

                provider.ListLeftPushAsync("fastlog", json);
                provider.ListTrimForget("fastlog", 0, mMaxCount);
            }
            catch (Exception ex) {
                mLogWriter?.WriteLine(GetExceptionDetailMessage(ex));
                mLogWriter?.Flush();
                this.OnPrepare();
            }
        }

        protected override void OnPrepare() {
            try {
                provider = new Caching.StackExchangeRedisCachingProvider(RedisServerAddress + ",abortConnect=false");

                if (isWindows) {
                    //options = new Jil.Options(false, true, false, Jil.DateTimeFormat.MicrosoftStyleMillisecondsSinceUnixEpoch, true, Jil.UnspecifiedDateTimeKindBehavior.IsUTC);
                }
            }
            catch { }
            // = new Caching.RedisBoostCachingProvider(RedisServerAddress);
            //mMultiplexer = ConnectionMultiplexer.Connect(RedisServerAddress + ",abortConnect=false", mLogWriter);
        }

        /// <summary>
        /// 以倒序的顺序返回指定范围内的日志记录。
        /// </summary>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<RedisLog> GetLogs(int start, int count) {
            var ret = provider.ListRange("fastlog", start, count);

            bool success;

            foreach (var item in ret) {
                var log = JsonDeserialize(item.ToString(), out success);
                if (success)
                    yield return log;
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

        public struct RedisLog {
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
