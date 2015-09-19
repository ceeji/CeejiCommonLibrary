using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Data.BinaryPackage {
    /// <summary>
    /// 指定 CPK 数据可能拥有的一些属性标记。
    /// </summary>
    [Flags]
    public enum CPKFlags : uint {
        /// <summary>
        /// 无标记
        /// </summary>
        None,
        /// <summary>
        /// 标示此 CPK 数据中包括对整个 CPK 文件的哈希签名数据，可用于防止文件在无意中被用户修改（不能用于防止篡改，因为哈希值也可以被修改），但此签名不包括文件头。
        /// </summary>
        HashIncluded = 1 << 0,
        /// <summary>
        /// 标示 CPK 包含一个全球唯一标识符（Guid），用于跟踪相同文件的编辑
        /// </summary>
        GuidIncluded = 1 << 1,
        /// <summary>
        /// 标志 CPK 被 LZ4 算法压缩，该算法的特点是极其快速、压缩率低、解压快速
        /// </summary>
        LZ4Compressed = 1 << 2,
        /// <summary>
        /// 标志 CPK 被 LZ4HC 算法压缩，该算法的特点是速度慢、压缩率中等、解压快速
        /// </summary>
        LZ4HCCompressed = 1 << 3,
        /// <summary>
        /// 标志 CPK 被 Deflate 算法压缩，该算法的特点是速度慢、压缩率较好、解压中速，且具有 GZip 头的校验功能（暂不支持请勿使用）
        /// </summary>
        DeflateCompressed = 1 << 4
    }
}
