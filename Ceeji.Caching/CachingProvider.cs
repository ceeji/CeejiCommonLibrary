using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.Caching {
    /// <summary>
    /// An abstract class that represents a kind of thread-safe chaching provider. You should create a class that derived from this class for using
    /// </summary>
    /// <remarks>
    /// This class will try to finish all things and retry forever. So, if a command failed, it will wait for some time and try again and again
    /// </remarks>
    public abstract class CachingProvider : IDisposable {

        #region Public members
        /// <summary>
        /// Increases values by 1 
        /// </summary>
        /// <param name="key">The key to increase, whose value must be integer</param>
        /// <returns></returns>
        public Task IncrementAsync(string key) {
            return tryDoCommand(() => OnIncrementAsync(key));
        }

        /// <summary>
        /// Set expire time of a key
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="time">The TimeSpan from now</param>
        /// <returns></returns>
        public Task SetExpireTimeAsync(string key, TimeSpan time) {
            return tryDoCommand(() => OnSetExpireTimeAsync(key, time));
        }

        /// <summary>
        /// Add one or more values to a set
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Task SetAddAsync(string key, params string[] value) {
            return tryDoCommand(() => OnSetAddAsync(key, value));
        }

        /// <summary>
        /// Interval between command retries
        /// </summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(500);
        /// <summary>
        /// Timeout for command retries
        /// </summary>
        public TimeSpan RetryTimeout { get; set; } = TimeSpan.FromMilliseconds(10000);

        /// <summary>
        /// 释放此接口使用的一切资源。
        /// </summary>
        public abstract void Dispose();

        #endregion

        #region Members for override
        /// <summary>
        /// Increment a key in the derived class
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected abstract Task OnIncrementAsync(string key);
        protected abstract Task OnSetExpireTimeAsync(string key, TimeSpan interval);
        protected abstract Task OnSetAddAsync(string key, params string[] value);
        protected abstract Task<bool> KeyExistsAsync(string key);
        /// <summary>
        /// Try to restore connection and others in derived class
        /// </summary>
        /// <returns></returns>
        protected abstract Task OnRestoreEnvironment();
        #endregion

        #region private members
        private async Task tryDoCommand(Func<Task> command) {
            var now = -1;

            while (true) {
                try {
                    await command();
                }
                catch {
                    if (now == -1)
                        now = Environment.TickCount;

                    if (Environment.TickCount - now > RetryTimeout.TotalMilliseconds)
                        throw new TimeoutException();

                    // restore
                    try {
                        await OnRestoreEnvironment();
                    }
                    catch { }
                }
            }
        }
        #endregion
    }
}
