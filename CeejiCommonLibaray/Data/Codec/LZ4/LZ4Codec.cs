#define LZ4n

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Data.Codec.LZ4 {
    public class LZ4Codec {
        #region configuration

        /// <summary>
        /// Memory usage formula : N->2^N Bytes (examples : 10 -> 1KB; 12 -> 4KB ; 16 -> 64KB; 20 -> 1MB; etc.)
        /// Increasing memory usage improves compression ratio
        /// Reduced memory usage can improve speed, due to cache effect
        /// Default value is 14, for 16KB, which nicely fits into Intel x86 L1 cache
        /// </summary>
        private const int MEMORY_USAGE = 14;

        /// <summary>
        /// Decreasing this value will make the algorithm skip faster data segments considered "incompressible"
        /// This may decrease compression ratio dramatically, but will be faster on incompressible data
        /// Increasing this value will make the algorithm search more before declaring a segment "incompressible"
        /// This could improve compression a bit, but will be slower on incompressible data
        /// The default value (6) is recommended
        /// </summary>
        private const int NOTCOMPRESSIBLE_DETECTIONLEVEL = 6;

        #endregion

        #region consts

        private const int MINMATCH = 4;
#pragma warning disable 162
        private const int SKIPSTRENGTH = NOTCOMPRESSIBLE_DETECTIONLEVEL > 2 ? NOTCOMPRESSIBLE_DETECTIONLEVEL : 2;
#pragma warning restore 162
        private const int COPYLENGTH = 8;
        private const int LASTLITERALS = 5;
        private const int MFLIMIT = COPYLENGTH + MINMATCH;
        private const int MINLENGTH = MFLIMIT + 1;
        private const int MAXD_LOG = 16;
        private const int MAXD = 1 << MAXD_LOG;
        private const int MAXD_MASK = MAXD - 1;
        private const int MAX_DISTANCE = (1 << MAXD_LOG) - 1;
        private const int ML_BITS = 4;
        private const int ML_MASK = (1 << ML_BITS) - 1;
        private const int RUN_BITS = 8 - ML_BITS;
        private const int RUN_MASK = (1 << RUN_BITS) - 1;
        private const int STEPSIZE_64 = 8;
        private const int STEPSIZE_32 = 4;

        private const int LZ4_64KLIMIT = (1 << 16) + (MFLIMIT - 1);

        private const int HASH_LOG = MEMORY_USAGE - 2;
        private const int HASH_TABLESIZE = 1 << HASH_LOG;
        private const int HASH_ADJUST = (MINMATCH * 8) - HASH_LOG;

        private const int HASH64K_LOG = HASH_LOG + 1;
        private const int HASH64K_TABLESIZE = 1 << HASH64K_LOG;
        private const int HASH64K_ADJUST = (MINMATCH * 8) - HASH64K_LOG;

        private const int HASHHC_LOG = MAXD_LOG - 1;
        private const int HASHHC_TABLESIZE = 1 << HASHHC_LOG;
        private const int HASHHC_ADJUST = (MINMATCH * 8) - HASHHC_LOG;
        //private const int HASHHC_MASK = HASHHC_TABLESIZE - 1;

        private static readonly int[] DECODER_TABLE_32 = new[] { 0, 3, 2, 3, 0, 0, 0, 0 };
        private static readonly int[] DECODER_TABLE_64 = new[] { 0, 0, 0, -1, 0, 1, 2, 3 };

        private static readonly int[] DEBRUIJN_TABLE_32 = new[] {
			0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1,
			3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1
		};

        private static readonly int[] DEBRUIJN_TABLE_64 = new[] {
			0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7,
			0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7,
			7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6,
			7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7
		};

        private const int MAX_NB_ATTEMPTS = 256;
        private const int OPTIMAL_ML = (ML_MASK - 1) + MINMATCH;

        #endregion

        #region public interface (common)

        /// <summary>Gets maximum the length of the output.</summary>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Maximum number of bytes needed for compressed buffer.</returns>
        public static int MaximumOutputLength(int inputLength) {
            return inputLength + (inputLength / 255) + 16;
        }
        #endregion

        #region internal interface (common)

        internal static void CheckArguments(
            byte[] input, int inputOffset, ref int inputLength,
            byte[] output, int outputOffset, ref int outputLength) {
            if (inputLength < 0) inputLength = input.Length - inputOffset;
            if (inputLength == 0) {
                outputLength = 0;
                return;
            }

            if (input == null) throw new ArgumentNullException("input");
            if (inputOffset < 0 || inputOffset + inputLength > input.Length)
                throw new ArgumentException("inputOffset and inputLength are invalid for given input");

            if (outputLength < 0) outputLength = output.Length - outputOffset;
            if (output == null) throw new ArgumentNullException("output");
            if (outputOffset < 0 || outputOffset + outputLength > output.Length)
                throw new ArgumentException("outputOffset and outputLength are invalid for given output");
        }

        #endregion

        /// <summary>Copies block of memory.</summary>
        /// <param name="src">The source.</param>
        /// <param name="dst">The destination.</param>
        /// <param name="len">The length (in bytes).</param>
        private static unsafe void BlockCopy(byte* src, byte* dst, int len) {
            while (len >= 8) {
                *(ulong*)dst = *(ulong*)src;
                dst += 8;
                src += 8;
                len -= 8;
            }
            if (len >= 4) {
                *(uint*)dst = *(uint*)src;
                dst += 4;
                src += 4;
                len -= 4;
            }
            if (len >= 2) {
                *(ushort*)dst = *(ushort*)src;
                dst += 2;
                src += 2;
                len -= 2;
            }
            if (len >= 1) {
                *dst = *src; /* d++; s++; l--; */
            }
        }

        /// <summary>Copies block of memory.</summary>
        /// <param name="dst">The destination.</param>
        /// <param name="len">The length (in bytes).</param>
        /// <param name="val">The value.</param>
        private static unsafe void BlockFill(byte* dst, int len, byte val) {
            if (len >= 8) {
                ulong mask = val;
                mask |= mask << 8;
                mask |= mask << 16;
                mask |= mask << 32;
                do {
                    *(ulong*)dst = mask;
                    dst += 8;
                    len -= 8;
                } while (len >= 8);
            }

            while (len-- > 0) *dst++ = val;
        }

        #region Encode32

        /// <summary>Encodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="output">The output.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Encode32(
            byte* input,
            byte* output,
            int inputLength,
            int outputLength) {
            if (inputLength < LZ4_64KLIMIT) {
                var hashTable = new ushort[HASH64K_TABLESIZE];
                fixed (ushort* h = &hashTable[0]) {
                    return LZ4_compress64kCtx_32(h, input, output, inputLength, outputLength);
                }
            }
            else {
                var hashTable = new byte*[HASH_TABLESIZE];
                fixed (byte** h = &hashTable[0]) {
                    return LZ4_compressCtx_32(h, input, output, inputLength, outputLength);
                }
            }
        }

        /// <summary>Encodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputOffset">The output offset.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Encode32(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength) {
            CheckArguments(
                input, inputOffset, ref inputLength,
                output, outputOffset, ref outputLength);

            if (outputLength == 0) return 0;

            fixed (byte* inputPtr = &input[inputOffset])
            fixed (byte* outputPtr = &output[outputOffset]) {
                return Encode32(inputPtr, outputPtr, inputLength, outputLength);
            }
        }

        /// <summary>Encodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Compressed buffer.</returns>
        public static byte[] Encode32(byte[] input, int inputOffset, int inputLength) {
            if (inputLength < 0) inputLength = input.Length - inputOffset;

            if (input == null) throw new ArgumentNullException("input");
            if (inputOffset < 0 || inputOffset + inputLength > input.Length)
                throw new ArgumentException("inputOffset and inputLength are invalid for given input");

            var result = new byte[MaximumOutputLength(inputLength)];
            var length = Encode32(input, inputOffset, inputLength, result, 0, result.Length);

            if (length != result.Length) {
                if (length < 0)
                    throw new InvalidOperationException("Compression has been corrupted");
                var buffer = new byte[length];
                Buffer.BlockCopy(result, 0, buffer, 0, length);
                return buffer;
            }
            return result;
        }

        #endregion

        #region Decode32

        /// <summary>Decodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <param name="knownOutputLength">Set it to <c>true</c> if output length is known.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Decode32(
            byte* input,
            int inputLength,
            byte* output,
            int outputLength,
            bool knownOutputLength) {
            if (knownOutputLength) {
                var length = LZ4_uncompress_32(input, output, outputLength);
                if (length != inputLength)
                    throw new ArgumentException("LZ4 block is corrupted, or invalid length has been given.");
                return outputLength;
            }
            else {
                var length = LZ4_uncompress_unknownOutputSize_32(input, output, inputLength, outputLength);
                if (length < 0)
                    throw new ArgumentException("LZ4 block is corrupted, or invalid length has been given.");
                return length;
            }
        }

        /// <summary>Decodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputOffset">The output offset.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <param name="knownOutputLength">Set it to <c>true</c> if output length is known.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Decode32(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength,
            bool knownOutputLength) {
            CheckArguments(
                input, inputOffset, ref inputLength,
                output, outputOffset, ref outputLength);

            if (outputLength == 0) return 0;

            fixed (byte* inputPtr = &input[inputOffset])
            fixed (byte* outputPtr = &output[outputOffset]) {
                return Decode32(inputPtr, inputLength, outputPtr, outputLength, knownOutputLength);
            }
        }

        /// <summary>Decodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Decompressed buffer.</returns>
        public static byte[] Decode32(byte[] input, int inputOffset, int inputLength, int outputLength) {
            if (inputLength < 0) inputLength = input.Length - inputOffset;

            if (input == null) throw new ArgumentNullException("input");
            if (inputOffset < 0 || inputOffset + inputLength > input.Length)
                throw new ArgumentException("inputOffset and inputLength are invalid for given input");

            var result = new byte[outputLength];
            var length = Decode32(input, inputOffset, inputLength, result, 0, outputLength, true);
            if (length != outputLength)
                throw new ArgumentException("outputLength is not valid");
            return result;
        }

        #endregion

        #region Encode64

        /// <summary>Encodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="output">The output.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Encode64(
            byte* input,
            byte* output,
            int inputLength,
            int outputLength) {
            if (inputLength < LZ4_64KLIMIT) {
                var hashTable = new ushort[HASH64K_TABLESIZE];
                fixed (ushort* h = &hashTable[0]) {
                    return LZ4_compress64kCtx_64(h, input, output, inputLength, outputLength);
                }
            }
            else {
                var hashTable = new uint[HASH_TABLESIZE];
                fixed (uint* h = &hashTable[0]) {
                    return LZ4_compressCtx_64(h, input, output, inputLength, outputLength);
                }
            }
        }

        /// <summary>Encodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputOffset">The output offset.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Encode64(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength) {
            CheckArguments(
                input, inputOffset, ref inputLength,
                output, outputOffset, ref outputLength);

            if (outputLength == 0) return 0;

            fixed (byte* inputPtr = &input[inputOffset])
            fixed (byte* outputPtr = &output[outputOffset]) {
                return Encode64(inputPtr, outputPtr, inputLength, outputLength);
            }
        }

        /// <summary>Encodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Compressed buffer.</returns>
        public static byte[] Encode64(byte[] input, int inputOffset, int inputLength) {
            if (inputLength < 0) inputLength = input.Length - inputOffset;

            if (input == null) throw new ArgumentNullException("input");
            if (inputOffset < 0 || inputOffset + inputLength > input.Length)
                throw new ArgumentException("inputOffset and inputLength are invalid for given input");

            var result = new byte[MaximumOutputLength(inputLength)];
            var length = Encode64(input, inputOffset, inputLength, result, 0, result.Length);

            if (length != result.Length) {
                if (length < 0)
                    throw new InvalidOperationException("Compression has been corrupted");
                var buffer = new byte[length];
                Buffer.BlockCopy(result, 0, buffer, 0, length);
                return buffer;
            }
            return result;
        }

        #endregion

        #region Decode64

        /// <summary>Decodes the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <param name="knownOutputLength">Set it to <c>true</c> if output length is known.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Decode64(
            byte* input,
            int inputLength,
            byte* output,
            int outputLength,
            bool knownOutputLength) {
            if (knownOutputLength) {
                var length = LZ4_uncompress_64(input, output, outputLength);
                if (length != inputLength)
                    throw new ArgumentException("LZ4 block is corrupted, or invalid length has been given.");
                return outputLength;
            }
            else {
                var length = LZ4_uncompress_unknownOutputSize_64(input, output, inputLength, outputLength);
                if (length < 0)
                    throw new ArgumentException("LZ4 block is corrupted, or invalid length has been given.");
                return length;
            }
        }

        /// <summary>Decode64s the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputOffset">The output offset.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <param name="knownOutputLength">Set it to <c>true</c> if output length is known.</param>
        /// <returns>Number of bytes written.</returns>
        public static unsafe int Decode64(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength,
            bool knownOutputLength) {
            CheckArguments(
                input, inputOffset, ref inputLength,
                output, outputOffset, ref outputLength);

            if (outputLength == 0) return 0;

            fixed (byte* inputPtr = &input[inputOffset])
            fixed (byte* outputPtr = &output[outputOffset]) {
                return Decode64(inputPtr, inputLength, outputPtr, outputLength, knownOutputLength);
            }
        }

        /// <summary>Decode64s the specified input.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Decompressed buffer.</returns>
        public static byte[] Decode64(byte[] input, int inputOffset, int inputLength, int outputLength) {
            if (inputLength < 0) inputLength = input.Length - inputOffset;

            if (input == null) throw new ArgumentNullException("input");
            if (inputOffset < 0 || inputOffset + inputLength > input.Length)
                throw new ArgumentException("inputOffset and inputLength are invalid for given input");

            var result = new byte[outputLength];
            var length = Decode64(input, inputOffset, inputLength, result, 0, outputLength, true);
            if (length != outputLength)
                throw new ArgumentException("outputLength is not valid");
            return result;
        }

        #endregion

        #region HC utilities

        // ReSharper disable InconsistentNaming

        private unsafe class LZ4HC_Data_Structure {
            public byte* src_base;
            public byte* nextToUpdate;
            public int[] hashTable;
            public ushort[] chainTable;
        };

        // ReSharper restore InconsistentNaming


        private static unsafe LZ4HC_Data_Structure LZ4HC_Create(byte* src) {
            var hc4 = new LZ4HC_Data_Structure {
                hashTable = new int[HASHHC_TABLESIZE],
                chainTable = new ushort[MAXD]
            };

            fixed (ushort* ct = &hc4.chainTable[0]) {
                BlockFill((byte*)ct, MAXD * sizeof(ushort), 0xFF);
            }

            hc4.src_base = src;
            hc4.nextToUpdate = src + 1;

            return hc4;
        }

        #endregion

        #region Encode32HC

        private static unsafe int LZ4_compressHC_32(byte* input, byte* output, int inputLength, int outputLength) {
            return LZ4_compressHCCtx_32(LZ4HC_Create(input), input, output, inputLength, outputLength);
        }

        /// <summary>Encodes the specified input using HC codec.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputOffset">The output offset.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Number of bytes written. NOTE: when output buffer is too small it returns negative value.</returns>
        public static unsafe int Encode32HC(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength) {
            if (inputLength == 0) return 0;

            CheckArguments(
                input, inputOffset, ref inputLength,
                output, outputOffset, ref outputLength);

            fixed (byte* inputPtr = &input[inputOffset])
            fixed (byte* outputPtr = &output[outputOffset]) {
                var length = LZ4_compressHC_32(inputPtr, outputPtr, inputLength, outputLength);
                return length <= 0 ? -1 : length;
            }
        }

        /// <summary>Encodes the specified input using HC codec.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Buffer with compressed data (NOTE: it can be bigger than input).</returns>
        public static byte[] Encode32HC(
            byte[] input, int inputOffset, int inputLength) {
            if (inputLength == 0) return new byte[0];
            var outputLength = MaximumOutputLength(inputLength);
            var result = new byte[outputLength];
            var length = Encode32HC(input, inputOffset, inputLength, result, 0, outputLength);

            if (length < 0)
                throw new ArgumentException("Provided data seems to be corrupted.");

            if (length != outputLength) {
                var buffer = new byte[length];
                Buffer.BlockCopy(result, 0, buffer, 0, length);
                result = buffer;
            }

            return result;
        }

        #endregion

        #region Encode64HC

        private static unsafe int LZ4_compressHC_64(byte* input, byte* output, int inputLength, int outputLength) {
            return LZ4_compressHCCtx_64(LZ4HC_Create(input), input, output, inputLength, outputLength);
        }

        /// <summary>Encodes the specified input using HC codec.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <param name="output">The output.</param>
        /// <param name="outputOffset">The output offset.</param>
        /// <param name="outputLength">Length of the output.</param>
        /// <returns>Number of bytes written. NOTE: when output buffer is too small it returns negative value.</returns>
        public static unsafe int Encode64HC(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength) {
            if (inputLength == 0) return 0;

            CheckArguments(
                input, inputOffset, ref inputLength,
                output, outputOffset, ref outputLength);

            fixed (byte* inputPtr = &input[inputOffset])
            fixed (byte* outputPtr = &output[outputOffset]) {
                var length = LZ4_compressHC_64(inputPtr, outputPtr, inputLength, outputLength);
                return length <= 0 ? -1 : length;
            }
        }

        /// <summary>Encodes the specified input using HC codec.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Buffer with compressed data (NOTE: it can be bigger than input).</returns>
        public static byte[] Encode64HC(
            byte[] input, int inputOffset, int inputLength) {
            if (inputLength == 0) return new byte[0];
            var outputLength = MaximumOutputLength(inputLength);
            var result = new byte[outputLength];
            var length = Encode64HC(input, inputOffset, inputLength, result, 0, outputLength);

            if (length < 0)
                throw new ArgumentException("Provided data seems to be corrupted.");

            if (length != outputLength) {
                var buffer = new byte[length];
                Buffer.BlockCopy(result, 0, buffer, 0, length);
                result = buffer;
            }

            return result;
        }

        #endregion

        #region LZ4_compressCtx_32

        private static unsafe int LZ4_compressCtx_32(
            byte** hash_table,
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            byte* _p;

            fixed (int* debruijn32 = &DEBRUIJN_TABLE_32[0]) {
                // r93
                var src_p = src;
                const int src_base = 0;
                var src_anchor = src_p;
                var src_end = src_p + src_len;
                var src_mflimit = src_end - MFLIMIT;

                var dst_p = dst;
                var dst_end = dst_p + dst_maxlen;

                var src_LASTLITERALS = src_end - LASTLITERALS;
                var src_LASTLITERALS_1 = src_LASTLITERALS - 1;

                var src_LASTLITERALS_STEPSIZE_1 = src_LASTLITERALS - (STEPSIZE_32 - 1);
                var dst_LASTLITERALS_1 = dst_end - (1 + LASTLITERALS);
                var dst_LASTLITERALS_3 = dst_end - (2 + 1 + LASTLITERALS);

                int length;

                uint h, h_fwd;

                // Init
                if (src_len < MINLENGTH) goto _last_literals;

                // First Byte
                hash_table[((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST)] = (src_p - src_base);
                src_p++;
                h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST);

                // Main Loop
                while (true) {
                    var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
                    var src_p_fwd = src_p;
                    byte* xxx_ref;
                    byte* xxx_token;

                    // Find a match
                    do {
                        h = h_fwd;
                        var step = findMatchAttempts++ >> SKIPSTRENGTH;
                        src_p = src_p_fwd;
                        src_p_fwd = src_p + step;

                        if (src_p_fwd > src_mflimit) goto _last_literals;

                        h_fwd = ((((*(uint*)(src_p_fwd))) * 2654435761u) >> HASH_ADJUST);
                        xxx_ref = src_base + hash_table[h];
                        hash_table[h] = (src_p - src_base);
                    } while ((xxx_ref < src_p - MAX_DISTANCE) || ((*(uint*)(xxx_ref)) != (*(uint*)(src_p))));

                    // Catch up
                    while ((src_p > src_anchor) && (xxx_ref > src) && (src_p[-1] == xxx_ref[-1])) {
                        src_p--;
                        xxx_ref--;
                    }

                    // Encode Literal length
                    length = (int)(src_p - src_anchor);
                    xxx_token = dst_p++;

                    if (dst_p + length + (length >> 8) > dst_LASTLITERALS_3) return 0; // Check output limit

                    if (length >= RUN_MASK) {
                        var len = length - RUN_MASK;
                        *xxx_token = (RUN_MASK << ML_BITS);
                        if (len > 254) {
                            do {
                                *dst_p++ = 255;
                                len -= 255;
                            } while (len > 254);
                            *dst_p++ = (byte)len;
                            BlockCopy(src_anchor, dst_p, (length));
                            dst_p += length;
                            goto _next_match;
                        }
                        *dst_p++ = (byte)len;
                    }
                    else {
                        *xxx_token = (byte)(length << ML_BITS);
                    }

                    // Copy Literals
                    _p = dst_p + (length);
                    do {
                        *(uint*)dst_p = *(uint*)src_anchor;
                        dst_p += 4;
                        src_anchor += 4;
                        *(uint*)dst_p = *(uint*)src_anchor;
                        dst_p += 4;
                        src_anchor += 4;
                    } while (dst_p < _p);
                    dst_p = _p;

                _next_match:

                    // Encode Offset
                    *(ushort*)dst_p = (ushort)(src_p - xxx_ref);
                    dst_p += 2;

                    // Start Counting
                    src_p += MINMATCH;
                    xxx_ref += MINMATCH; // MinMatch already verified
                    src_anchor = src_p;

                    while (src_p < src_LASTLITERALS_STEPSIZE_1) {
                        var diff = (*(int*)(xxx_ref)) ^ (*(int*)(src_p));
                        if (diff == 0) {
                            src_p += STEPSIZE_32;
                            xxx_ref += STEPSIZE_32;
                            continue;
                        }
                        src_p += debruijn32[(((uint)((diff) & -(diff)) * 0x077CB531u)) >> 27];
                        goto _endCount;
                    }

                    if ((src_p < src_LASTLITERALS_1) && ((*(ushort*)(xxx_ref)) == (*(ushort*)(src_p)))) {
                        src_p += 2;
                        xxx_ref += 2;
                    }
                    if ((src_p < src_LASTLITERALS) && (*xxx_ref == *src_p)) src_p++;

                _endCount:

                    // Encode MatchLength
                    length = (int)(src_p - src_anchor);

                    if (dst_p + (length >> 8) > dst_LASTLITERALS_1) return 0; // Check output limit

                    if (length >= ML_MASK) {
                        *xxx_token += ML_MASK;
                        length -= ML_MASK;
                        for (; length > 509; length -= 510) {
                            *dst_p++ = 255;
                            *dst_p++ = 255;
                        }
                        if (length > 254) {
                            length -= 255;
                            *dst_p++ = 255;
                        }
                        *dst_p++ = (byte)length;
                    }
                    else {
                        *xxx_token += (byte)length;
                    }

                    // Test end of chunk
                    if (src_p > src_mflimit) {
                        src_anchor = src_p;
                        break;
                    }

                    // Fill table
                    hash_table[((((*(uint*)(src_p - 2))) * 2654435761u) >> HASH_ADJUST)] = (src_p - 2 - src_base);

                    // Test next position

                    h = ((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST);
                    xxx_ref = src_base + hash_table[h];
                    hash_table[h] = (src_p - src_base);

                    if ((xxx_ref > src_p - (MAX_DISTANCE + 1)) && ((*(uint*)(xxx_ref)) == (*(uint*)(src_p)))) {
                        xxx_token = dst_p++;
                        *xxx_token = 0;
                        goto _next_match;
                    }

                    // Prepare next loop
                    src_anchor = src_p++;
                    h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST);
                }

            _last_literals:

                // Encode Last Literals
				{
                    var lastRun = (int)(src_end - src_anchor);

                    if (dst_p + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > dst_end) return 0;

                    if (lastRun >= RUN_MASK) {
                        *dst_p++ = (RUN_MASK << ML_BITS);
                        lastRun -= RUN_MASK;
                        for (; lastRun > 254; lastRun -= 255) *dst_p++ = 255;
                        *dst_p++ = (byte)lastRun;
                    }
                    else *dst_p++ = (byte)(lastRun << ML_BITS);
                    BlockCopy(src_anchor, dst_p, (int)(src_end - src_anchor));
                    dst_p += src_end - src_anchor;
                }

                // End
                return (int)((dst_p) - dst);
            }
        }

        #endregion

        #region LZ4_compress64kCtx_32

        private static unsafe int LZ4_compress64kCtx_32(
            ushort* hash_table,
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            byte* _p;

            fixed (int* debruijn32 = &DEBRUIJN_TABLE_32[0]) {
                // r93
                var src_p = src;
                var src_anchor = src_p;
                var src_base = src_p;
                var src_end = src_p + src_len;
                var src_mflimit = src_end - MFLIMIT;

                var dst_p = dst;
                var dst_end = dst_p + dst_maxlen;

                var src_LASTLITERALS = src_end - LASTLITERALS;
                var src_LASTLITERALS_1 = src_LASTLITERALS - 1;

                var src_LASTLITERALS_STEPSIZE_1 = src_LASTLITERALS - (STEPSIZE_32 - 1);
                var dst_LASTLITERALS_1 = dst_end - (1 + LASTLITERALS);
                var dst_LASTLITERALS_3 = dst_end - (2 + 1 + LASTLITERALS);

                int len, length;

                uint h, h_fwd;

                // Init
                if (src_len < MINLENGTH) goto _last_literals;

                // First Byte
                src_p++;
                h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH64K_ADJUST);

                // Main Loop
                while (true) {
                    var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
                    var src_p_fwd = src_p;
                    byte* xxx_ref;
                    byte* xxx_token;

                    // Find a match
                    do {
                        h = h_fwd;
                        var step = findMatchAttempts++ >> SKIPSTRENGTH;
                        src_p = src_p_fwd;
                        src_p_fwd = src_p + step;

                        if (src_p_fwd > src_mflimit) goto _last_literals;

                        h_fwd = ((((*(uint*)(src_p_fwd))) * 2654435761u) >> HASH64K_ADJUST);
                        xxx_ref = src_base + hash_table[h];
                        hash_table[h] = (ushort)(src_p - src_base);
                    } while ((*(uint*)(xxx_ref)) != (*(uint*)(src_p)));

                    // Catch up
                    while ((src_p > src_anchor) && (xxx_ref > src) && (src_p[-1] == xxx_ref[-1])) {
                        src_p--;
                        xxx_ref--;
                    }

                    // Encode Literal length
                    length = (int)(src_p - src_anchor);
                    xxx_token = dst_p++;

                    if (dst_p + length + (length >> 8) > dst_LASTLITERALS_3) return 0; // Check output limit

                    if (length >= RUN_MASK) {
                        len = length - RUN_MASK;
                        *xxx_token = (RUN_MASK << ML_BITS);
                        if (len > 254) {
                            do {
                                *dst_p++ = 255;
                                len -= 255;
                            } while (len > 254);
                            *dst_p++ = (byte)len;
                            BlockCopy(src_anchor, dst_p, (length));
                            dst_p += length;
                            goto _next_match;
                        }
                        *dst_p++ = (byte)len;
                    }
                    else {
                        *xxx_token = (byte)(length << ML_BITS);
                    }

                    // Copy Literals
                    _p = dst_p + (length);
                    do {
                        *(uint*)dst_p = *(uint*)src_anchor;
                        dst_p += 4;
                        src_anchor += 4;
                        *(uint*)dst_p = *(uint*)src_anchor;
                        dst_p += 4;
                        src_anchor += 4;
                    } while (dst_p < _p);
                    dst_p = _p;

                _next_match:

                    // Encode Offset
                    *(ushort*)dst_p = (ushort)(src_p - xxx_ref);
                    dst_p += 2;

                    // Start Counting
                    src_p += MINMATCH;
                    xxx_ref += MINMATCH; // MinMatch verified
                    src_anchor = src_p;

                    while (src_p < src_LASTLITERALS_STEPSIZE_1) {
                        var diff = (*(int*)(xxx_ref)) ^ (*(int*)(src_p));
                        if (diff == 0) {
                            src_p += STEPSIZE_32;
                            xxx_ref += STEPSIZE_32;
                            continue;
                        }
                        src_p += debruijn32[(((uint)((diff) & -(diff)) * 0x077CB531u)) >> 27];
                        goto _endCount;
                    }

                    if ((src_p < src_LASTLITERALS_1) && ((*(ushort*)(xxx_ref)) == (*(ushort*)(src_p)))) {
                        src_p += 2;
                        xxx_ref += 2;
                    }
                    if ((src_p < src_LASTLITERALS) && (*xxx_ref == *src_p)) src_p++;

                _endCount:

                    // Encode MatchLength
                    len = (int)(src_p - src_anchor);

                    if (dst_p + (len >> 8) > dst_LASTLITERALS_1) return 0; // Check output limit

                    if (len >= ML_MASK) {
                        *xxx_token += ML_MASK;
                        len -= ML_MASK;
                        for (; len > 509; len -= 510) {
                            *dst_p++ = 255;
                            *dst_p++ = 255;
                        }
                        if (len > 254) {
                            len -= 255;
                            *dst_p++ = 255;
                        }
                        *dst_p++ = (byte)len;
                    }
                    else *xxx_token += (byte)len;

                    // Test end of chunk
                    if (src_p > src_mflimit) {
                        src_anchor = src_p;
                        break;
                    }

                    // Fill table
                    hash_table[((((*(uint*)(src_p - 2))) * 2654435761u) >> HASH64K_ADJUST)] = (ushort)(src_p - 2 - src_base);

                    // Test next position

                    h = ((((*(uint*)(src_p))) * 2654435761u) >> HASH64K_ADJUST);
                    xxx_ref = src_base + hash_table[h];
                    hash_table[h] = (ushort)(src_p - src_base);

                    if ((*(uint*)(xxx_ref)) == (*(uint*)(src_p))) {
                        xxx_token = dst_p++;
                        *xxx_token = 0;
                        goto _next_match;
                    }

                    // Prepare next loop
                    src_anchor = src_p++;
                    h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH64K_ADJUST);
                }

            _last_literals:

                // Encode Last Literals
				{
                    var lastRun = (int)(src_end - src_anchor);
                    if (dst_p + lastRun + 1 + (lastRun - RUN_MASK + 255) / 255 > dst_end) return 0;
                    if (lastRun >= RUN_MASK) {
                        *dst_p++ = (RUN_MASK << ML_BITS);
                        lastRun -= RUN_MASK;
                        for (; lastRun > 254; lastRun -= 255) *dst_p++ = 255;
                        *dst_p++ = (byte)lastRun;
                    }
                    else *dst_p++ = (byte)(lastRun << ML_BITS);
                    BlockCopy(src_anchor, dst_p, (int)(src_end - src_anchor));
                    dst_p += src_end - src_anchor;
                }

                // End
                return (int)((dst_p) - dst);
            }
        }

        #endregion

        #region LZ4_uncompress_32

        private static unsafe int LZ4_uncompress_32(
            byte* src,
            byte* dst,
            int dst_len) {
            fixed (int* dec32table = &DECODER_TABLE_32[0]) {
                // r93
                var src_p = src;
                byte* xxx_ref;

                var dst_p = dst;
                var dst_end = dst_p + dst_len;
                byte* dst_cpy;

                var dst_LASTLITERALS = dst_end - LASTLITERALS;
                var dst_COPYLENGTH = dst_end - COPYLENGTH;
                var dst_COPYLENGTH_STEPSIZE_4 = dst_end - COPYLENGTH - (STEPSIZE_32 - 4);

                uint xxx_token;

                // Main Loop
                while (true) {
                    int length;

                    // get runlength
                    xxx_token = *src_p++;
                    if ((length = (int)(xxx_token >> ML_BITS)) == RUN_MASK) {
                        int len;
                        for (; (len = *src_p++) == 255; length += 255) {
                            /* do nothing */
                        }
                        length += len;
                    }

                    // copy literals
                    dst_cpy = dst_p + length;

                    if (dst_cpy > dst_COPYLENGTH) {
                        if (dst_cpy != dst_end) goto _output_error; // Error : not enough place for another match (min 4) + 5 literals
                        BlockCopy(src_p, dst_p, (length));
                        src_p += length;
                        break; // EOF
                    }
                    do {
                        *(uint*)dst_p = *(uint*)src_p;
                        dst_p += 4;
                        src_p += 4;
                        *(uint*)dst_p = *(uint*)src_p;
                        dst_p += 4;
                        src_p += 4;
                    } while (dst_p < dst_cpy);
                    src_p -= (dst_p - dst_cpy);
                    dst_p = dst_cpy;

                    // get offset
                    xxx_ref = (dst_cpy) - (*(ushort*)(src_p));
                    src_p += 2;
                    if (xxx_ref < dst) goto _output_error; // Error : offset outside destination buffer

                    // get matchlength
                    if ((length = (int)(xxx_token & ML_MASK)) == ML_MASK) {
                        for (; *src_p == 255; length += 255) src_p++;
                        length += *src_p++;
                    }

                    // copy repeated sequence
                    if ((dst_p - xxx_ref) < STEPSIZE_32) {
                        const int dec64 = 0;

                        dst_p[0] = xxx_ref[0];
                        dst_p[1] = xxx_ref[1];
                        dst_p[2] = xxx_ref[2];
                        dst_p[3] = xxx_ref[3];
                        dst_p += 4;
                        xxx_ref += 4;
                        xxx_ref -= dec32table[dst_p - xxx_ref];
                        (*(uint*)(dst_p)) = (*(uint*)(xxx_ref));
                        dst_p += STEPSIZE_32 - 4;
                        xxx_ref -= dec64;
                    }
                    else {
                        *(uint*)dst_p = *(uint*)xxx_ref;
                        dst_p += 4;
                        xxx_ref += 4;
                    }
                    dst_cpy = dst_p + length - (STEPSIZE_32 - 4);

                    if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4) {
                        if (dst_cpy > dst_LASTLITERALS) goto _output_error; // Error : last 5 bytes must be literals
                        {
                            do {
                                *(uint*)dst_p = *(uint*)xxx_ref;
                                dst_p += 4;
                                xxx_ref += 4;
                                *(uint*)dst_p = *(uint*)xxx_ref;
                                dst_p += 4;
                                xxx_ref += 4;
                            } while (dst_p < dst_COPYLENGTH);
                        }

                        while (dst_p < dst_cpy) *dst_p++ = *xxx_ref++;
                        dst_p = dst_cpy;
                        continue;
                    }

                    do {
                        *(uint*)dst_p = *(uint*)xxx_ref;
                        dst_p += 4;
                        xxx_ref += 4;
                        *(uint*)dst_p = *(uint*)xxx_ref;
                        dst_p += 4;
                        xxx_ref += 4;
                    } while (dst_p < dst_cpy);
                    dst_p = dst_cpy; // correction
                }

                // end of decoding
                return (int)((src_p) - src);

                                                               // write overflow error detected
            _output_error:
                return (int)(-((src_p) - src));
            }
        }

        #endregion

        #region LZ4_uncompress_unknownOutputSize_32

        private static unsafe int LZ4_uncompress_unknownOutputSize_32(
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            fixed (int* dec32table = &DECODER_TABLE_32[0]) {
                // r93
                var src_p = src;
                var src_end = src_p + src_len;
                byte* xxx_ref;

                var dst_p = dst;
                var dst_end = dst_p + dst_maxlen;
                byte* dst_cpy;

                var src_LASTLITERALS_3 = (src_end - (2 + 1 + LASTLITERALS));
                var src_LASTLITERALS_1 = (src_end - (LASTLITERALS + 1));
                var dst_COPYLENGTH = (dst_end - COPYLENGTH);
                var dst_COPYLENGTH_STEPSIZE_4 = (dst_end - (COPYLENGTH + (STEPSIZE_32 - 4)));
                var dst_LASTLITERALS = (dst_end - LASTLITERALS);
                var dst_MFLIMIT = (dst_end - MFLIMIT);

                // Special case
                if (src_p == src_end) goto _output_error; // A correctly formed null-compressed LZ4 must have at least one byte (token=0)

                // Main Loop
                while (true) {
                    uint xxx_token;
                    int length;

                    // get runlength
                    xxx_token = *src_p++;
                    if ((length = (int)(xxx_token >> ML_BITS)) == RUN_MASK) {
                        var s = 255;
                        while ((src_p < src_end) && (s == 255)) {
                            s = *src_p++;
                            length += s;
                        }
                    }

                    // copy literals
                    dst_cpy = dst_p + length;

                    if ((dst_cpy > dst_MFLIMIT) || (src_p + length > src_LASTLITERALS_3)) {
                        if (dst_cpy > dst_end) goto _output_error; // Error : writes beyond output buffer
                        if (src_p + length != src_end) goto _output_error; // Error : LZ4 format requires to consume all input at this stage (no match within the last 11 bytes, and at least 8 remaining input bytes for another match+literals)
                        BlockCopy(src_p, dst_p, (length));
                        dst_p += length;
                        break; // Necessarily EOF, due to parsing restrictions
                    }
                    do {
                        *(uint*)dst_p = *(uint*)src_p;
                        dst_p += 4;
                        src_p += 4;
                        *(uint*)dst_p = *(uint*)src_p;
                        dst_p += 4;
                        src_p += 4;
                    } while (dst_p < dst_cpy);
                    src_p -= (dst_p - dst_cpy);
                    dst_p = dst_cpy;

                    // get offset
                    xxx_ref = (dst_cpy) - (*(ushort*)(src_p));
                    src_p += 2;
                    if (xxx_ref < dst) goto _output_error; // Error : offset outside of destination buffer

                    // get matchlength
                    if ((length = (int)(xxx_token & ML_MASK)) == ML_MASK) {
                        while (src_p < src_LASTLITERALS_1) // Error : a minimum input bytes must remain for LASTLITERALS + token
						{
                            int s = *src_p++;
                            length += s;
                            if (s == 255) continue;
                            break;
                        }
                    }

                    // copy repeated sequence
                    if (dst_p - xxx_ref < STEPSIZE_32) {
                        const int dec64 = 0;

                        dst_p[0] = xxx_ref[0];
                        dst_p[1] = xxx_ref[1];
                        dst_p[2] = xxx_ref[2];
                        dst_p[3] = xxx_ref[3];
                        dst_p += 4;
                        xxx_ref += 4;
                        xxx_ref -= dec32table[dst_p - xxx_ref];
                        (*(uint*)(dst_p)) = (*(uint*)(xxx_ref));
                        dst_p += STEPSIZE_32 - 4;
                        xxx_ref -= dec64;
                    }
                    else {
                        *(uint*)dst_p = *(uint*)xxx_ref;
                        dst_p += 4;
                        xxx_ref += 4;
                    }
                    dst_cpy = dst_p + length - (STEPSIZE_32 - 4);

                    if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4) {
                        if (dst_cpy > dst_LASTLITERALS) goto _output_error; // Error : last 5 bytes must be literals
                        {
                            do {
                                *(uint*)dst_p = *(uint*)xxx_ref;
                                dst_p += 4;
                                xxx_ref += 4;
                                *(uint*)dst_p = *(uint*)xxx_ref;
                                dst_p += 4;
                                xxx_ref += 4;
                            } while (dst_p < dst_COPYLENGTH);
                        }

                        while (dst_p < dst_cpy) *dst_p++ = *xxx_ref++;
                        dst_p = dst_cpy;
                        continue;
                    }

                    do {
                        *(uint*)dst_p = *(uint*)xxx_ref;
                        dst_p += 4;
                        xxx_ref += 4;
                        *(uint*)dst_p = *(uint*)xxx_ref;
                        dst_p += 4;
                        xxx_ref += 4;
                    } while (dst_p < dst_cpy);
                    dst_p = dst_cpy; // correction
                }

                // end of decoding
                return (int)((dst_p) - dst);

                                                               // write overflow error detected
            _output_error:
                return (int)(-((src_p) - src));
            }
        }

        #endregion

        // Update chains up to ip (excluded)
        private static unsafe void LZ4HC_Insert_32(LZ4HC_Data_Structure hc4, byte* src_p) {
            fixed (ushort* chainTable = hc4.chainTable)
            fixed (int* hashTable = hc4.hashTable) {
                var src_base = hc4.src_base;

                while (hc4.nextToUpdate < src_p) {
                    var p = hc4.nextToUpdate;
                    var delta = (int)((p) - (hashTable[((((*(uint*)(p))) * 2654435761u) >> HASHHC_ADJUST)] + src_base));
                    if (delta > MAX_DISTANCE) delta = MAX_DISTANCE;
                    chainTable[((int)p) & MAXD_MASK] = (ushort)delta;
                    hashTable[((((*(uint*)(p))) * 2654435761u) >> HASHHC_ADJUST)] = (int)(p - src_base);
                    hc4.nextToUpdate++;
                }
            }
        }

        private static unsafe int LZ4HC_CommonLength_32(byte* p1, byte* p2, byte* src_LASTLITERALS) {
            fixed (int* debruijn32 = DEBRUIJN_TABLE_32) {
                var p1t = p1;

                while (p1t < src_LASTLITERALS - (STEPSIZE_32 - 1)) {
                    var diff = (*(int*)(p2)) ^ (*(int*)(p1t));
                    if (diff == 0) {
                        p1t += STEPSIZE_32;
                        p2 += STEPSIZE_32;
                        continue;
                    }
                    p1t += debruijn32[(((uint)((diff) & -(diff)) * 0x077CB531u)) >> 27];
                    return (int)(p1t - p1);
                }
                if ((p1t < (src_LASTLITERALS - 1)) && ((*(ushort*)(p2)) == (*(ushort*)(p1t)))) {
                    p1t += 2;
                    p2 += 2;
                }
                if ((p1t < src_LASTLITERALS) && (*p2 == *p1t)) p1t++;
                return (int)(p1t - p1);
            }
        }

        private static unsafe int LZ4HC_InsertAndFindBestMatch_32(
            LZ4HC_Data_Structure hc4, byte* src_p, byte* src_LASTLITERALS, ref byte* matchpos) {
            fixed (ushort* chainTable = hc4.chainTable)
            fixed (int* hashTable = hc4.hashTable) {
                var src_base = hc4.src_base;
                var nbAttempts = MAX_NB_ATTEMPTS;
                int repl = 0, ml = 0;
                ushort delta = 0;

                // HC4 match finder
                LZ4HC_Insert_32(hc4, src_p);
                var xxx_ref = (hashTable[((((*(uint*)(src_p))) * 2654435761u) >> HASHHC_ADJUST)] + src_base);

                // Detect repetitive sequences of length <= 4
                if (xxx_ref >= src_p - 4) // potential repetition
				{
                    if ((*(uint*)(xxx_ref)) == (*(uint*)(src_p))) // confirmed
					{
                        delta = (ushort)(src_p - xxx_ref);
                        repl = ml = LZ4HC_CommonLength_32(src_p + MINMATCH, xxx_ref + MINMATCH, src_LASTLITERALS) + MINMATCH;
                        matchpos = xxx_ref;
                    }
                    xxx_ref = ((xxx_ref) - chainTable[((int)xxx_ref) & MAXD_MASK]);
                }

                while ((xxx_ref >= src_p - MAX_DISTANCE) && (nbAttempts != 0)) {
                    nbAttempts--;
                    if (*(xxx_ref + ml) == *(src_p + ml))
                        if ((*(uint*)(xxx_ref)) == (*(uint*)(src_p))) {
                            var mlt = LZ4HC_CommonLength_32(src_p + MINMATCH, xxx_ref + MINMATCH, src_LASTLITERALS) + MINMATCH;
                            if (mlt > ml) {
                                ml = mlt;
                                matchpos = xxx_ref;
                            }
                        }
                    xxx_ref = ((xxx_ref) - chainTable[((int)xxx_ref) & MAXD_MASK]);
                }

                // Complete table
                if (repl != 0) {
                    var src_ptr = src_p;

                    var src_end = src_p + repl - (MINMATCH - 1);
                    while (src_ptr < src_end - delta) {
                        chainTable[((int)src_ptr) & MAXD_MASK] = delta; // Pre-Load
                        src_ptr++;
                    }
                    do {
                        chainTable[((int)src_ptr) & MAXD_MASK] = delta;
                        hashTable[((((*(uint*)(src_ptr))) * 2654435761u) >> HASHHC_ADJUST)] = (int)(src_ptr - src_base); // Head of chain
                        src_ptr++;
                    } while (src_ptr < src_end);
                    hc4.nextToUpdate = src_end;
                }

                return ml;
            }
        }

        private static unsafe int LZ4HC_InsertAndGetWiderMatch_32(
            LZ4HC_Data_Structure hc4, byte* src_p, byte* startLimit, byte* src_LASTLITERALS, int longest,
            ref byte* matchpos, ref byte* startpos) {
            fixed (ushort* chainTable = hc4.chainTable)
            fixed (int* hashTable = hc4.hashTable)
            fixed (int* debruijn32 = DEBRUIJN_TABLE_32) {
                var src_base = hc4.src_base;
                var nbAttempts = MAX_NB_ATTEMPTS;
                var delta = (int)(src_p - startLimit);

                // First Match
                LZ4HC_Insert_32(hc4, src_p);
                var xxx_ref = (hashTable[((((*(uint*)(src_p))) * 2654435761u) >> HASHHC_ADJUST)] + src_base);

                while ((xxx_ref >= src_p - MAX_DISTANCE) && (nbAttempts != 0)) {
                    nbAttempts--;
                    if (*(startLimit + longest) == *(xxx_ref - delta + longest)) {
                        if ((*(uint*)(xxx_ref)) == (*(uint*)(src_p))) {
                            var reft = xxx_ref + MINMATCH;
                            var ipt = src_p + MINMATCH;
                            var startt = src_p;

                            while (ipt < src_LASTLITERALS - (STEPSIZE_32 - 1)) {
                                var diff = (*(int*)(reft)) ^ (*(int*)(ipt));
                                if (diff == 0) {
                                    ipt += STEPSIZE_32;
                                    reft += STEPSIZE_32;
                                    continue;
                                }
                                ipt += debruijn32[(((uint)((diff) & -(diff)) * 0x077CB531u)) >> 27];
                                goto _endCount;
                            }
                            if ((ipt < (src_LASTLITERALS - 1)) && ((*(ushort*)(reft)) == (*(ushort*)(ipt)))) {
                                ipt += 2;
                                reft += 2;
                            }
                            if ((ipt < src_LASTLITERALS) && (*reft == *ipt)) ipt++;
                        _endCount:
                            reft = xxx_ref;

                            while ((startt > startLimit) && (reft > hc4.src_base) && (startt[-1] == reft[-1])) {
                                startt--;
                                reft--;
                            }

                            if ((ipt - startt) > longest) {
                                longest = (int)(ipt - startt);
                                matchpos = reft;
                                startpos = startt;
                            }
                        }
                    }
                    xxx_ref = ((xxx_ref) - chainTable[((int)xxx_ref) & MAXD_MASK]);
                }

                return longest;
            }
        }

        private static unsafe int LZ4_encodeSequence_32(
            ref byte* src_p, ref byte* dst_p, ref byte* src_anchor, int matchLength, byte* xxx_ref, byte* dst_end) {
            int len;

            // Encode Literal length
            var length = (int)(src_p - src_anchor);
            var xxx_token = (dst_p)++;
            if ((dst_p + length + (2 + 1 + LASTLITERALS) + (length >> 8)) > dst_end) return 1; // Check output limit
            if (length >= RUN_MASK) {
                *xxx_token = (RUN_MASK << ML_BITS);
                len = length - RUN_MASK;
                for (; len > 254; len -= 255) *(dst_p)++ = 255;
                *(dst_p)++ = (byte)len;
            }
            else {
                *xxx_token = (byte)(length << ML_BITS);
            }

            // Copy Literals
            var _p = dst_p + (length);
            do {
                *(uint*)dst_p = *(uint*)src_anchor;
                dst_p += 4;
                src_anchor += 4;
                *(uint*)dst_p = *(uint*)src_anchor;
                dst_p += 4;
                src_anchor += 4;
            } while (dst_p < _p);
            dst_p = _p;

            // Encode Offset
            *(ushort*)dst_p = (ushort)(src_p - xxx_ref);
            dst_p += 2;

            // Encode MatchLength
            len = (matchLength - MINMATCH);
            if (dst_p + (1 + LASTLITERALS) + (length >> 8) > dst_end) return 1; // Check output limit
            if (len >= ML_MASK) {
                *xxx_token += ML_MASK;
                len -= ML_MASK;
                for (; len > 509; len -= 510) {
                    *(dst_p)++ = 255;
                    *(dst_p)++ = 255;
                }
                if (len > 254) {
                    len -= 255;
                    *(dst_p)++ = 255;
                }
                *(dst_p)++ = (byte)len;
            }
            else {
                *xxx_token += (byte)len;
            }

            // Prepare next loop
            src_p += matchLength;
            src_anchor = src_p;

            return 0;
        }

        private static unsafe int LZ4_compressHCCtx_32(
            LZ4HC_Data_Structure ctx,
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            var src_p = src;
            var src_anchor = src_p;
            var src_end = src_p + src_len;
            var src_mflimit = src_end - MFLIMIT;
            var src_LASTLITERALS = (src_end - LASTLITERALS);

            var dst_p = dst;
            var dst_end = dst_p + dst_maxlen;

            byte* xxx_ref = null;
            byte* start2 = null;
            byte* ref2 = null;
            byte* start3 = null;
            byte* ref3 = null;

            src_p++;

            // Main Loop
            while (src_p < src_mflimit) {
                var ml = LZ4HC_InsertAndFindBestMatch_32(ctx, src_p, src_LASTLITERALS, ref xxx_ref);
                if (ml == 0) {
                    src_p++;
                    continue;
                }

                // saved, in case we would skip too much
                var start0 = src_p;
                var ref0 = xxx_ref;
                var ml0 = ml;

            _Search2:
                var ml2 = src_p + ml < src_mflimit
                    ? LZ4HC_InsertAndGetWiderMatch_32(ctx, src_p + ml - 2, src_p + 1, src_LASTLITERALS, ml, ref ref2, ref start2)
                    : ml;

                if (ml2 == ml) // No better match
				{
                    if (LZ4_encodeSequence_32(ref src_p, ref dst_p, ref src_anchor, ml, xxx_ref, dst_end) != 0) return 0;
                    continue;
                }

                if (start0 < src_p) {
                    if (start2 < src_p + ml0) // empirical
					{
                        src_p = start0;
                        xxx_ref = ref0;
                        ml = ml0;
                    }
                }

                // Here, start0==ip
                if ((start2 - src_p) < 3) // First Match too small : removed
				{
                    ml = ml2;
                    src_p = start2;
                    xxx_ref = ref2;
                    goto _Search2;
                }

            _Search3:
                // Currently we have :
                // ml2 > ml1, and
                // ip1+3 <= ip2 (usually < ip1+ml1)
                if ((start2 - src_p) < OPTIMAL_ML) {
                    var new_ml = ml;
                    if (new_ml > OPTIMAL_ML) new_ml = OPTIMAL_ML;
                    if (src_p + new_ml > start2 + ml2 - MINMATCH) new_ml = (int)(start2 - src_p) + ml2 - MINMATCH;
                    var correction = new_ml - (int)(start2 - src_p);
                    if (correction > 0) {
                        start2 += correction;
                        ref2 += correction;
                        ml2 -= correction;
                    }
                }
                // Now, we have start2 = ip+new_ml, with new_ml=min(ml, OPTIMAL_ML=18)

                var ml3 = start2 + ml2 < src_mflimit
                    ? LZ4HC_InsertAndGetWiderMatch_32(ctx, start2 + ml2 - 3, start2, src_LASTLITERALS, ml2, ref ref3, ref start3)
                    : ml2;

                if (ml3 == ml2) // No better match : 2 sequences to encode
				{
                    // ip & ref are known; Now for ml
                    if (start2 < src_p + ml) ml = (int)(start2 - src_p);
                    // Now, encode 2 sequences
                    if (LZ4_encodeSequence_32(ref src_p, ref dst_p, ref src_anchor, ml, xxx_ref, dst_end) != 0) return 0;
                    src_p = start2;
                    if (LZ4_encodeSequence_32(ref src_p, ref dst_p, ref src_anchor, ml2, ref2, dst_end) != 0) return 0;
                    continue;
                }

                if (start3 < src_p + ml + 3) // Not enough space for match 2 : remove it
				{
                    if (start3 >= (src_p + ml)) // can write Seq1 immediately ==> Seq2 is removed, so Seq3 becomes Seq1
					{
                        if (start2 < src_p + ml) {
                            var correction = (int)(src_p + ml - start2);
                            start2 += correction;
                            ref2 += correction;
                            ml2 -= correction;
                            if (ml2 < MINMATCH) {
                                start2 = start3;
                                ref2 = ref3;
                                ml2 = ml3;
                            }
                        }

                        if (LZ4_encodeSequence_32(ref src_p, ref dst_p, ref src_anchor, ml, xxx_ref, dst_end) != 0) return 0;
                        src_p = start3;
                        xxx_ref = ref3;
                        ml = ml3;

                        start0 = start2;
                        ref0 = ref2;
                        ml0 = ml2;
                        goto _Search2;
                    }

                    start2 = start3;
                    ref2 = ref3;
                    ml2 = ml3;
                    goto _Search3;
                }

                // OK, now we have 3 ascending matches; let's write at least the first one
                // ip & ref are known; Now for ml
                if (start2 < src_p + ml) {
                    if ((start2 - src_p) < ML_MASK) {
                        if (ml > OPTIMAL_ML) ml = OPTIMAL_ML;
                        if (src_p + ml > start2 + ml2 - MINMATCH) ml = (int)(start2 - src_p) + ml2 - MINMATCH;
                        var correction = ml - (int)(start2 - src_p);
                        if (correction > 0) {
                            start2 += correction;
                            ref2 += correction;
                            ml2 -= correction;
                        }
                    }
                    else {
                        ml = (int)(start2 - src_p);
                    }
                }
                if (LZ4_encodeSequence_32(ref src_p, ref dst_p, ref src_anchor, ml, xxx_ref, dst_end) != 0) return 0;

                src_p = start2;
                xxx_ref = ref2;
                ml = ml2;

                start2 = start3;
                ref2 = ref3;
                ml2 = ml3;

                goto _Search3;
            }

            // Encode Last Literals
            {
                var lastRun = (int)(src_end - src_anchor);
                if ((dst_p - dst) + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > (uint)dst_maxlen) return 0; // Check output limit
                if (lastRun >= RUN_MASK) {
                    *dst_p++ = (RUN_MASK << ML_BITS);
                    lastRun -= RUN_MASK;
                    for (; lastRun > 254; lastRun -= 255) *dst_p++ = 255;
                    *dst_p++ = (byte)lastRun;
                }
                else *dst_p++ = (byte)(lastRun << ML_BITS);
                BlockCopy(src_anchor, dst_p, (int)(src_end - src_anchor));
                dst_p += src_end - src_anchor;
            }

            // End
            return (int)((dst_p) - dst);
        }

        #region LZ4_compressCtx_64

        private static unsafe int LZ4_compressCtx_64(
            uint* hash_table,
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            byte* _p;

            fixed (int* debruijn64 = &DEBRUIJN_TABLE_64[0]) {
                // r93
                var src_p = src;
                var src_base = src_p;
                var src_anchor = src_p;
                var src_end = src_p + src_len;
                var src_mflimit = src_end - MFLIMIT;

                var dst_p = dst;
                var dst_end = dst_p + dst_maxlen;

                var src_LASTLITERALS = src_end - LASTLITERALS;
                var src_LASTLITERALS_1 = src_LASTLITERALS - 1;

                var src_LASTLITERALS_3 = src_LASTLITERALS - 3;
                var src_LASTLITERALS_STEPSIZE_1 = src_LASTLITERALS - (STEPSIZE_64 - 1);
                var dst_LASTLITERALS_1 = dst_end - (1 + LASTLITERALS);
                var dst_LASTLITERALS_3 = dst_end - (2 + 1 + LASTLITERALS);

                int length;
                uint h, h_fwd;

                // Init
                if (src_len < MINLENGTH) goto _last_literals;

                // First Byte
                hash_table[((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST)] = (uint)(src_p - src_base);
                src_p++;
                h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST);

                // Main Loop
                while (true) {
                    var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
                    var src_p_fwd = src_p;
                    byte* src_ref;
                    byte* dst_token;

                    // Find a match
                    do {
                        h = h_fwd;
                        var step = findMatchAttempts++ >> SKIPSTRENGTH;
                        src_p = src_p_fwd;
                        src_p_fwd = src_p + step;

                        if (src_p_fwd > src_mflimit) goto _last_literals;

                        h_fwd = ((((*(uint*)(src_p_fwd))) * 2654435761u) >> HASH_ADJUST);
                        src_ref = src_base + hash_table[h];
                        hash_table[h] = (uint)(src_p - src_base);
                    } while ((src_ref < src_p - MAX_DISTANCE) || ((*(uint*)(src_ref)) != (*(uint*)(src_p))));

                    // Catch up
                    while ((src_p > src_anchor) && (src_ref > src) && (src_p[-1] == src_ref[-1])) {
                        src_p--;
                        src_ref--;
                    }

                    // Encode Literal length
                    length = (int)(src_p - src_anchor);
                    dst_token = dst_p++;

                    if (dst_p + length + (length >> 8) > dst_LASTLITERALS_3) return 0; // Check output limit

                    if (length >= RUN_MASK) {
                        var len = length - RUN_MASK;
                        *dst_token = (RUN_MASK << ML_BITS);
                        if (len > 254) {
                            do {
                                *dst_p++ = 255;
                                len -= 255;
                            } while (len > 254);
                            *dst_p++ = (byte)len;
                            BlockCopy(src_anchor, dst_p, (length));
                            dst_p += length;
                            goto _next_match;
                        }
                        *dst_p++ = (byte)len;
                    }
                    else {
                        *dst_token = (byte)(length << ML_BITS);
                    }

                    // Copy Literals
                    _p = dst_p + (length);
                    {
                        do {
                            *(ulong*)dst_p = *(ulong*)src_anchor;
                            dst_p += 8;
                            src_anchor += 8;
                        } while (dst_p < _p);
                    }
                    dst_p = _p;

                _next_match:

                    // Encode Offset
                    *(ushort*)dst_p = (ushort)(src_p - src_ref);
                    dst_p += 2;

                    // Start Counting
                    src_p += MINMATCH;
                    src_ref += MINMATCH; // MinMatch already verified
                    src_anchor = src_p;

                    while (src_p < src_LASTLITERALS_STEPSIZE_1) {
                        var diff = (*(long*)(src_ref)) ^ (*(long*)(src_p));
                        if (diff == 0) {
                            src_p += STEPSIZE_64;
                            src_ref += STEPSIZE_64;
                            continue;
                        }
                        src_p += debruijn64[(((ulong)((diff) & -(diff)) * 0x0218A392CDABBD3FL)) >> 58];
                        goto _endCount;
                    }

                    if ((src_p < src_LASTLITERALS_3) && ((*(uint*)(src_ref)) == (*(uint*)(src_p)))) {
                        src_p += 4;
                        src_ref += 4;
                    }
                    if ((src_p < src_LASTLITERALS_1) && ((*(ushort*)(src_ref)) == (*(ushort*)(src_p)))) {
                        src_p += 2;
                        src_ref += 2;
                    }
                    if ((src_p < src_LASTLITERALS) && (*src_ref == *src_p)) src_p++;

                _endCount:

                    // Encode MatchLength
                    length = (int)(src_p - src_anchor);

                    if (dst_p + (length >> 8) > dst_LASTLITERALS_1) return 0; // Check output limit

                    if (length >= ML_MASK) {
                        *dst_token += ML_MASK;
                        length -= ML_MASK;
                        for (; length > 509; length -= 510) {
                            *dst_p++ = 255;
                            *dst_p++ = 255;
                        }
                        if (length > 254) {
                            length -= 255;
                            *dst_p++ = 255;
                        }
                        *dst_p++ = (byte)length;
                    }
                    else {
                        *dst_token += (byte)length;
                    }

                    // Test end of chunk
                    if (src_p > src_mflimit) {
                        src_anchor = src_p;
                        break;
                    }

                    // Fill table
                    hash_table[((((*(uint*)(src_p - 2))) * 2654435761u) >> HASH_ADJUST)] = (uint)(src_p - 2 - src_base);

                    // Test next position

                    h = ((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST);
                    src_ref = src_base + hash_table[h];
                    hash_table[h] = (uint)(src_p - src_base);

                    if ((src_ref > src_p - (MAX_DISTANCE + 1)) && ((*(uint*)(src_ref)) == (*(uint*)(src_p)))) {
                        dst_token = dst_p++;
                        *dst_token = 0;
                        goto _next_match;
                    }

                    // Prepare next loop
                    src_anchor = src_p++;
                    h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH_ADJUST);
                }

            _last_literals:

                // Encode Last Literals
                var lastRun = (int)(src_end - src_anchor);
                if (dst_p + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > dst_end) return 0;
                if (lastRun >= RUN_MASK) {
                    *dst_p++ = (RUN_MASK << ML_BITS);
                    lastRun -= RUN_MASK;
                    for (; lastRun > 254; lastRun -= 255) *dst_p++ = 255;
                    *dst_p++ = (byte)lastRun;
                }
                else *dst_p++ = (byte)(lastRun << ML_BITS);
                BlockCopy(src_anchor, dst_p, (int)(src_end - src_anchor));
                dst_p += src_end - src_anchor;

                // End
                return (int)(dst_p - dst);
            }
        }

        #endregion

        #region LZ4_compress64kCtx_64

        private static unsafe int LZ4_compress64kCtx_64(
            ushort* hash_table,
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            byte* _p;

            fixed (int* debruijn64 = &DEBRUIJN_TABLE_64[0]) {
                // r93
                var src_p = src;
                var src_anchor = src_p;
                var src_base = src_p;
                var src_end = src_p + src_len;
                var src_mflimit = src_end - MFLIMIT;

                var dst_p = dst;
                var dst_end = dst_p + dst_maxlen;

                var src_LASTLITERALS = src_end - LASTLITERALS;
                var src_LASTLITERALS_1 = src_LASTLITERALS - 1;

                var src_LASTLITERALS_3 = src_LASTLITERALS - 3;

                var src_LASTLITERALS_STEPSIZE_1 = src_LASTLITERALS - (STEPSIZE_64 - 1);
                var dst_LASTLITERALS_1 = dst_end - (1 + LASTLITERALS);
                var dst_LASTLITERALS_3 = dst_end - (2 + 1 + LASTLITERALS);

                int len, length;

                uint h, h_fwd;

                // Init
                if (src_len < MINLENGTH) goto _last_literals;

                // First Byte
                src_p++;
                h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH64K_ADJUST);

                // Main Loop
                while (true) {
                    var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
                    var src_p_fwd = src_p;
                    byte* src_ref;
                    byte* dst_token;

                    // Find a match
                    do {
                        h = h_fwd;
                        var step = findMatchAttempts++ >> SKIPSTRENGTH;
                        src_p = src_p_fwd;
                        src_p_fwd = src_p + step;

                        if (src_p_fwd > src_mflimit) goto _last_literals;

                        h_fwd = ((((*(uint*)(src_p_fwd))) * 2654435761u) >> HASH64K_ADJUST);
                        src_ref = src_base + hash_table[h];
                        hash_table[h] = (ushort)(src_p - src_base);
                    } while ((*(uint*)(src_ref)) != (*(uint*)(src_p)));

                    // Catch up
                    while ((src_p > src_anchor) && (src_ref > src) && (src_p[-1] == src_ref[-1])) {
                        src_p--;
                        src_ref--;
                    }

                    // Encode Literal length
                    length = (int)(src_p - src_anchor);
                    dst_token = dst_p++;

                    if (dst_p + length + (length >> 8) > dst_LASTLITERALS_3) return 0; // Check output limit

                    if (length >= RUN_MASK) {
                        len = length - RUN_MASK;
                        *dst_token = (RUN_MASK << ML_BITS);
                        if (len > 254) {
                            do {
                                *dst_p++ = 255;
                                len -= 255;
                            } while (len > 254);
                            *dst_p++ = (byte)len;
                            BlockCopy(src_anchor, dst_p, (length));
                            dst_p += length;
                            goto _next_match;
                        }
                        *dst_p++ = (byte)len;
                    }
                    else {
                        *dst_token = (byte)(length << ML_BITS);
                    }

                    // Copy Literals
                    {
                        _p = dst_p + (length);
                        {
                            do {
                                *(ulong*)dst_p = *(ulong*)src_anchor;
                                dst_p += 8;
                                src_anchor += 8;
                            } while (dst_p < _p);
                        }
                        dst_p = _p;
                    }

                _next_match:

                    // Encode Offset
                    *(ushort*)dst_p = (ushort)(src_p - src_ref);
                    dst_p += 2;

                    // Start Counting
                    src_p += MINMATCH;
                    src_ref += MINMATCH; // MinMatch verified
                    src_anchor = src_p;

                    while (src_p < src_LASTLITERALS_STEPSIZE_1) {
                        var diff = (*(long*)(src_ref)) ^ (*(long*)(src_p));
                        if (diff == 0) {
                            src_p += STEPSIZE_64;
                            src_ref += STEPSIZE_64;
                            continue;
                        }
                        src_p += debruijn64[(((ulong)((diff) & -(diff)) * 0x0218A392CDABBD3FL)) >> 58];
                        goto _endCount;
                    }

                    if ((src_p < src_LASTLITERALS_3) && ((*(uint*)(src_ref)) == (*(uint*)(src_p)))) {
                        src_p += 4;
                        src_ref += 4;
                    }
                    if ((src_p < src_LASTLITERALS_1) && ((*(ushort*)(src_ref)) == (*(ushort*)(src_p)))) {
                        src_p += 2;
                        src_ref += 2;
                    }
                    if ((src_p < src_LASTLITERALS) && (*src_ref == *src_p)) src_p++;

                _endCount:

                    // Encode MatchLength
                    len = (int)(src_p - src_anchor);

                    if (dst_p + (len >> 8) > dst_LASTLITERALS_1) return 0; // Check output limit

                    if (len >= ML_MASK) {
                        *dst_token += ML_MASK;
                        len -= ML_MASK;
                        for (; len > 509; len -= 510) {
                            *dst_p++ = 255;
                            *dst_p++ = 255;
                        }
                        if (len > 254) {
                            len -= 255;
                            *dst_p++ = 255;
                        }
                        *dst_p++ = (byte)len;
                    }
                    else {
                        *dst_token += (byte)len;
                    }

                    // Test end of chunk
                    if (src_p > src_mflimit) {
                        src_anchor = src_p;
                        break;
                    }

                    // Fill table
                    hash_table[((((*(uint*)(src_p - 2))) * 2654435761u) >> HASH64K_ADJUST)] = (ushort)(src_p - 2 - src_base);

                    // Test next position

                    h = ((((*(uint*)(src_p))) * 2654435761u) >> HASH64K_ADJUST);
                    src_ref = src_base + hash_table[h];
                    hash_table[h] = (ushort)(src_p - src_base);

                    if ((*(uint*)(src_ref)) == (*(uint*)(src_p))) {
                        dst_token = dst_p++;
                        *dst_token = 0;
                        goto _next_match;
                    }

                    // Prepare next loop
                    src_anchor = src_p++;
                    h_fwd = ((((*(uint*)(src_p))) * 2654435761u) >> HASH64K_ADJUST);
                }

            _last_literals:

                // Encode Last Literals
                var lastRun = (int)(src_end - src_anchor);
                if (dst_p + lastRun + 1 + (lastRun - RUN_MASK + 255) / 255 > dst_end) return 0;
                if (lastRun >= RUN_MASK) {
                    *dst_p++ = (RUN_MASK << ML_BITS);
                    lastRun -= RUN_MASK;
                    for (; lastRun > 254; lastRun -= 255) *dst_p++ = 255;
                    *dst_p++ = (byte)lastRun;
                }
                else *dst_p++ = (byte)(lastRun << ML_BITS);
                BlockCopy(src_anchor, dst_p, (int)(src_end - src_anchor));
                dst_p += src_end - src_anchor;

                // End
                return (int)(dst_p - dst);
            }
        }

        #endregion

        #region LZ4_uncompress_64

        private static unsafe int LZ4_uncompress_64(
            byte* src,
            byte* dst,
            int dst_len) {
            fixed (int* dec32table = &DECODER_TABLE_32[0])
            fixed (int* dec64table = &DECODER_TABLE_64[0]) {
                // r93
                var src_p = src;
                byte* dst_ref;

                var dst_p = dst;
                var dst_end = dst_p + dst_len;
                byte* dst_cpy;

                var dst_LASTLITERALS = dst_end - LASTLITERALS;
                var dst_COPYLENGTH = dst_end - COPYLENGTH;
                var dst_COPYLENGTH_STEPSIZE_4 = dst_end - COPYLENGTH - (STEPSIZE_64 - 4);

                byte token;

                // Main Loop
                while (true) {
                    int length;

                    // get runlength
                    token = *src_p++;
                    if ((length = (token >> ML_BITS)) == RUN_MASK) {
                        int len;
                        for (; (len = *src_p++) == 255; length += 255) {
                            /* do nothing */
                        }
                        length += len;
                    }

                    // copy literals
                    dst_cpy = dst_p + length;

                    if (dst_cpy > dst_COPYLENGTH) {
                        if (dst_cpy != dst_end) goto _output_error; // Error : not enough place for another match (min 4) + 5 literals
                        BlockCopy(src_p, dst_p, (length));
                        src_p += length;
                        break; // EOF
                    }
                    do {
                        *(ulong*)dst_p = *(ulong*)src_p;
                        dst_p += 8;
                        src_p += 8;
                    } while (dst_p < dst_cpy);
                    src_p -= (dst_p - dst_cpy);
                    dst_p = dst_cpy;

                    // get offset
                    dst_ref = (dst_cpy) - (*(ushort*)(src_p));
                    src_p += 2;
                    if (dst_ref < dst) goto _output_error; // Error : offset outside destination buffer

                    // get matchlength
                    if ((length = (token & ML_MASK)) == ML_MASK) {
                        for (; *src_p == 255; length += 255) src_p++;
                        length += *src_p++;
                    }

                    // copy repeated sequence
                    if ((dst_p - dst_ref) < STEPSIZE_64) {
                        var dec64 = dec64table[dst_p - dst_ref];

                        dst_p[0] = dst_ref[0];
                        dst_p[1] = dst_ref[1];
                        dst_p[2] = dst_ref[2];
                        dst_p[3] = dst_ref[3];
                        dst_p += 4;
                        dst_ref += 4;
                        dst_ref -= dec32table[dst_p - dst_ref];
                        (*(uint*)(dst_p)) = (*(uint*)(dst_ref));
                        dst_p += STEPSIZE_64 - 4;
                        dst_ref -= dec64;
                    }
                    else {
                        *(ulong*)dst_p = *(ulong*)dst_ref;
                        dst_p += 8;
                        dst_ref += 8;
                    }
                    dst_cpy = dst_p + length - (STEPSIZE_64 - 4);

                    if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4) {
                        if (dst_cpy > dst_LASTLITERALS) goto _output_error; // Error : last 5 bytes must be literals
                        while (dst_p < dst_COPYLENGTH) {
                            *(ulong*)dst_p = *(ulong*)dst_ref;
                            dst_p += 8;
                            dst_ref += 8;
                        }

                        while (dst_p < dst_cpy) *dst_p++ = *dst_ref++;
                        dst_p = dst_cpy;
                        continue;
                    }

                    {
                        do {
                            *(ulong*)dst_p = *(ulong*)dst_ref;
                            dst_p += 8;
                            dst_ref += 8;
                        } while (dst_p < dst_cpy);
                    }
                    dst_p = dst_cpy; // correction
                }

                // end of decoding
                return (int)((src_p) - src);

                                                               // write overflow error detected
            _output_error:
                return (int)(-((src_p) - src));
            }
        }

        #endregion

        #region LZ4_uncompress_unknownOutputSize_64

        private static unsafe int LZ4_uncompress_unknownOutputSize_64(
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            fixed (int* dec32table = &DECODER_TABLE_32[0])
            fixed (int* dec64table = &DECODER_TABLE_64[0]) {
                // r93
                var src_p = src;
                var src_end = src_p + src_len;
                byte* dst_ref;

                var dst_p = dst;
                var dst_end = dst_p + dst_maxlen;
                byte* dst_cpy;

                var src_LASTLITERALS_3 = (src_end - (2 + 1 + LASTLITERALS));
                var src_LASTLITERALS_1 = (src_end - (LASTLITERALS + 1));
                var dst_COPYLENGTH = (dst_end - COPYLENGTH);
                var dst_COPYLENGTH_STEPSIZE_4 = (dst_end - (COPYLENGTH + (STEPSIZE_64 - 4)));
                var dst_LASTLITERALS = (dst_end - LASTLITERALS);
                var dst_MFLIMIT = (dst_end - MFLIMIT);

                // Special case
                if (src_p == src_end) goto _output_error; // A correctly formed null-compressed LZ4 must have at least one byte (token=0)

                // Main Loop
                while (true) {
                    byte token;
                    int length;

                    // get runlength
                    token = *src_p++;
                    if ((length = (token >> ML_BITS)) == RUN_MASK) {
                        var s = 255;
                        while ((src_p < src_end) && (s == 255)) {
                            s = *src_p++;
                            length += s;
                        }
                    }

                    // copy literals
                    dst_cpy = dst_p + length;

                    if ((dst_cpy > dst_MFLIMIT) || (src_p + length > src_LASTLITERALS_3)) {
                        if (dst_cpy > dst_end)
                            goto _output_error; // Error : writes beyond output buffer
                        if (src_p + length != src_end)
                            goto _output_error; // Error : LZ4 format requires to consume all input at this stage (no match within the last 11 bytes, and at least 8 remaining input bytes for another match+literals)
                        BlockCopy(src_p, dst_p, (length));
                        dst_p += length;
                        break; // Necessarily EOF, due to parsing restrictions
                    }
                    do {
                        *(ulong*)dst_p = *(ulong*)src_p;
                        dst_p += 8;
                        src_p += 8;
                    } while (dst_p < dst_cpy);
                    src_p -= (dst_p - dst_cpy);
                    dst_p = dst_cpy;

                    // get offset
                    dst_ref = (dst_cpy) - (*(ushort*)(src_p));
                    src_p += 2;
                    if (dst_ref < dst) {
                        goto _output_error; // Error : offset outside of destination buffer
                    }

                    // get matchlength
                    if ((length = (token & ML_MASK)) == ML_MASK) {
                        while (src_p < src_LASTLITERALS_1) // Error : a minimum input bytes must remain for LASTLITERALS + token
						{
                            int s = *src_p++;
                            length += s;
                            if (s == 255) continue;
                            break;
                        }
                    }

                    // copy repeated sequence
                    if (dst_p - dst_ref < STEPSIZE_64) {
                        var dec64 = dec64table[dst_p - dst_ref];

                        dst_p[0] = dst_ref[0];
                        dst_p[1] = dst_ref[1];
                        dst_p[2] = dst_ref[2];
                        dst_p[3] = dst_ref[3];
                        dst_p += 4;
                        dst_ref += 4;
                        dst_ref -= dec32table[dst_p - dst_ref];
                        (*(uint*)(dst_p)) = (*(uint*)(dst_ref));
                        dst_p += STEPSIZE_64 - 4;
                        dst_ref -= dec64;
                    }
                    else {
                        *(ulong*)dst_p = *(ulong*)dst_ref;
                        dst_p += 8;
                        dst_ref += 8;
                    }
                    dst_cpy = dst_p + length - (STEPSIZE_64 - 4);

                    if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4) {
                        if (dst_cpy > dst_LASTLITERALS) {
                            goto _output_error; // Error : last 5 bytes must be literals
                        }
                        while (dst_p < dst_COPYLENGTH) {
                            *(ulong*)dst_p = *(ulong*)dst_ref;
                            dst_p += 8;
                            dst_ref += 8;
                        }

                        while (dst_p < dst_cpy) *dst_p++ = *dst_ref++;
                        dst_p = dst_cpy;
                        continue;
                    }

                    do {
                        *(ulong*)dst_p = *(ulong*)dst_ref;
                        dst_p += 8;
                        dst_ref += 8;
                    } while (dst_p < dst_cpy);
                    dst_p = dst_cpy; // correction
                }

                // end of decoding
                return (int)(dst_p - dst);

            _output_error:

                // write overflow error detected
                return (int)-(src_p - src);
            }
        }

        #endregion

        // Update chains up to ip (excluded)
        private static unsafe void LZ4HC_Insert_64(LZ4HC_Data_Structure hc4, byte* src_p) {
            fixed (ushort* chainTable = hc4.chainTable)
            fixed (int* hashTable = hc4.hashTable) {
                var src_base = hc4.src_base;
                while (hc4.nextToUpdate < src_p) {
                    var p = hc4.nextToUpdate;
                    var delta = (int)((p) - (hashTable[((((*(uint*)(p))) * 2654435761u) >> HASHHC_ADJUST)] + src_base));
                    if (delta > MAX_DISTANCE) delta = MAX_DISTANCE;
                    chainTable[((int)p) & MAXD_MASK] = (ushort)delta;
                    hashTable[((((*(uint*)(p))) * 2654435761u) >> HASHHC_ADJUST)] = (int)(p - src_base);
                    hc4.nextToUpdate++;
                }
            }
        }

        private static unsafe int LZ4HC_CommonLength_64(byte* p1, byte* p2, byte* src_LASTLITERALS) {
            fixed (int* debruijn64 = DEBRUIJN_TABLE_64) {
                var p1t = p1;

                while (p1t < src_LASTLITERALS - (STEPSIZE_64 - 1)) {
                    var diff = (*(long*)(p2)) ^ (*(long*)(p1t));
                    if (diff == 0) {
                        p1t += STEPSIZE_64;
                        p2 += STEPSIZE_64;
                        continue;
                    }
                    p1t += debruijn64[(((ulong)((diff) & -(diff)) * 0x0218A392CDABBD3FL)) >> 58];
                    return (int)(p1t - p1);
                }
                if ((p1t < (src_LASTLITERALS - 3)) && ((*(uint*)(p2)) == (*(uint*)(p1t)))) {
                    p1t += 4;
                    p2 += 4;
                }
                if ((p1t < (src_LASTLITERALS - 1)) && ((*(ushort*)(p2)) == (*(ushort*)(p1t)))) {
                    p1t += 2;
                    p2 += 2;
                }
                if ((p1t < src_LASTLITERALS) && (*p2 == *p1t)) p1t++;
                return (int)(p1t - p1);
            }
        }

        private static unsafe int LZ4HC_InsertAndFindBestMatch_64(
            LZ4HC_Data_Structure hc4, byte* src_p, byte* src_LASTLITERALS, ref byte* matchpos) {
            fixed (ushort* chainTable = hc4.chainTable)
            fixed (int* hashTable = hc4.hashTable) {
                var src_base = hc4.src_base;
                var nbAttempts = MAX_NB_ATTEMPTS;
                int repl = 0, ml = 0;
                ushort delta = 0;

                // HC4 match finder
                LZ4HC_Insert_64(hc4, src_p);
                var src_ref = (hashTable[((((*(uint*)(src_p))) * 2654435761u) >> HASHHC_ADJUST)] + src_base);

                // Detect repetitive sequences of length <= 4
                if (src_ref >= src_p - 4) // potential repetition
				{
                    if ((*(uint*)(src_ref)) == (*(uint*)(src_p))) // confirmed
					{
                        delta = (ushort)(src_p - src_ref);
                        repl = ml = LZ4HC_CommonLength_64(src_p + MINMATCH, src_ref + MINMATCH, src_LASTLITERALS) + MINMATCH;
                        matchpos = src_ref;
                    }
                    src_ref = ((src_ref) - chainTable[((int)src_ref) & MAXD_MASK]);
                }

                while ((src_ref >= src_p - MAX_DISTANCE) && (nbAttempts != 0)) {
                    nbAttempts--;
                    if (*(src_ref + ml) == *(src_p + ml))
                        if ((*(uint*)(src_ref)) == (*(uint*)(src_p))) {
                            var mlt = LZ4HC_CommonLength_64(src_p + MINMATCH, src_ref + MINMATCH, src_LASTLITERALS) + MINMATCH;
                            if (mlt > ml) {
                                ml = mlt;
                                matchpos = src_ref;
                            }
                        }
                    src_ref = ((src_ref) - chainTable[((int)src_ref) & MAXD_MASK]);
                }

                // Complete table
                if (repl != 0) {
                    var ptr = src_p;

                    var end = src_p + repl - (MINMATCH - 1);
                    while (ptr < end - delta) {
                        chainTable[((int)ptr) & MAXD_MASK] = delta; // Pre-Load
                        ptr++;
                    }
                    do {
                        chainTable[((int)ptr) & MAXD_MASK] = delta;
                        hashTable[((((*(uint*)(ptr))) * 2654435761u) >> HASHHC_ADJUST)] = (int)(ptr - src_base); // Head of chain
                        ptr++;
                    } while (ptr < end);
                    hc4.nextToUpdate = end;
                }

                return ml;
            }
        }

        private static unsafe int LZ4HC_InsertAndGetWiderMatch_64(
            LZ4HC_Data_Structure hc4, byte* src_p, byte* startLimit, byte* src_LASTLITERALS, int longest,
            ref byte* matchpos, ref byte* startpos) {
            fixed (ushort* chainTable = hc4.chainTable)
            fixed (int* hashTable = hc4.hashTable)
            fixed (int* debruijn64 = DEBRUIJN_TABLE_64) {
                var src_base = hc4.src_base;
                var nbAttempts = MAX_NB_ATTEMPTS;
                var delta = (int)(src_p - startLimit);

                // First Match
                LZ4HC_Insert_64(hc4, src_p);
                var src_ref = (hashTable[((((*(uint*)(src_p))) * 2654435761u) >> HASHHC_ADJUST)] + src_base);

                while ((src_ref >= src_p - MAX_DISTANCE) && (nbAttempts != 0)) {
                    nbAttempts--;
                    if (*(startLimit + longest) == *(src_ref - delta + longest)) {
                        if ((*(uint*)(src_ref)) == (*(uint*)(src_p))) {
                            var reft = src_ref + MINMATCH;
                            var ipt = src_p + MINMATCH;
                            var startt = src_p;

                            while (ipt < src_LASTLITERALS - (STEPSIZE_64 - 1)) {
                                var diff = (*(long*)(reft)) ^ (*(long*)(ipt));
                                if (diff == 0) {
                                    ipt += STEPSIZE_64;
                                    reft += STEPSIZE_64;
                                    continue;
                                }
                                ipt += debruijn64[(((ulong)((diff) & -(diff)) * 0x0218A392CDABBD3FL)) >> 58];
                                goto _endCount;
                            }
                            if ((ipt < (src_LASTLITERALS - 3)) && ((*(uint*)(reft)) == (*(uint*)(ipt)))) {
                                ipt += 4;
                                reft += 4;
                            }
                            if ((ipt < (src_LASTLITERALS - 1)) && ((*(ushort*)(reft)) == (*(ushort*)(ipt)))) {
                                ipt += 2;
                                reft += 2;
                            }
                            if ((ipt < src_LASTLITERALS) && (*reft == *ipt)) ipt++;

                        _endCount:
                            reft = src_ref;

                            while ((startt > startLimit) && (reft > hc4.src_base) && (startt[-1] == reft[-1])) {
                                startt--;
                                reft--;
                            }

                            if ((ipt - startt) > longest) {
                                longest = (int)(ipt - startt);
                                matchpos = reft;
                                startpos = startt;
                            }
                        }
                    }
                    src_ref = ((src_ref) - chainTable[((int)src_ref) & MAXD_MASK]);
                }

                return longest;
            }
        }

        private static unsafe int LZ4_encodeSequence_64(
            ref byte* src_p, ref byte* dst_p, ref byte* src_anchor, int matchLength, byte* src_ref, byte* dst_end) {
            int len;
            var dst_token = (dst_p)++;

            // Encode Literal length
            var length = (int)(src_p - src_anchor);
            if ((dst_p + length + (2 + 1 + LASTLITERALS) + (length >> 8)) > dst_end) return 1; // Check output limit
            if (length >= RUN_MASK) {
                *dst_token = (RUN_MASK << ML_BITS);
                len = length - RUN_MASK;
                for (; len > 254; len -= 255) *(dst_p)++ = 255;
                *(dst_p)++ = (byte)len;
            }
            else {
                *dst_token = (byte)(length << ML_BITS);
            }

            // Copy Literals
            var _p = dst_p + (length);
            do {
                *(ulong*)dst_p = *(ulong*)src_anchor;
                dst_p += 8;
                src_anchor += 8;
            } while (dst_p < _p);
            dst_p = _p;

            // Encode Offset
            *(ushort*)dst_p = (ushort)(src_p - src_ref);
            dst_p += 2;

            // Encode MatchLength
            len = (matchLength - MINMATCH);
            if (dst_p + (1 + LASTLITERALS) + (length >> 8) > dst_end) return 1; // Check output limit
            if (len >= ML_MASK) {
                *dst_token += ML_MASK;
                len -= ML_MASK;
                for (; len > 509; len -= 510) {
                    *(dst_p)++ = 255;
                    *(dst_p)++ = 255;
                }
                if (len > 254) {
                    len -= 255;
                    *(dst_p)++ = 255;
                }
                *(dst_p)++ = (byte)len;
            }
            else {
                *dst_token += (byte)len;
            }

            // Prepare next loop
            src_p += matchLength;
            src_anchor = src_p;

            return 0;
        }

        private static unsafe int LZ4_compressHCCtx_64(
            LZ4HC_Data_Structure ctx,
            byte* src,
            byte* dst,
            int src_len,
            int dst_maxlen) {
            var src_p = src;
            var src_anchor = src_p;
            var src_end = src_p + src_len;
            var src_mflimit = src_end - MFLIMIT;
            var src_LASTLITERALS = (src_end - LASTLITERALS);

            var dst_p = dst;
            var dst_end = dst_p + dst_maxlen;

            byte* src_ref = null;
            byte* start2 = null;
            byte* ref2 = null;
            byte* start3 = null;
            byte* ref3 = null;

            src_p++;

            // Main Loop
            while (src_p < src_mflimit) {
                var ml = LZ4HC_InsertAndFindBestMatch_64(ctx, src_p, src_LASTLITERALS, ref src_ref);
                if (ml == 0) {
                    src_p++;
                    continue;
                }

                // saved, in case we would skip too much
                var start0 = src_p;
                var ref0 = src_ref;
                var ml0 = ml;

            _Search2:
                var ml2 = src_p + ml < src_mflimit
                    ? LZ4HC_InsertAndGetWiderMatch_64(ctx, src_p + ml - 2, src_p + 1, src_LASTLITERALS, ml, ref ref2, ref start2)
                    : ml;

                if (ml2 == ml) // No better match
				{
                    if (LZ4_encodeSequence_64(ref src_p, ref dst_p, ref src_anchor, ml, src_ref, dst_end) != 0) return 0;
                    continue;
                }

                if (start0 < src_p && start2 < src_p + ml0) {
                    src_p = start0;
                    src_ref = ref0;
                    ml = ml0;
                }

                // Here, start0==ip
                if ((start2 - src_p) < 3) // First Match too small : removed
				{
                    ml = ml2;
                    src_p = start2;
                    src_ref = ref2;
                    goto _Search2;
                }

            _Search3:
                // Currently we have :
                // ml2 > ml1, and
                // ip1+3 <= ip2 (usually < ip1+ml1)
                if ((start2 - src_p) < OPTIMAL_ML) {
                    var new_ml = ml;
                    if (new_ml > OPTIMAL_ML) new_ml = OPTIMAL_ML;
                    if (src_p + new_ml > start2 + ml2 - MINMATCH) new_ml = (int)(start2 - src_p) + ml2 - MINMATCH;
                    var correction = new_ml - (int)(start2 - src_p);
                    if (correction > 0) {
                        start2 += correction;
                        ref2 += correction;
                        ml2 -= correction;
                    }
                }

                // Now, we have start2 = ip+new_ml, with new_ml=min(ml, OPTIMAL_ML=18)

                var ml3 = start2 + ml2 < src_mflimit
                    ? LZ4HC_InsertAndGetWiderMatch_64(ctx, start2 + ml2 - 3, start2, src_LASTLITERALS, ml2, ref ref3, ref start3)
                    : ml2;

                if (ml3 == ml2) // No better match : 2 sequences to encode
				{
                    // ip & ref are known; Now for ml
                    if (start2 < src_p + ml) ml = (int)(start2 - src_p);

                    // Now, encode 2 sequences
                    if (LZ4_encodeSequence_64(ref src_p, ref dst_p, ref src_anchor, ml, src_ref, dst_end) != 0) return 0;
                    src_p = start2;
                    if (LZ4_encodeSequence_64(ref src_p, ref dst_p, ref src_anchor, ml2, ref2, dst_end) != 0) return 0;
                    continue;
                }

                if (start3 < src_p + ml + 3) // Not enough space for match 2 : remove it
				{
                    if (start3 >= src_p + ml) // can write Seq1 immediately ==> Seq2 is removed, so Seq3 becomes Seq1
					{
                        if (start2 < src_p + ml) {
                            var correction = (int)(src_p + ml - start2);
                            start2 += correction;
                            ref2 += correction;
                            ml2 -= correction;
                            if (ml2 < MINMATCH) {
                                start2 = start3;
                                ref2 = ref3;
                                ml2 = ml3;
                            }
                        }

                        if (LZ4_encodeSequence_64(ref src_p, ref dst_p, ref src_anchor, ml, src_ref, dst_end) != 0) return 0;
                        src_p = start3;
                        src_ref = ref3;
                        ml = ml3;

                        start0 = start2;
                        ref0 = ref2;
                        ml0 = ml2;
                        goto _Search2;
                    }

                    start2 = start3;
                    ref2 = ref3;
                    ml2 = ml3;
                    goto _Search3;
                }

                // OK, now we have 3 ascending matches; let's write at least the first one
                // ip & ref are known; Now for ml
                if (start2 < src_p + ml) {
                    if (start2 - src_p < ML_MASK) {
                        if (ml > OPTIMAL_ML) ml = OPTIMAL_ML;
                        if (src_p + ml > start2 + ml2 - MINMATCH) ml = (int)(start2 - src_p) + ml2 - MINMATCH;
                        var correction = ml - (int)(start2 - src_p);
                        if (correction > 0) {
                            start2 += correction;
                            ref2 += correction;
                            ml2 -= correction;
                        }
                    }
                    else {
                        ml = (int)(start2 - src_p);
                    }
                }

                if (LZ4_encodeSequence_64(ref src_p, ref dst_p, ref src_anchor, ml, src_ref, dst_end) != 0)
                    return 0;

                src_p = start2;
                src_ref = ref2;
                ml = ml2;

                start2 = start3;
                ref2 = ref3;
                ml2 = ml3;

                goto _Search3;
            }

            // Encode Last Literals
            var lastRun = (int)(src_end - src_anchor);
            if ((dst_p - dst) + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > (uint)dst_maxlen) return 0; // Check output limit
            if (lastRun >= RUN_MASK) {
                *dst_p++ = (RUN_MASK << ML_BITS);
                lastRun -= RUN_MASK;
                for (; lastRun > 254; lastRun -= 255) *dst_p++ = 255;
                *dst_p++ = (byte)lastRun;
            }
            else {
                *dst_p++ = (byte)(lastRun << ML_BITS);
            }
            BlockCopy(src_anchor, dst_p, (int)(src_end - src_anchor));
            dst_p += src_end - src_anchor;

            // End
            return (int)((dst_p) - dst);
        }

        /// <summary>Encodes the specified input using HC codec.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Buffer with compressed data (NOTE: it can be bigger than input).</returns>
        public static int EncodeHC(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength) {
            if (bitWidth == 4)
                return Encode32HC(input, inputOffset, inputLength, output, outputOffset, outputLength);
            else
                return Encode64HC(input, inputOffset, inputLength, output, outputOffset, outputLength);
        }

        /// <summary>Encodes the specified input using HC codec.</summary>
        /// <param name="input">The input.</param>
        /// <param name="inputOffset">The input offset.</param>
        /// <param name="inputLength">Length of the input.</param>
        /// <returns>Buffer with compressed data (NOTE: it can be bigger than input).</returns>
        public static int Encode(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength) {
            if (bitWidth == 4)
                return Encode32(input, inputOffset, inputLength, output, outputOffset, outputLength);
            else
                return Encode64(input, inputOffset, inputLength, output, outputOffset, outputLength);
        }

        public static unsafe int Decode(
            byte[] input,
            int inputOffset,
            int inputLength,
            byte[] output,
            int outputOffset,
            int outputLength,
            bool knownOutputLength) {

                if (bitWidth == 4)
                    return Decode32(input, inputOffset, inputLength, output, outputOffset, outputLength, knownOutputLength);
                else
                    return Decode64(input, inputOffset, inputLength, output, outputOffset, outputLength, knownOutputLength);
        }

        static LZ4Codec() {
            bitWidth = IntPtr.Size;
        }

        private static int bitWidth = -1;
    }
}
