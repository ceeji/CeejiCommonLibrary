using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ceeji.Caching {
    /// <summary>
    /// An redis-based caching provider, which is built on RedisBoost project
    /// </summary>
    public class RedisBoostCachingProvider : CachingProvider {
        #region 公开方法
        /// <summary>
        /// initalize a new instance of <see cref="Ceeji.Caching.RedisBoostCachingProvider"/>.
        /// </summary>
        public RedisBoostCachingProvider() {
        }

        /// <summary>
        /// initalize a new instance of <see cref="Ceeji.Caching.RedisBoostCachingProvider"/> with host property.
        /// </summary>
        public RedisBoostCachingProvider(string host) : this() {
            Host = host;

            OnRestoreEnvironment().Wait();
        }

        /// <summary>
        /// initalize a new instance of <see cref="Ceeji.Caching.RedisBoostCachingProvider"/> with host and port property.
        /// </summary>
        public RedisBoostCachingProvider(string host, int port) : this() {
            Host = host;
            Port = port;

            OnRestoreEnvironment().Wait();
        }

        /// <summary>
        /// initalize a new instance of <see cref="Ceeji.Caching.RedisBoostCachingProvider"/> with host and port property.
        /// </summary>
        public RedisBoostCachingProvider(string host, int port, int database) : this() {
            Host = host;
            Port = port;
            Database = database;

            OnRestoreEnvironment().Wait();
        }
        #endregion

        #region 公开属性
        /// <summary>
        /// Gets or sets the host address of redis server
        /// </summary>
        public string Host { get; set; } = null;
        /// <summary>
        /// Gets or sets the port of the redis server, default 6379
        /// </summary>
        public int Port { get; set; } = 6379;
        /// <summary>
        /// Gets or sets the database of the redis server, default 0
        /// </summary>
        public int Database { get; set; } = 0;

        #endregion

        #region protected members
        protected override Task OnIncrementAsync(string key) {
            using (rwLock.ReaderLock()) {
                return client.IncrAsync(key);
            }
        }

        protected override async Task OnRestoreEnvironment() {
            using (rwLock.WriterLock()) {
                try {
                    if (client != null) {
                        try {
                            await client.DisconnectAsync();
                        }
                        catch { }
                        try {
                            client.Dispose();
                        }
                        catch { }
                    }

                    client = await RedisBoost.RedisClient.ConnectAsync(Host, Port, Database);
                }
                catch { }
            }
        }

        private RedisBoost.IRedisClient client;
        /// <summary>
        /// A lock to make sure when the environment
        /// </summary>
        private Nito.AsyncEx.AsyncReaderWriterLock rwLock = new Nito.AsyncEx.AsyncReaderWriterLock();

        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: 释放托管状态(托管对象)。
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~CachingProvider() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public override void Dispose() {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }

        protected override async Task OnSetExpireTimeAsync(string key, TimeSpan interval) {
            using (rwLock.ReaderLock())
                await client.PExpireAsync(key, (int)interval.TotalMilliseconds);
        }

        protected override async Task OnSetAddAsync(string key, params string[] value) {
            using (rwLock.ReaderLock())
                await client.SAddAsync(key, value);
        }

        protected override async Task<bool> OnKeyExistsAsync(string key) {
            using (rwLock.ReaderLock()) {
                var ret = await client.ExistsAsync(key);
                return ret == 1;
            }
        }

        protected override async Task<long> OnSetLengthAsync(string key) {
            using (rwLock.ReaderLock())
                return await client.SCardAsync(key);
        }

        protected override async Task<IEnumerable<string>> OnSetMembersAsync(string key) {
            using (rwLock.ReaderLock()) {
                var ret = await client.SMembersAsync(key);

                return ret.Select(x => x.As<string>());
            }
        }

        protected override async Task<string> OnStringGetAsync(string key) {
            using (rwLock.ReaderLock()) {
                return (await client.GetAsync(key)).As<string>();
            }
        }

        protected override async Task<string[]> OnStringGetAsync(string[] key) {
            using (rwLock.ReaderLock()) {
                var ret = await client.MGetAsync(key);
                var arr = new string[ret.Length];

                for (var i = 0; i < ret.Length; ++i)
                    arr[i] = ret[i].As<string>();

                return arr;
            }
        }

        protected override async Task OnSetRemoveAsync(string key, string[] keys) {
            using (rwLock.ReaderLock()) {
                await client.SRemAsync(key, keys);
            }
        }

        protected override async Task<string[]> OnSetIntersectAsync(params string[] keys) {
            using (rwLock.ReaderLock()) {
                var ret = await client.SInterAsync(keys);
                var arr = new string[ret.Length];

                for (var i = 0; i < ret.Length; ++i)
                    arr[i] = ret[i].As<string>();

                return arr;
            }
        }

        protected override async Task OnSet(string key, string value) {
            using (rwLock.ReaderLock()) {
                await client.SetAsync(key, value);
            }
        }

        protected override async Task OnSet(string key, byte[] value) {
            using (rwLock.ReaderLock()) {
                await client.SetAsync(key, value);
            }
        }

        protected override async Task<bool> OnSet(string key, byte[] value, bool canReplace = true) {

            if (!canReplace) {
                if ((await KeyExistsAsync(key)))
                    return false;
            }

            await OnSet(key, value);
            return true;
        }

        protected override async Task<bool> OnSet(string key, string value, bool canReplace = true) {
            if (!canReplace) {
                if ((await KeyExistsAsync(key)))
                    return false;

            }

            await OnSet(key, value);
            return true;
        }

        protected async override Task OnKeyRemoveAsync(params string[] keys) {
            using (rwLock.ReaderLock()) {
                foreach (var key in keys) {
                    await client.DelAsync(key);
                }
            }
        }
        #endregion
    }
}