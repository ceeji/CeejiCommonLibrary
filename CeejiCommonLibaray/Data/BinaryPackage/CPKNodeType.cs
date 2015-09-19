using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Data.BinaryPackage {
    /// <summary>
    /// 代表 CPK 节点的类型标记。
    /// </summary>
    [Flags]
    public enum CPKValueType : uint {
        /// <summary>
        /// 无标记
        /// </summary>
        None = 0,
        /// <summary>
        /// 代表二进制数据
        /// </summary>
        Binary = 1 << 0,
        /// <summary>
        /// 代表文本数据
        /// </summary>
        String = 1 << 1,
        /// <summary>
        /// 代表 UTF-8 格式编码的数据
        /// </summary>
        EncodingUTF8 = 1 << 2,
        /// <summary>
        /// 代表 GB2312 格式编码的数据
        /// </summary>
        EncodingGB2312 = 1 << 3,
        /// <summary>
        /// 代表 XML 格式数据
        /// </summary>
        XmlDocument = 1 << 4,
        /// <summary>
        /// 代表 JSON 格式数据
        /// </summary>
        JSON = 1 << 5,
        /// <summary>
        /// 16 位 UTF-16 字符
        /// </summary>
        Char = 1 << 6,
        /// <summary>
        /// 8 位无符号整数
        /// </summary>
        Byte = 1 << 7,
        /// <summary>
        /// 16 位无符号整数
        /// </summary>
        Int16 = 1 << 8,
        /// <summary>
        /// 16 位无符号整数
        /// </summary>
        UInt16 = 1 << 9,
        /// <summary>
        /// 32 位无符号整数
        /// </summary>
        Int32 = 1 << 10,
        /// <summary>
        /// 32 位无符号整数
        /// </summary>
        UInt32 = 1 << 11,
        /// <summary>
        /// 64 位无符号整数
        /// </summary>
        Int64 = 1 << 12,
        /// <summary>
        /// 64 位无符号整数
        /// </summary>
        UInt64 = 1 << 13,
        /// <summary>
        /// 货币类型
        /// </summary>
        Decimal = 1 << 14,
        /// <summary>
        /// 代表一个日期时间
        /// </summary>
        DateTime = 1 << 15,
        /// <summary>
        /// 代表 Guid 类型数据
        /// </summary>
        Guid = 1 << 16,
        /// <summary>
        /// 代表 Bool 类型数据
        /// </summary>
        Boolean = 1 << 17,
        /// <summary>
        /// （保留，暂不支持）代表此节点将进行数据完整性校验，并存储数据摘要
        /// </summary>
        HashIncluded = 1 << 18,
        /// <summary>
        /// 代表 列表（集合）类型，其中可以存储任意数量的节点，每个节点都有自己的名字。
        /// </summary>
        List = 1 << 19,
        /// <summary>
        /// 代表数组类型，其中存储的节点类型都是相同的，每个节点只能通过索引（下标)来访问。
        /// </summary>
        Array = 1 << 20,
        /// <summary>
        /// 代表超高压缩类型，其中存储的内容尽可能减少存储空间。（暂不支持）
        /// </summary>
        CompressedVeryHigh = 1 << 21,
        /// <summary>
        /// 代表高压缩类型，其中存储的内容尽可能减少存储空间，同时要求解压迅速。（暂不支持）
        /// </summary>
        CompressedHighDecompressedFast = 1 << 22,
        /// <summary>
        /// 代表低压缩类型，其中存储的内容减少存储空间，但要尽量不影响使用速度。（暂不支持）
        /// </summary>
        CompressedLow = 1 << 23
    }
}
