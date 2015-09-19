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
            return client.IncrAsync(key);
        }

        protected override async Task OnRestoreEnvironment() {
            rwLock.EnterWriteLock();

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
            finally {
                rwLock.ExitWriteLock();
            }
        }

        private RedisBoost.IRedisClient client;
        /// <summary>
        /// A lock to make sure when the environment
        /// </summary>
        private ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

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

        protected override Task OnSetExpireTimeAsync(string key, TimeSpan interval) {
            return client.PExpireAsync(key, (int)interval.TotalMilliseconds);
        }

        protected override Task OnSetAddAsync(string key, params string[] value) {
            return client.SAddAsync(key, value);
        }

        protected override Task<bool> KeyExistsAsync(string key) {
            return client.ExistsAsync(key);
        }
        #endregion
    }
}
