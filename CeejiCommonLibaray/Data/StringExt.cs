using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 提供对 System.String 类型的扩展方法。
    /// </summary>
    public static class StringExt {
        /// <summary>
        /// 获取一个新字符串，其中不包含 UTF-8 的 BOM 字符。
        /// </summary>
        /// <returns></returns>
        public static string GetStringWihoutBOM(this string source) {
            byte[] sourceBinary = Encoding.UTF8.GetBytes(source);

            if (sourceBinary.Length < 3)
                return source;

            if (sourceBinary[0] == _byteOrderMarkUtf8[0] && sourceBinary[1] == _byteOrderMarkUtf8[1] && sourceBinary[2] == _byteOrderMarkUtf8[2])
                return Encoding.UTF8.GetString(sourceBinary.Skip(3).ToArray());

            return source;
        }

        /// <summary>
        /// 判断指定的对象，如果对象是 null 或零长度列表，则为 true，否则为 false。
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsEmptyOrNull(this string source) {
            return source == null || source == "";
        }

        /// <summary>
        /// 判断指定的对象，如果对象是 null 或零长度列表，则为 true，否则为 false。
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsEmptyOrNull(this IList source) {
            return source == null || source.Count == 0;
        }

        private readonly static byte[] _byteOrderMarkUtf8 = Encoding.UTF8.GetPreamble();
    }
}
