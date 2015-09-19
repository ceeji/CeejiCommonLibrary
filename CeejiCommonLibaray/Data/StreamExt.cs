using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;

namespace System {
    public static class StreamExt {
        /// <summary>
        /// 写指定的 buffer 的全部数据到流中。允许直接刷新缓冲区。
        /// </summary>
        /// <param name="s">要写入的流。</param>
        /// <param name="buffer">要写入的数据。</param>
        /// <param name="flush">是否在写入后刷新缓冲区。</param>
        public static void Write(this Stream s, byte[] buffer, bool flush = false) {
            try {
                s.Write(buffer, 0, buffer.Length);
                if (flush)
                    s.Flush();
            }
            catch { }
        }

        /// <summary>
        /// 写指定的字符串数据到流中。允许直接刷新缓冲区。
        /// </summary>
        /// <param name="content">要写入的流。</param>
        /// <param name="encode">字符编码，默认为 UTF-8。</param>
        /// <param name="flush">是否在写入后刷新缓冲区。</param>
        public static void WriteWithEncoding(this Stream s, string content, Encoding encode = null, bool flush = false) {
            s.Write((encode ?? Encoding.UTF8).GetBytes(content), flush);
        }

        /// <summary>
        /// 写指定的字符串数据到流中。允许直接刷新缓冲区。
        /// </summary>
        /// <param name="content">要写入的流。</param>
        /// <param name="flush">是否在写入后刷新缓冲区。</param>
        public static void Write(this Stream s, string content, bool flush = false) {
            s.Write(Encoding.UTF8.GetBytes(content), flush);
        }
    }
}
