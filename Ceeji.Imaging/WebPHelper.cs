using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Ceeji.Imaging {
    /// <summary>
    /// 提供对 WebP 格式的跨平台支持。
    /// </summary>
    public static class WebPHelper {
        [DllImport("libwebp", CallingConvention = CallingConvention.Cdecl)]
        static extern int WebPEncodeBGRA(IntPtr rgba, int width, int height, int stride,
                                         float quality_factor, out IntPtr output);
        [DllImportAttribute("libwebp", EntryPoint = "WebPSafeFree")]
        public static extern void WebPSafeFree(IntPtr toDeallocate);

        [DllImport("libwebp")]
        public static extern UIntPtr WebPDecodeBGRAInto(byte[] data, uint data_size, IntPtr output_buffer,
                                                        int output_buffer_size, int output_stride);

        [DllImport("libwebp")]
        public static extern int WebPGetInfo(byte[] data, uint data_size, ref int width, ref int height);


        /// <summary>
        /// 将指定的受支持图像数据转换为 WebP 流。
        /// </summary>
        /// <param name="input">输入流</param>
        /// <param name="output">输出流</param>
        /// <param name="quality">品质，0 - 100，或 -1 代表无损压缩。</param>
        public static byte[] Encode(byte[] input, int quality = 75) {
            using (var ms = new MemoryStream()) {
                Encode(new MemoryStream(input), ms, quality);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 将指定的受支持图像数据转换为 WebP 流。
        /// </summary>
        /// <param name="input">输入流</param>
        /// <param name="output">输出流</param>
        /// <param name="quality">品质，0 - 100，或 -1 代表无损压缩。</param>
        public static void Encode(Stream input, Stream output, int quality = 75) {
            Bitmap source;
            try {
                var pos = input.Position;
                source = new Bitmap(input);
            }
            catch (ArgumentException) {
                // 输入图片无法解析
                throw;
            }

            BitmapData data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try {
                IntPtr webp_data = IntPtr.Zero;

                try {
                    int size = WebPEncodeBGRA(data.Scan0, source.Width, source.Height, data.Stride, quality, out webp_data);

                    if (size == 0) throw new InvalidDataException("Can not convert from the specific stream to WebP: the libwebp return 0");

                    byte[] buffer = new byte[size];
                    Marshal.Copy(webp_data, buffer, 0, size);
                    output.Write(buffer, 0, buffer.Length);
                }
                finally {
                    if (webp_data != IntPtr.Zero) {
                        WebPSafeFree(webp_data);
                    }
                }
            }
            finally {
                source.UnlockBits(data);
                source.Dispose();
            }
        }

        /// <summary>
        /// 将 WebP 流转换为指定的图片格式。
        /// </summary>
        /// <param name="input">输入流</param>
        /// <param name="output">输出流</param>
        /// <param name="format">输出的图片格式</param>
        /// <param name="jpegQuality">品质，0 - 100，或 -1 代表无损压缩。仅在输出格式为 JPEG 时有效。</param>
        public static void Decode(Stream input, Stream output, ImageFormat format, int jpegQuality = 90) {
            input.Position = 0;
            byte[] raw_data = new byte[input.Length];
            input.Read(raw_data, 0, raw_data.Length);

            int width = 0;
            int height = 0;

            if (WebPGetInfo(raw_data, (uint)input.Length, ref width, ref height) == 0)
                throw new FormatException("Error loading file info");

            int stride = width * 4;
            int output_buffer_size = height * stride;

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            // byte[] output_buffer = new byte[output_buffer_size];
            try {
                var ret = WebPDecodeBGRAInto(raw_data, (uint)input.Length, data.Scan0, output_buffer_size, stride);

                if (ret == UIntPtr.Zero) throw new InvalidDataException("Can not convert from the specific stream: the libwebp return 0");

                bitmap.UnlockBits(data);
                data = null;

                ImageCodecInfo decoder = GetEncoder(format);

                if (decoder.FormatID == ImageFormat.Jpeg.Guid) {
                    System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;

                    EncoderParameters myEncoderParameters = new EncoderParameters(1);
                    EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 50L);
                    myEncoderParameters.Param[0] = myEncoderParameter;

                    bitmap.Save(output, decoder, myEncoderParameters);
                }
                else {
                    bitmap.Save(output, format);
                }
            }
            finally {
                if (data != null)
                    bitmap.UnlockBits(data);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format) {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs) {
                if (codec.FormatID == format.Guid) {
                    return codec;
                }
            }
            return null;
        }
    }
}
