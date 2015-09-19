using Ceeji.Data.BinaryPackage;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Ceeji.Data.BinaryPackage {
    /// <summary>
    /// 代表从流中存取高性能二进制包（CeejiBinaryPackage，*.CPK）格式的对象和方法。此类所构建的高性能二进制包是在内存中全额存储的。
    /// </summary>
    public class CPKPackage {
        private CPKPackage() {
            this.FormatVersion = FORMAT_VERSION_CODE;
            
            this.Nodes = new Dictionary<string, CPKNode>();
        }

        /// <summary>
        /// 创建一个新的高性能二进制包（CeejiBinaryPackage，*.CPK）格式的数据集。
        /// </summary>
        /// <param name="contentType">正文类型，可以随便指定，格式规范为 软件名[.子类型[.子类型]]，例如 MediaPlayer.UserSettings，Bzbx.Settings.Local。此类型用于区分不同格式的 CPK 数据包。打开 CPK 包时，系统会验证此格式。</param>
        public CPKPackage(string contentType, CPKFlags flags = CPKFlags.None) : this() {
            this.ContentType = contentType;
            this.Flags = flags;

            if ((this.Flags & CPKFlags.GuidIncluded) != 0) {
                // 如果要求创建 Guid，则创建
                // 只能在此处创建，保存或加载时不创建
                // 以确保文件 Guid 正确的跟踪性
                this.PackageGuid = Guid.NewGuid();
                this["_!#guid#!_"] = this.PackageGuid.Value;
            }
        }

        /// <summary>
        /// 从指定的二进制数组中加载高性能二进制包（CeejiBinaryPackage，*.CPK 格式）的数据集。
        /// </summary>
        /// <param name="data">要从中读取的二进制数组。</param>
        /// <param name="expectedContentType">正文类型的期待值，如果不是该值，会抛出异常。</param>
        /// <exception cref="InvalidDataException"></exception>
        public CPKPackage(byte[] data, string expectedContentType = null) : this(new MemoryStream(data), expectedContentType) {

        }

        /// <summary>
        /// 从指定的流中加载高性能二进制包（CeejiBinaryPackage，*.CPK 格式）的数据集。
        /// </summary>
        /// <param name="stream">要从中读取的流。</param>
        /// <param name="expectedContentType">正文类型的期待值，如果不是该值，会抛出异常。</param>
        /// <exception cref="InvalidDataException"></exception>
        public CPKPackage(Stream stream, string expectedContentType = null)
            : this() {
                loadFromStream(stream, expectedContentType);
        }

        /// <summary>
        /// 获取当前 CPK 包的版本号。
        /// </summary>
        public int FormatVersion { get; private set; }

        private void loadFromStream(Stream stream, string expectedContentType) {
            // TODO 流式读取尚未完成
            // 增加参数，是否一次性读取
            // 增加迭代器，用于流读取
            // 如果是流读取，只有读取结束之时，才可以使用 Nodes 属性
            var br = new BinaryReader(stream);

            if (!br.ReadBytes(5).SequenceEqual(mHeaderBytes)) // 文件头
                throw new InvalidDataException("指定的流不是 CPK 数据，或流位置不正确。");

            if ((this.FormatVersion = br.ReadUInt16()) > FORMAT_VERSION_CODE) // 版本号
                throw new NotSupportedException("不支持读取此版本的 CPK 数据（CPK 格式版本过高）");

            this.Flags = (CPKFlags)br.ReadUInt32(); // 标记位

            this.ContentType = CPKNode.readStringWithLength(br); // 正文类型

            if (expectedContentType != null && expectedContentType != this.ContentType)
                throw new InvalidDataException("输入数据的类型不是需要的类型。");

            if ((this.Flags & CPKFlags.LZ4Compressed) == CPKFlags.LZ4Compressed) {
                stream = new Ceeji.Data.Codec.LZ4.LZ4Stream(stream, System.IO.Compression.CompressionMode.Decompress, false, 1024 * 1024 * 30);
            }
            if ((this.Flags & CPKFlags.LZ4HCCompressed) == CPKFlags.LZ4HCCompressed) {
                stream = new Ceeji.Data.Codec.LZ4.LZ4Stream(stream, System.IO.Compression.CompressionMode.Decompress, true, 1024 * 1024 * 30);
            }
            if ((this.Flags & CPKFlags.DeflateCompressed) == CPKFlags.DeflateCompressed) {
                stream = new DeflateStream(stream, System.IO.Compression.CompressionMode.Decompress, true);
            }

            long pos = -1, pos2 = -1;
            if (this.HashIncluded) {
                pos = stream.Position;
            }

            CPKNode hashNode = null;

            CPKNode node;
            while ((node = CPKNode.DeserializeFromStream(stream)) != null) {
                if (node.Name != "_!#hash#!_") {
                    if (this.HashIncluded) {
                        pos2 = stream.Position;
                    }
                    this.Nodes.Add(node.Name, node);
                    if (node.Name == "_!#guid#!_") {
                        this.PackageGuid = (Guid)node.Value.Value;
                    }
                }
                else {
                    hashNode = node;
                }
            }

            if (this.HashIncluded) {
                stream.Position = pos;
                var hash = HashHelper.ComputeRawHash(new PartialReadOnlyStream(stream, pos, pos2 - pos), HashHelper.MD5Algorithm);

                if (hashNode == null || !hashNode.Get<byte[]>().SequenceEqual(hash)) {
                    throw new InvalidDataException("指定的输入数据的数据摘要不一致，可能已经被无意间修改");
                }
            }
        }

        class PartialReadOnlyStream : Stream {
            public PartialReadOnlyStream(Stream s, long startPosition, long length) {
                this.s = s;
                s.Seek(startPosition, SeekOrigin.Begin);
                ReadEnd = startPosition + length;
                this.len = length;
                this.start = startPosition;
            }

            private Stream s;
            private long start, len;
            public long ReadEnd { get; set; }

            public override int Read(byte[] array, int offset, int count) {
                if (s.Position >= this.ReadEnd)
                    return 0;

                if (s.Position + count > this.ReadEnd)
                    count = (int)(this.ReadEnd - s.Position);

                return s.Read(array, offset, count);
            }



            public override bool CanRead {
                get { return s.CanRead; }
            }

            public override bool CanSeek {
                get { return s.CanSeek; }
            }

            public override bool CanWrite {
                get { return false; }
            }

            public override void Flush() {
                s.Flush();
            }

            public override long Length {
                get { return this.len; }
            }

            public override long Position {
                get {
                    return s.Position - start;
                }
                set {
                    s.Seek(value + start, SeekOrigin.Begin);
                }
            }

            public override long Seek(long offset, SeekOrigin origin) {
                return s.Seek(offset + start, origin);
            }

            public override void SetLength(long value) {
                s.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count) {
                throw new NotImplementedException();
            }
        }

        internal static byte[] readFully(Stream input) {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream()) {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        static CPKPackage() {
            mEmptyByteArr = new byte[0];
            mHeaderBytes = Encoding.ASCII.GetBytes("CJPKG");
        }

        /// <summary>
        /// 返回或设置此二进制包（BinaryPackage）的正文类型。正文类型必须定义，以便确认文件中存储内容的解析方式。不同内容的 CPK 包，应有不同的正文类型。
        /// </summary>
        public string ContentType {
            get {
                return this.mContentType;
            }
            set {
                if (value == null || value == "")
                    throw new ArgumentNullException("ContentType");

                this.mContentType = value;
            }
        }

        /// <summary>
        /// 标示此 CPK 数据中包括对整个 CPK 文件的哈希签名数据，可用于防止文件在无意中被用户修改（不能用于防止篡改，因为哈希值也可以被修改），但此签名不包括文件头。
        /// </summary>
        public bool HashIncluded {
            get {
                return (this.Flags & CPKFlags.HashIncluded) != 0;
            }
            set {
                this.Flags |= CPKFlags.HashIncluded;
            }
        }

        /// <summary>
        /// 返回包的 Guid 值（如果有）。
        /// </summary>
        public Guid? PackageGuid { get; private set; }

        /// <summary>
        /// 返回 CPK 包内的所有节点的集合。
        /// </summary>
        public IDictionary<string, CPKNode> Nodes {
            get {
                return this.mNodes;
            }
            private set {
                if (value is Dictionary<string, CPKNode>)
                    this.mNodes = value as Dictionary<string, CPKNode>;
                else
                    throw new ArgumentException();
            }
        }

        /// <summary>
        /// 返回或设置指定名称的节点的值。
        /// </summary>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        public CPKValue this[string nodeName] {
            get {
                CPKNode ret;
                if (this.Nodes.TryGetValue(nodeName, out ret)) {
                    return ret.Value;
                }
                ret = new CPKNode(nodeName, (object)null);
                this.Nodes[nodeName] = ret;
                return ret.Value;
            }
            set {
                this.Nodes[nodeName] = new CPKNode(nodeName, value);
            }
        }

        /// <summary>
        /// 返回或设置 CPK 文件的标记。
        /// </summary>
        public CPKFlags Flags {
            get { return this.mFlags; }
            set { this.mFlags = value; }
        }

        /// <summary>
        /// 将包的内容写入指定的流。
        /// </summary>
        /// <param name="stream"></param>
        public void WriteToStream(Stream stream) {
            if (stream == null)
                throw new ArgumentNullException("stream");

            // 打开流进行写入
            var bw = new BinaryWriter(stream);

            // 输出文件头
            bw.Write(mHeaderBytes);
            bw.Write(FORMAT_VERSION_CODE);
            bw.Write((uint)this.Flags);
            bw.Write(CPKNode.getBinaryWithLength(Encoding.UTF8.GetBytes(ContentType)));

            // 输出正文
            long pos = -1;
            if (this.HashIncluded) {
                pos = stream.Position;
            }

            if ((this.Flags & CPKFlags.LZ4Compressed) == CPKFlags.LZ4Compressed) {
                stream.Flush();
                stream = new Ceeji.Data.Codec.LZ4.LZ4Stream(stream, System.IO.Compression.CompressionMode.Compress, false, 1024 * 1024 * 30);
                bw = new BinaryWriter(stream);
            }
            if ((this.Flags & CPKFlags.LZ4HCCompressed) == CPKFlags.LZ4HCCompressed) {
                stream.Flush();
                stream = new Ceeji.Data.Codec.LZ4.LZ4Stream(stream, System.IO.Compression.CompressionMode.Compress, true, 1024 * 1024 * 30);
                bw = new BinaryWriter(stream);
            }
            if ((this.Flags & CPKFlags.DeflateCompressed) == CPKFlags.DeflateCompressed) {
                stream.Flush();
                stream = new DeflateStream(stream, System.IO.Compression.CompressionMode.Compress, true);
                bw = new BinaryWriter(stream);
            }

            // var binNodes = mEmptyByteArr;
            foreach (var node in this.Nodes) {
                if (node.Key == "_!#hash#!_")
                    continue;

                node.Value.SerializeToStream(stream);
                //binNodes = binNodes.Concat(data).ToArray();
            }

            // bw.Write(binNodes);

            // 如果需要哈希，则计算哈希
            if (this.HashIncluded) {
                long posn = stream.Position;
                stream.Position = pos;
                var hash = HashHelper.ComputeRawHash(stream, HashHelper.MD5Algorithm);
                stream.Position = posn;
                new CPKNode("_!#hash#!_", hash).SerializeToStream(stream);
            }

            // 输出结束符
            bw.Write((byte)0);
            bw.Flush();
            stream.Flush();
        }

        public byte[] GetBinary() {
            var ms = new MemoryStream();

            WriteToStream(ms);

            return ms.ToArray();
        }

        public override string ToString() {
            return "CPKPackage: " + this.ContentType + " [" + this.Nodes.Count + "]";
        }

        private string mContentType;
        private CPKFlags mFlags;
        private Dictionary<string, CPKNode> mNodes;
        private static readonly byte[] mHeaderBytes;
        private const ushort FORMAT_VERSION_CODE = 1;
        private static byte[] mEmptyByteArr;
    }
}
