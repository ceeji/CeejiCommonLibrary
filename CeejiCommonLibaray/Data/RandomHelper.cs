using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 提供对随机变量的支持。此类中的方法保证线程安全。
    /// </summary>
    public static class RandomHelper {
        /// <summary>
        /// 产生一个强完全随机的字符串。这种变量在加密中是安全的，和传统的随机数生成方法不同。
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string NextStrongRandomString(int length) {
            byte[] buffer = NextStrongRandomByteArray(length);
            var sb = new StringBuilder();

            for (var i = 0; i < length; ++i) {
                sb.Append(randomString[buffer[i] % randomString.Length]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 产生一个弱随机的字符串。这种变量在加密中是不安全的，但是速度比较快。
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string NextWeakRandomString(int length) {
            byte[] buffer = NextWeakRandomByteArray(length);
            var sb = new StringBuilder();

            for (var i = 0; i < length; ++i) {
                sb.Append(randomString[buffer[i] % randomString.Length]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 产生一个弱随机的字节数组。这种变量在加密中是不安全的，但是速度比较快。
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] NextWeakRandomByteArray(int length) {
            byte[] buffer = new byte[length];
            r.NextBytes(buffer);
            
            return buffer;
        }

        /// <summary>
        /// 产生一个强完全随机的字节数组。如果 allowZeroByte 为 false，产生的序列中不含有零。这种变量在加密中是安全的，和传统的随机数生成方法不同。
        /// </summary>
        /// <param name="length"></param>
        /// <param name="allowZeroByte">是否允许 0 字节。</param>
        /// <returns></returns>
        public static byte[] NextStrongRandomByteArray(int length, bool allowZeroByte = true) {
            if (rdGenerator == null) {
                rdGenerator = RNGCryptoServiceProvider.Create();
            }
            byte[] buffer = new byte[length];

            if (allowZeroByte)
                rdGenerator.GetBytes(buffer);
            else
                rdGenerator.GetNonZeroBytes(buffer);

            return buffer;
        }

        /// <summary>
        /// 产生一个随机的盐，盐的长度为 23 位。
        /// </summary>
        /// <returns></returns>
        public static string NewSalt() {
            return NextStrongRandomString(23);
        }

        /// <summary>
        /// 产生一个随机 session id，由 32 位随机字符串组成。
        /// </summary>
        /// <returns></returns>
        public static string NewSessionID() {
            return NextStrongRandomString(32);
        }

        /// <summary>
        /// 产生一个随机 session key，由 64 位随机字符串组成。
        /// </summary>
        /// <returns></returns>
        public static string NewSessionKey() {
            return NextStrongRandomString(64);
        }

        /// <summary>
        /// 产生非负随机数。
        /// </summary>
        /// <returns></returns>
        public static int NextRandomInt() {
            return r.Next();
        }

        /// <summary>
        /// 产生非负随机数。
        /// </summary>
        /// <param name="max">最大值，该值不会被取到。</param>
        /// <param name="min">最小值。</param>
        /// <returns></returns>
        public static int NextRandomInt(int min, int max) {
            return r.Next(min, max);
        }

        /// <summary>
        /// 产生非负随机数。
        /// </summary>
        /// <param name="max">最大值，该值不会被取到。</param>
        /// <param name="min">最小值。</param>
        /// <returns></returns>
        public static double NextRandomDouble() {
            return r.NextDouble();
        }

        [ThreadStatic]
        private static RandomNumberGenerator rdGenerator = null;
        private const string randomString = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static ThreadSafeRandom r = new ThreadSafeRandom();
    }

    /// <summary>
    /// 提供一个线程安全的 Random 实现。
    /// </summary>
    public class ThreadSafeRandom {
        private static readonly Random _global = new Random();
        [ThreadStatic]
        private static Random _local;

        /// <summary>
        /// 初始化 <see cref="ThreadSafeRandom"/> 的新实例。
        /// </summary>
        public ThreadSafeRandom() {
            createLocal();
        }
        /// <summary>
        /// 返回一个整数随机数。此方法是线程安全的。
        /// </summary>
        /// <returns></returns>
        public int Next() {
            createLocal();
            return _local.Next();
        }

        /// <summary>
        /// 返回指定范围内的随机数。
        /// </summary>
        /// <param name="min">最小值。</param>
        /// <param name="max">必须大于等于 min，且返回结果不会包含 max。</param>
        /// <returns></returns>
        public int Next(int min, int max) {
            createLocal();
            return _local.Next(min, max);
        }

        /// <summary>
        /// 返回 0和1 之间的随机数。
        /// </summary>
        /// <returns></returns>
        public double NextDouble() {
            createLocal();
            return _local.NextDouble();
        }

        /// <summary>
        /// 返回 0和1 之间的随机数。
        /// </summary>
        /// <returns></returns>
        public void NextBytes(byte[] buffer) {
            createLocal();
            _local.NextBytes(buffer);
        }

        private void createLocal() {
            if (_local == null) {
                int seed;
                lock (_global) {
                    seed = _global.Next();
                }
                _local = new Random(seed);
            }
        }
    }
}
