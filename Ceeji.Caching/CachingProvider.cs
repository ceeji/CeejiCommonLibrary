using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.Caching {
    /// <summary>
    /// An abstract class that represents a kind of thread-safe chaching provider which supports redis-like operation. You should create a class that derived from this class for using
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

        public Task<IEnumerable<string>> SetMembersAsync(string key) {
            return tryDoCommand(() => OnSetMembersAsync(key));
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
        /// Get the right value of a list, and block if it is empty
        /// </summary>
        /// <param name="key"></param>
        /// <param name="secondsToWait">time to wait, in seconds</param>
        /// <returns></returns>
        public Task<string> BRightPopAsync(string key, int secondsToWait = 30) {
            return tryDoCommand(() => OnBRightPopAsync(key, secondsToWait));
        }

        protected abstract Task<string> OnBRightPopAsync(string key, int secondsToWait);

        public Task<long> SetLengthAsync(string key) {
            return tryDoCommand(() => OnSetLengthAsync(key));
        }

        /// <summary>
        /// Check if a key is exists in the database
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task<bool> KeyExistsAsync(string key) {
            return tryDoCommand(() => OnKeyExistsAsync(key));
        }

        /// <summary>
        /// Gets the value of a key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task<string> StringGetAsync(string key) {
            return tryDoCommand(() => OnStringGetAsync(key));
        }

        /// <summary>
        /// Gets the value of many key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task<string[]> StringGetAsync(params string[] key) {
            return tryDoCommand(() => OnStringGetAsync(key));
        }

        /// <summary>
        /// Gets the value of many key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string[] StringGet(params string[] key) {
            string[] ret = null;
            Task.Run(async () => { ret = await tryDoCommand(() => OnStringGetAsync(key)); }).Wait();
            return ret;
        }

        public Task<string[]> SetIntersectAsync(params string[] key) {
            return tryDoCommand(() => OnSetIntersectAsync(key));
        }

        /// <summary>
        /// Gets the value of a key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string StringGet(string key) {
            return OnStringGet(key);
        }

        protected virtual string OnStringGet(string key) {
            string ret = null;
            Task.Run(async () => { ret = await tryDoCommand(() => OnStringGetAsync(key)); }).Wait();
            return ret;
        }

        public Task SetRemoveAsync(string key, params string[] keys) {
            return tryDoCommand(() => OnSetRemoveAsync(key, keys));
        }

        public Task SetAsync(string key, string val) {
            return tryDoCommand(() => OnSet(key, val));
        }

        public Task SetAsync(string key, byte[] val) {
            return tryDoCommand(() => OnSet(key, val));
        }

        public Task SetAsync(string key, string val, bool canReplace) {
            return tryDoCommand(() => OnSet(key, val, canReplace ));
        }

        public abstract void ListTrimForget(string key, int start, int count);

        public Task SetAsync(string key, byte[] val, bool canReplace) {
            return tryDoCommand(() => OnSet(key, val, canReplace));
        }

        public Task KeyRemoveAsync(params string[] keys) {
            return tryDoCommand(() => OnKeyRemoveAsync(keys));
        }

        public async Task<bool> SetAsync(string key, string val, bool canReplace, TimeSpan? timeout) {
            if (!(await tryDoCommand(() => OnSet(key, val, canReplace))))
                return false;

            if (timeout.HasValue) {
                await tryDoCommand(() => OnSetExpireTimeAsync(key, timeout.Value));
            }
            return true;
        }

        public Task ListLeftPushAsync(string key, string val) {
            return tryDoCommand(() => OnListLeftPushAsync(key, val));
        }

        public Task ListRightPopAsync(string key) {
            return tryDoCommand(() => OnListRightPopAsync(key));
        }

        public async Task<int> ListLengthAsync(string key) {
            return (int)(await tryDoCommand(() => OnListLengthAsync(key)));
        }

        public int ListLength(string key) {
            int val = -1;
            Task.Run(async () => {
                val = (int)(await tryDoCommand(() => OnListLengthAsync(key)));
            }).Wait();

            return val; 
        }

        public async Task<IList<string>> ListRangeAsync(string key, int pos, int length) {
            return (await tryDoCommand(() => OnListRangeAsync(key, pos, length)));
        }

        public IList<string> ListRange(string key, int pos, int length) {
            IList<string> val = null;

            Task.Run(async () => {
                val = (await tryDoCommand(() => OnListRangeAsync(key, pos, length)));
            }).Wait();

            return val;
        }

        public async Task<bool> SetAsync(string key, byte[] val, bool canReplace, TimeSpan? timeout) {
            if (!(await tryDoCommand(() => OnSet(key, val, canReplace))))
                return false;

            if (timeout.HasValue) {
                await tryDoCommand(() => OnSetExpireTimeAsync(key, timeout.Value));
            }
            return true;
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
        protected abstract Task<IEnumerable<string>> OnSetMembersAsync(string key);
        protected abstract Task OnSetRemoveAsync(string key, string[] keys);

        protected abstract Task<string> OnStringGetAsync(string key);
        protected abstract Task<string[]> OnStringGetAsync(string[] key);

        protected abstract Task<long> OnSetLengthAsync(string key);
        protected abstract Task<long> OnListLengthAsync(string key);
        protected abstract Task<bool> OnKeyExistsAsync(string key);

        protected abstract Task OnSet(string key, string value);
        protected abstract Task OnSet(string key, byte[] value);

        protected abstract Task<bool> OnSet(string key, string value, bool canReplace);
        protected abstract Task<bool> OnSet(string key, byte[] value, bool canReplace);

        protected abstract Task OnListLeftPushAsync(string key, string val);
        protected abstract Task OnListRightPopAsync(string key);
        protected abstract Task OnKeyRemoveAsync(params string[] key);

        protected abstract Task<string[]> OnSetIntersectAsync(params string[] keys);
        protected abstract Task<IList<string>> OnListRangeAsync(string key, int pos, int length);

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
                    return;
                }
                catch (Exception ex) {
                    // 如果出错，记录一个特殊的日志
                    try {
                        File.AppendAllText("cachingProvider.debug.log", "[" + DateTime.Now + "] " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
                    }
                    catch { }

                    if (now == -1)
                        now = Environment.TickCount;

                    if (Environment.TickCount - now > RetryTimeout.TotalMilliseconds)
                        throw new TimeoutException();

                    // restore
                    try {
                        await OnRestoreEnvironment();
                    }
                    catch (Exception exx) {
                        // 如果出错，记录一个特殊的日志
                        try {
                            File.AppendAllText("cachingProvider.debug.log", "[" + DateTime.Now + "] " + exx.Message + Environment.NewLine + exx.StackTrace + Environment.NewLine);
                        }
                        catch { }
                    }
                }
            }
        }

        protected virtual Task<T> tryDoCommand<T>(Func<Task<T>> command) {
            var now = -1;

            while (true) {
                try {
                    return command();
                }
                catch {
                    if (now == -1)
                        now = Environment.TickCount;

                    if (Environment.TickCount - now > RetryTimeout.TotalMilliseconds)
                        throw new TimeoutException();

                    // restore
                    try {
                        OnRestoreEnvironment().Wait();
                    }
                    catch { }
                }
            }
        }
        #endregion
    }
}
