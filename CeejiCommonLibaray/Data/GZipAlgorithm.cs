using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 代表 GZip 压缩格式。
    /// </summary>
    public class GZipAlgorithm : CompressionAlgorithm {
        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <param name="outputBuffer">将转换写入的输出。</param>
        /// <param name="outputOffset">输出字节数组中的偏移量，从该位置开始写入数据。</param>
        /// <returns>计算所得的转换的字节数。</returns>
        public override int CompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            using (var ms = new MemoryStream(outputBuffer, outputOffset, outputBuffer.Length - outputOffset, true)) {
                using (var outStream = new GZipStream(ms, CompressionMode.Compress)) {
                    outStream.Write(inputBuffer, inputOffset, inputCount);
                    outStream.Flush();
                }
                return (int)ms.Position;
            }
        }

        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <returns>计算所得的转换。</returns>
        public override byte[] CompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            using (var ms = new MemoryStream()) {
                using (var outStream = new GZipStream(ms, CompressionMode.Compress)) {
                    outStream.Write(inputBuffer, inputOffset, inputCount);
                    outStream.Flush();
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <param name="outputBuffer">将转换写入的输出。</param>
        /// <param name="outputOffset">输出字节数组中的偏移量，从该位置开始写入数据。</param>
        /// <returns>计算所得的转换的字节数。</returns>
        public override int DecompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            using (var ms = new MemoryStream(inputBuffer, inputOffset, inputCount)) {
                using (var outStream = new GZipStream(ms, CompressionMode.Decompress)) {
                    var pos = outputOffset;
                    var lastRead = 0;
                    while ((lastRead = outStream.Read(outputBuffer, pos, Math.Min(bufferSize, outputBuffer.Length - pos))) != 0) {
                        pos += lastRead;
                    }
                    return pos - outputOffset;
                }
            }
        }

        private const int bufferSize = 1024 * 1024;

        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <returns>计算所得的转换。</returns>
        public override byte[] DecompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            using (var ms = new MemoryStream(inputBuffer, inputOffset, inputCount)) {
                using (var outStream = new GZipStream(ms, CompressionMode.Decompress)) {
                    return BinaryPackage.CPKPackage.readFully(outStream);
                }
            }
        }
    }

    /// <summary>
    /// 代表 Deflate 压缩算法。
    /// </summary>
    public class DeflateAlgorithm : CompressionAlgorithm {
        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <param name="outputBuffer">将转换写入的输出。</param>
        /// <param name="outputOffset">输出字节数组中的偏移量，从该位置开始写入数据。</param>
        /// <returns>计算所得的转换的字节数。</returns>
        public override int CompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            using (var ms = new MemoryStream(outputBuffer, outputOffset, outputBuffer.Length - outputOffset, true)) {
                using (var outStream = new DeflateStream(ms, CompressionMode.Compress)) {
                    outStream.Write(inputBuffer, inputOffset, inputCount);
                    outStream.Flush();
                }
                return (int)ms.Position;
            }
        }

        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <returns>计算所得的转换。</returns>
        public override byte[] CompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            using (var ms = new MemoryStream()) {
                using (var outStream = new DeflateStream(ms, CompressionMode.Compress)) {
                    outStream.Write(inputBuffer, inputOffset, inputCount);
                    outStream.Flush();
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <param name="outputBuffer">将转换写入的输出。</param>
        /// <param name="outputOffset">输出字节数组中的偏移量，从该位置开始写入数据。</param>
        /// <returns>计算所得的转换的字节数。</returns>
        public override int DecompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            using (var ms = new MemoryStream(inputBuffer, inputOffset, inputCount)) {
                using (var outStream = new DeflateStream(ms, CompressionMode.Decompress)) {
                    var pos = outputOffset;
                    var lastRead = 0;
                    while ((lastRead = outStream.Read(outputBuffer, pos, Math.Min(bufferSize, outputBuffer.Length - pos))) != 0) {
                        pos += lastRead;
                    }
                    return pos - outputOffset;
                }
            }
        }

        private const int bufferSize = 1024 * 1024;

        /// <summary>
        /// 转换指定字节数组的指定区域。
        /// </summary>
        /// <param name="inputBuffer">要为其计算转换的输入。</param>
        /// <param name="inputOffset">字节数组中的偏移量，从该位置开始使用数据。</param>
        /// <param name="inputCount">字节数组中用作数据的字节数。</param>
        /// <returns>计算所得的转换。</returns>
        public override byte[] DecompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            using (var ms = new MemoryStream(inputBuffer, inputOffset, inputCount)) {
                using (var outStream = new DeflateStream(ms, CompressionMode.Decompress)) {
                    return BinaryPackage.CPKPackage.readFully(outStream);
                }
            }
        }
    }
}
