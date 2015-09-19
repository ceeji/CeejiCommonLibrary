using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 提供对流的压缩和解压缩的支持。
    /// </summary>
    public static class Compressor {
        static Compressor() {
            ChunkSize = 1024 * 1024; // 默认块大小为 1 MB
            EmptyByteArray = new Delayed<byte[]>(() => new byte[0], DelayThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// 压缩数据流。
        /// </summary>
        /// <param name="algorithm">要使用的压缩算法。CompressionAlgorithms 类中提供了常用的压缩算法。</param>
        /// <param name="inputBuffer">指定输入缓冲区。</param>
        /// <param name="inputOffset">指定输入缓冲区的开始位置。</param>
        /// <param name="inputCount">指定输入缓冲区的长度。</param>
        /// <param name="outputBuffer">指定输出缓冲区。</param>
        /// <param name="outputOffset">指定输出缓冲区的开始位置。</param>
        /// <returns>返回压缩后的字节数。</returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static int Compression(CompressionAlgorithm algorithm, CompressionMode mode, byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            if (algorithm == null || inputBuffer == null || outputBuffer == null) throw new ArgumentNullException();

            if (mode == CompressionMode.Compress)
                return algorithm.CompressBlock(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
            else
                return algorithm.DecompressBlock(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset);
        }

        /// <summary>
        /// 压缩数据流。
        /// </summary>
        /// <param name="algorithm">要使用的压缩算法。CompressionAlgorithms 类中提供了常用的压缩算法。</param>
        /// <param name="inputBuffer">指定输入缓冲区。</param>
        /// <param name="inputOffset">指定输入缓冲区的开始位置。</param>
        /// <param name="inputCount">指定输入缓冲区的长度。</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static byte[] Compression(CompressionAlgorithm algorithm, CompressionMode mode, byte[] inputBuffer, int inputOffset, int inputCount) {
            if (algorithm == null || inputBuffer == null) throw new ArgumentNullException();

            if (mode == CompressionMode.Compress)
                return algorithm.CompressFinalBlock(inputBuffer, inputOffset, inputCount);
            else
                return algorithm.DecompressFinalBlock(inputBuffer, inputOffset, inputCount);
        }

        /// <summary>
        /// 压缩数据流。
        /// </summary>
        /// <param name="algorithm">要使用的压缩算法。CompressionAlgorithms 类中提供了常用的压缩算法。</param>
        /// <param name="inputBuffer">指定输入缓冲区。</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static byte[] Compression(CompressionAlgorithm algorithm, CompressionMode mode, byte[] inputBuffer) {
            if (algorithm == null || inputBuffer == null) throw new ArgumentNullException();

            if (mode == CompressionMode.Compress)
                return algorithm.CompressFinalBlock(inputBuffer, 0, inputBuffer.Length);
            else
                return algorithm.DecompressFinalBlock(inputBuffer, 0, inputBuffer.Length);
        }

        /// <summary>
        /// 压缩数据流。
        /// </summary>
        /// <param name="algorithm">要使用的压缩算法。CompressionAlgorithms 类中提供了常用的压缩算法。</param>
        /// <param name="inputStream">指定输入流。</param>
        /// <param name="inputCount">输入流的长度，或 -1 以代表读至流的末尾。</param>
        /// <param name="outputStream">指定输出流。</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static void Compression(CompressionAlgorithm algorithm, CompressionMode mode, Stream inputStream, int inputCount, Stream outputStream) {
            if (algorithm == null || inputStream == null || outputStream == null) throw new ArgumentNullException();
            if (inputCount == 0)
                return;

            var countRead = 0;
            var lastTimeRead = 0;
            var inputBuffer = new byte[Compressor.ChunkSize];
            do {
                lastTimeRead = inputStream.Read(inputBuffer, 0, inputCount == -1 ? Compressor.ChunkSize : Math.Min(Compressor.ChunkSize, (inputCount - countRead)));
                if (lastTimeRead != 0) {
                    countRead += lastTimeRead;
                    if (mode == CompressionMode.Compress) {
                        var ret = algorithm.CompressFinalBlock(inputBuffer, 0, lastTimeRead);
                        outputStream.Write(ret, 0, ret.Length);
                    }
                    else {
                        var ret = algorithm.DecompressFinalBlock(inputBuffer, 0, lastTimeRead);
                        outputStream.Write(ret, 0, ret.Length);
                    }
                }
            }
            while ((countRead < inputCount && inputCount != -1) || (inputCount == -1 && lastTimeRead != 0));
        }

        /// <summary>
        /// 压缩数据流。
        /// </summary>
        /// <param name="algorithm">要使用的压缩算法。CompressionAlgorithms 类中提供了常用的压缩算法。</param>
        /// <param name="inputStream">指定输入流。</param>
        /// <param name="outputStream">指定输出流。</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static void Compression(CompressionAlgorithm algorithm, CompressionMode mode, Stream inputStream, Stream outputStream) {
            Compression(algorithm, mode, inputStream, -1, outputStream);
        }

        /// <summary>
        /// 压缩数据流。
        /// </summary>
        /// <param name="algorithm">要使用的压缩算法。CompressionAlgorithms 类中提供了常用的压缩算法。</param>
        /// <param name="inputStream">指定输入流。</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static byte[] Compression(CompressionAlgorithm algorithm, CompressionMode mode, Stream inputStream) {
            var ms = new MemoryStream();
            Compression(algorithm, mode, inputStream, -1, ms);

            return ms.ToArray();
        }

        /// <summary>
        /// 获取或设置压缩、解压缩时使用的每块的最大大小。
        /// </summary>
        public static int ChunkSize { get; set; }

        private static Delayed<byte[]> EmptyByteArray;
    }

    /// <summary>
    /// 提供常用的压缩算法。
    /// </summary>
    public static class CompressionAlgorithms {
        /// <summary>
        /// 获取 GZip 压缩算法，它是 Deflate 算法增加 CRC32 校验等文件头的文件格式。
        /// </summary>
        public static readonly Delayed<CompressionAlgorithm> GZip;

        /// <summary>
        /// 获取 Deflate 压缩算法，它的特点是压缩率很高，解压、压缩较慢。
        /// </summary>
        public static readonly Delayed<CompressionAlgorithm> Deflate;

        /// <summary>
        /// 获取 LZ4 压缩算法，它的特点是压缩率很低，解压、压缩极快。
        /// </summary>
        public static readonly Delayed<CompressionAlgorithm> LZ4;

        /// <summary>
        /// 获取 LZ4 HC 压缩算法，它的特点是压缩率较低（略低于 Deflate），解压极快，压缩较慢（略快于 Deflate）。
        /// </summary>
        public static readonly Delayed<CompressionAlgorithm> LZ4HC;

        static CompressionAlgorithms() {
            GZip = new Delayed<CompressionAlgorithm>(() => new GZipAlgorithm(), DelayThreadSafetyMode.ExecutionAndPublication);
            Deflate = new Delayed<CompressionAlgorithm>(() => new DeflateAlgorithm(), DelayThreadSafetyMode.ExecutionAndPublication);
            LZ4 = new Delayed<CompressionAlgorithm>(() => new LZ4Algorithm(0), DelayThreadSafetyMode.ExecutionAndPublication);
            LZ4HC = new Delayed<CompressionAlgorithm>(() => new LZ4Algorithm(1), DelayThreadSafetyMode.ExecutionAndPublication);
        }
    }
}
