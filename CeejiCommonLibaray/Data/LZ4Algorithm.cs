using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 实现 LZ4 压缩算法。
    /// </summary>
    public class LZ4Algorithm : CompressionAlgorithm {
        public LZ4Algorithm() { }
        public LZ4Algorithm(int level) {
            this.CompressionLevel = level;
        }

        /// <summary>
        /// 获取算法的压缩级别，其中，0 为普通压缩，1 为极限压缩（LZ4 HC）。
        /// </summary>
        public int CompressionLevel { get; set; }

        /// <summary>
        /// 获取算法是在 32 位 还是 64 位下执行。
        /// </summary>
        public static int BitMode {
            get {
                return mBitMode;
            }
        }

        static LZ4Algorithm() {
            mBitMode = IntPtr.Size == 4 ? 32 : 64;
        }

        public override int CompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            int length;
            if (mBitMode == 32 && CompressionLevel == 0) {
                length = Codec.LZ4.LZ4Codec.Encode32(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset + 4, outputBuffer.Length - outputOffset - 4);
            }
            else if (mBitMode == 64 && CompressionLevel == 0) {
                length = Codec.LZ4.LZ4Codec.Encode64(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset + 4, outputBuffer.Length - outputOffset - 4);
            }
            else if (mBitMode == 32 && CompressionLevel == 1) {
                length = Codec.LZ4.LZ4Codec.Encode32HC(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset + 4, outputBuffer.Length - outputOffset - 4);
            }
            else{
                length = Codec.LZ4.LZ4Codec.Encode64HC(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset + 4, outputBuffer.Length - outputOffset - 4);
            }

            var lengthNetwork = System.Net.IPAddress.HostToNetworkOrder(inputCount);
            BitConverter.GetBytes(lengthNetwork).CopyTo(outputBuffer, outputOffset);

            return length + 4;
        }

        public override byte[] CompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            var buffer = new byte[Codec.LZ4.LZ4Codec.MaximumOutputLength(inputCount) + 20];
            int length = CompressBlock(inputBuffer, inputOffset, inputCount, buffer, 0);

            var ret = new byte[length];
            Array.Copy(buffer, 0, ret, 0, length);

            return ret;
        }

        public override int DecompressBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset) {
            int length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(inputBuffer, inputOffset));

            if (mBitMode == 32) {
                return Codec.LZ4.LZ4Codec.Decode32(inputBuffer, inputOffset + 4, inputCount - 4, outputBuffer, outputOffset, length, true);
            }
            else {
                return Codec.LZ4.LZ4Codec.Decode64(inputBuffer, inputOffset + 4, inputCount - 4, outputBuffer, outputOffset, length, true);
            }
        }

        public override byte[] DecompressFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount) {
            int length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(inputBuffer, inputOffset));

            if (mBitMode == 32) {
                return Codec.LZ4.LZ4Codec.Decode32(inputBuffer, inputOffset + 4, inputCount - 4, length);
            }
            else {
                return Codec.LZ4.LZ4Codec.Decode64(inputBuffer, inputOffset + 4, inputCount - 4, length);
            }
        }

        private static int mBitMode;
    }
}
