using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 代表一种压缩算法。
    /// </summary>
    public abstract class CompressionAlgorithm {
        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <param name="outputBuffer">将转换写入的输出。</param>
        /// <param name="outputOffset">输出字节数组中的偏移量，从该位置开始写入数据。</param>
        /// <returns>计算所得的转换的字节数。</returns>
        public abstract int CompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset);
        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <returns>计算所得的转换。</returns>
        public abstract byte[] CompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount);
        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <param name="outputBuffer">将转换写入的输出。</param>
        /// <param name="outputOffset">输出字节数组中的偏移量，从该位置开始写入数据。</param>
        /// <returns>计算所得的转换的字节数。</returns>
        public abstract int DecompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset);
        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <returns>计算所得的转换。</returns>
        public abstract byte[] DecompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount);
    }
}
