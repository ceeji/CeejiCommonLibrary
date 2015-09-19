using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 提供对随机变量的支持。此类中的方法目前不保证线程安全。
    /// </summary>
    public class RandomHelper {
        public RandomHelper() {
            rr = new Random();
        }

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

            for (var i = 0; i < buffer.Length; ++i)
                buffer[i] = (byte)NextRandomInt(0, 256);

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
            if (r == null) {
                r = new Random();
            }

            return r.Next();
        }

        /// <summary>
        /// 产生非负随机数。
        /// </summary>
        /// <param name="max">最大值，该值不会被取到。</param>
        /// <param name="min">最小值。</param>
        /// <returns></returns>
        public static int NextRandomInt(int min, int max) {
            if (r == null) {
                r = new Random();
            }

            return r.Next(min, max);
        }

        /// <summary>
        /// 产生非负随机数。
        /// </summary>
        /// <returns></returns>
        public int RandomInt() {
            return rr.Next();
        }

        /// <summary>
        /// 产生非负随机数。
        /// </summary>
        /// <param name="max">最大值，该值不会被取到。</param>
        /// <param name="min">最小值。</param>
        /// <returns></returns>
        public int RandomInt(int min, int max) {
            return rr.Next(min, max);
        }

        private static RandomNumberGenerator rdGenerator = null;
        private const string randomString = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static Random r = null;
        private Random rr;
    }
}
