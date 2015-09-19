using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ceeji.Network {
    /// <summary>
    /// 定义一种将二进制类型编码为另一种数据类型的解码器类型。其接受一种输入类型，并通过计算、截断、合并、处理等操作，生成新的类型。
    /// </summary>
    /// <typeparam name="T">生成的新类型。</typeparam>
    public abstract class DataDecoder<T> {
        /// <summary>
        /// 解码指定的二进制范围，并返回解码结果。
        /// </summary>
        /// <param name="input"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public abstract T Encode(byte[] input, int offset, int count);
    }

    /// <summary>
    /// 定义 CPK 解码器。
    /// </summary>
    public class CPKDecoder : DataDecoder<Ceeji.Data.BinaryPackage.CPKPackage> {
        /// <summary>
        /// 将二进制网络数据翻译为 CPK 高性能二进制包格式。
        /// </summary>
        /// <param name="input"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override Ceeji.Data.BinaryPackage.CPKPackage Encode(byte[] input, int offset, int count) {
            Ceeji.Data.BinaryPackage.CPKPackage p = new Data.BinaryPackage.CPKPackage(new MemoryStream(input, offset, count));
            return p;
        }
    }
}
