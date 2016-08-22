using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ceeji {
    /// <summary>
    /// 用于帮助进行参数检查的类。
    /// </summary>
    public static class ThrowHelper {
        /// <summary>
        /// 如果参数为空，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowNull(params object[] arguments) {
            for (var i = 0; i < arguments.Length; i += 2) {
                if (arguments[i + 1] == null)
                    throw new ArgumentNullException((string)arguments[i], $"value of {arguments[i]} can not be null");
            }
        }

        /// <summary>
        /// 如果参数不符合范围要求，则抛出异常。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min">最小值（含)</param>
        /// <param name="max">最大值（含）</param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckRange(string name, long value, long min = long.MinValue, long max = long.MaxValue) {
            if (value < min || value > max) {
                throw new ArgumentException($"value of {name} exceed its range, min = {min}, max = {max}, val = {value}", name);
            }
        }

        /// <summary>
        /// 如果参数不符合范围要求，则抛出异常。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min">最小值（含)</param>
        /// <param name="max">最大值（含）</param>int
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]

        public static void CheckRange(string name, int value, int min = int.MinValue, int max = int.MaxValue) {
            if (value < min || value > max) {
                throw new ArgumentException($"value of {name} exceed its range, min = {min}, max = {max}, val = {value}", name);
            }
        }

        /// <summary>
        /// 如果参数不在指定的值域中，则抛出异常。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="range"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]

        public static void InRange<T>(string name, T value, IEnumerable<T> range) where T : IEquatable<T> {
            ThrowHelper.ThrowNull(nameof(range), range, nameof(name), name);

            foreach (var item in range) {
                if (item.Equals(value)) return;
            }

            throw new ArgumentOutOfRangeException(name, $"value of {name} out of range, value = {value}");
        }

        /// <summary>
        /// 如果参数为空，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowNull<T>(string name, T value) where T : class {
            if (value == null) {
                throw new ArgumentNullException(name, $"value of {name} can not be null");
            }
        }

        /// <summary>
        /// 如果参数为 null 或空字符串，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowEmpty(string name, string value) {
            if (value == null || value == string.Empty) {
                throw new ArgumentException($"value of {name} must not be null or empty", name);
            }
        }

        /// <summary>
        /// 如果参数为 null 或空字符串，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowEmpty(string name1, string value1, string name2, string value2) {
            if (value1 == null || value1 == string.Empty) {
                throw new ArgumentException("value must not be null or empty", name1);
            }

            if (value2 == null || value2 == string.Empty) {
                throw new ArgumentException($"value of {name2} must not be null or empty", name2);
            }
        }

        /// <summary>
        /// 如果参数为 null 或空字符串，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        public static void ThrowEmpty(string name1, string value1, string name2, string value2, string name3, string value3) {
            if (value1 == null || value1 == string.Empty) {
                throw new ArgumentException($"value of {name1} must not be null or empty", name1);
            }

            if (value2 == null || value2 == string.Empty) {
                throw new ArgumentException($"value of {name2} must not be null or empty", name2);
            }

            if (value3 == null || value3 == string.Empty) {
                throw new ArgumentException($"value of {name3} must not be null or empty", name3);
            }
        }

        /// <summary>
        /// 如果参数为 null 或空字符串，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowEmpty(params string[] arguments) {
            for (var i = 0; i < arguments.Length; i += 2) {
                if (arguments[i + 1] == null || arguments[i + 1] == string.Empty)
                    throw new ArgumentException($"value of {arguments[i]} must not be null or empty", (string)arguments[i]);
            }
        }

        /// <summary>
        /// 如果参数为空，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowNull<T1, T2>(string name1, T1 value1, string name2, T2 value2) where T1 : class where T2 : class {
            if (value1 == null) {
                throw new ArgumentNullException(name1, $"value of {name1} can not be null");
            }
            if (value2 == null) {
                throw new ArgumentNullException(name2, $"value of {name2} can not be null");
            }
        }

        /// <summary>
        /// 如果参数为空，则抛出异常。
        /// </summary>
        /// <param name="arguments"></param>
        public static void ThrowNull<T1, T2, T3>(string name1, T1 value1, string name2, T2 value2, string name3, T3 value3) where T1 : class where T2 : class where T3 : class {

            if (value1 == null) {
                throw new ArgumentNullException(name1, $"value of {name1} can not be null");
            }
            if (value2 == null) {
                throw new ArgumentNullException(name2, $"value of {name2} can not be null");
            }
            if (value3 == null) {
                throw new ArgumentNullException(name3, $"value of {name3} can not be null");
            }
        }
    }
}
