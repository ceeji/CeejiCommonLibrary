using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.Caching {
    public class StackExchangeRedisCachingProvider : CachingProvider {
        private Lazy<StackExchange.Redis.ConnectionMultiplexer> mLconn;

        private IDatabase DB {
            get {
                return mLconn.Value.GetDatabase(0);
            }
        }

        #region 公开方法

        /// <summary>
        /// initalize a new instance of <see cref="Ceeji.Caching.RedisBoostCachingProvider"/> with host property.
        /// </summary>
        /// <param name="connection_string">the connection string, e.g. 127.0.0.1,abortConnect=false</param>
        public StackExchangeRedisCachingProvider(string connection_string) {
            mLconn = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connection_string), true);
            // mLconn.Value.Configure(new System.IO.StreamWriter("/srv/bzbx/redis-cacheProvider.log"));
            mLconn.Value.ConnectionFailed += Redis_ConnectionFailed;
            mLconn.Value.ConnectionRestored += Redis_ConnectionRestored;
            mLconn.Value.ErrorMessage += Value_ErrorMessage;
            mLconn.Value.InternalError += Value_InternalError;
        }

        private void Value_InternalError(object sender, InternalErrorEventArgs e) {
            Console.WriteLine("Redis_InternalErrorMsg: " + e.Exception + ";" + e.Origin);
        }

        private void Value_ErrorMessage(object sender, RedisErrorEventArgs e) {
            Console.WriteLine("Redis_ErrorMsg: " + e.Message);
        }

        private void Redis_ConnectionRestored(object sender, ConnectionFailedEventArgs e) {
            Console.WriteLine("Redis_ConnectionRestored: " + e.FailureType.ToString() + Environment.NewLine + e.Exception);
        }

        private void Redis_ConnectionFailed(object sender, ConnectionFailedEventArgs e) {
            Console.WriteLine("Redis_ConnectionFailed: " + e.FailureType.ToString() + Environment.NewLine + e.Exception);
        }

        class Profiler : IProfiler {
            public object GetContext() {
                return context;
            }

            public object context = new object();
        }

        private Profiler mProfiler = null;

        public void BeginProfile() {
            if (mProfiler == null) {
                mProfiler = new Profiler();
                mLconn.Value.RegisterProfiler(mProfiler);
            }
            mLconn.Value.BeginProfiling(mProfiler.context);
        }

        public void EndProfile(Stream streamToWrite) {
            var msgs = mLconn.Value.FinishProfiling(mProfiler.context);
            using (var tw = new StreamWriter(streamToWrite, Encoding.UTF8, 4096, true)) {

                foreach (var msg in msgs) {
                    tw.WriteLine($"[{msg.CommandCreated.ToString("yyyy-MM-dd HH:mm:ss.fff")}] COMMAND {msg.Command}");
                    tw.WriteLine($"    +{ msg.CreationToEnqueued.TotalMilliseconds}ms AddedToQueue");
                    tw.WriteLine($"    +{ msg.EnqueuedToSending.TotalMilliseconds}ms BeginSending");
                    tw.WriteLine($"    +{ msg.SentToResponse.TotalMilliseconds}ms BeginReceive");
                    tw.WriteLine($"    +{ msg.ResponseToCompletion.TotalMilliseconds}ms Finish");
                }

                tw.Flush();
            }
        }

        public override void Dispose() {
            mLconn.Value.Close(true);
        }

        /// <summary>
        /// This method is not supported
        /// </summary>
        /// <param name="key"></param>
        /// <param name="secondsToWait"></param>
        /// <returns></returns>
        protected override Task<string> OnBRightPopAsync(string key, int secondsToWait) {
            throw new NotSupportedException();
        }

        protected override Task OnIncrementAsync(string key) {
            return DB.StringIncrementAsync(key, 1, CommandFlags.DemandMaster);
        }

        protected override Task<bool> OnKeyExistsAsync(string key) {
            return DB.KeyExistsAsync(key, CommandFlags.PreferSlave);
        }

        protected override Task OnKeyRemoveAsync(params string[] key) {
            return DB.KeyDeleteAsync(key.Select(x => (RedisKey)x).ToArray(), CommandFlags.DemandMaster);
        }

        protected override Task OnListLeftPushAsync(string key, string val) {
            return DB.ListLeftPushAsync(key, val, flags: CommandFlags.DemandMaster);
        }

        protected override Task<long> OnListLengthAsync(string key) {
            return DB.ListLengthAsync(key, CommandFlags.PreferSlave);
        }

        protected override async Task<IList<string>> OnListRangeAsync(string key, int pos, int length) {
            return (await DB.ListRangeAsync(key, pos, pos + length - 1, CommandFlags.PreferSlave)).Select(x => x.ToString()).ToList();
        }

        protected override Task OnListRightPopAsync(string key) {
            DB.ListRightPop(key, CommandFlags.FireAndForget | CommandFlags.DemandMaster);

            return Task.Run(() => { });
        }

        protected override Task OnRestoreEnvironment() {
            return Task.Run(() => { });
        }

        protected override Task OnSet(string key, byte[] value) {
            return DB.StringSetAsync(key, value, flags: CommandFlags.DemandMaster);
        }

        protected override Task OnSet(string key, string value) {
            return DB.StringSetAsync(key, value, flags: CommandFlags.DemandMaster);
        }

        protected override Task<bool> OnSet(string key, byte[] value, bool canReplace) {
            return DB.StringSetAsync(key, value, when: canReplace ? When.Always : When.NotExists, flags: CommandFlags.DemandMaster);
        }

        protected override Task<bool> OnSet(string key, string value, bool canReplace) {
            return DB.StringSetAsync(key, value, when: canReplace ? When.Always : When.NotExists, flags:  CommandFlags.DemandMaster);
        }

        protected override Task OnSetAddAsync(string key, params string[] value) {
            return DB.SetAddAsync(key, value.Select(x => (RedisValue)x).ToArray(), CommandFlags.DemandMaster);
        }

        protected override Task OnSetExpireTimeAsync(string key, TimeSpan interval) {
            return DB.KeyExpireAsync(key, interval, CommandFlags.DemandMaster);
        }

        protected override async Task<string[]> OnSetIntersectAsync(params string[] keys) {
            return (await DB.SetCombineAsync(SetOperation.Intersect, keys.Select(k => (RedisKey)k).ToArray(), flags: CommandFlags.PreferSlave)).Select(x => x.ToString()).ToArray();
        }

        protected override Task<long> OnSetLengthAsync(string key) {
            return DB.SetLengthAsync(key, CommandFlags.PreferSlave);
        }

        protected override async Task<IEnumerable<string>> OnSetMembersAsync(string key) {
            return (await DB.SetMembersAsync(key, CommandFlags.PreferSlave)).Select(x => x.ToString());
        }

        protected override Task OnSetRemoveAsync(string key, string[] keys) {
            return DB.SetRemoveAsync(key, keys.Select(x => (RedisValue)x).ToArray(), CommandFlags.DemandMaster);
        }

        protected override async Task<string[]> OnStringGetAsync(string[] key) {
            return (await DB.StringGetAsync(key.Select(x => (RedisKey)x).ToArray(), CommandFlags.PreferSlave)).Select(x => x.ToString()).ToArray();
        }

        protected override async Task<string> OnStringGetAsync(string key) {
            return await DB.StringGetAsync(key, CommandFlags.PreferSlave);
        }

        protected override string OnStringGet(string key) {
            return DB.StringGet(key, CommandFlags.PreferSlave);
        }

        public override void ListTrimForget(string key, int start, int count) {
            DB.ListTrim(key, start, start + count - 1, CommandFlags.FireAndForget | CommandFlags.DemandMaster);
        }

        protected override Task<T> tryDoCommand<T>(Func<Task<T>> command) {
            return command();
        }
        #endregion
    }
}
