using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ceeji.Data.BinaryPackage {
    /// <summary>
    /// 代表 CPK 高性能二进制包中存储的值。
    /// </summary>
    public class CPKValue : IEnumerable {
        /// <summary>
        /// 创建 CPK 节点的新实例。
        /// </summary>
        /// <param name="value">节点的值。</param>
        public CPKValue(object value = null) {
            this.Value = value;
        }

        /// <summary>
        /// 创建 CPK 节点的新实例。
        /// </summary>
        /// <param name="type">节点的类型，只能为 None，List 或 Array。</param>
        public CPKValue(CPKValueType type = CPKValueType.None) {
            if (type == CPKValueType.List) {
                this.Value = new List<CPKNode>();
            }
            else if (type == CPKValueType.Array) {
                this.Value = new List<CPKValue>();
            }
            else if (type == CPKValueType.None)
                this.Value = null;
            else
                throw new ArgumentOutOfRangeException();
        }

        private CPKValue(CPKValueType type, object value, byte[] raw, List<CPKValue> items, Dictionary<string, CPKNode> nodes) {
            this.Type = type;
            this.mValue = value;
            this.RawValue = raw;

            if (this.Type == CPKValueType.Array) {
                this.Items = items;
            }
            else if (this.Type == CPKValueType.List) {
                this.Nodes = nodes;
            }
        }

        /// <summary>
        /// 获取节点的值。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Get<T>() {
            if (Value != null)
                return (T)Value;

            return default(T);
        }

        /// <summary>
        /// 获取节点的值，允许指定默认值，当节点类型为 None 时返回默认值。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Get<T>(T defaultVal) {
            if (Value != null && this.Type != CPKValueType.None)
                return (T)Value;

            return defaultVal;
        }

        static CPKValue() {
            mRawFalse = new byte[] { 0 };
            mRawTrue = new byte[] { 1 };
            mNullValue = new byte[] { };
        }

        public override string ToString() {
            return this.Value != null ? this.Value.ToString() : "null";
        }

        /// <summary>
        /// 返回或设置此 CPKValue 的值。对于一般元素，返回对应的 .NET 类型。对于数组，使用 IEnumerable&lt;CPKValue&gt; 类型。对于列表，使用 IEnumerable&lt;CPKNode&gt; 类型。
        /// </summary>
        public object Value {
            get {
                return mValue;
            }
            set {
                if (value is string) {
                    Type = CPKValueType.String | CPKValueType.EncodingUTF8;
                } // int 32
                else if (value is int) {
                    Type = CPKValueType.Int32;
                } // int 64
                else if (value is long) {
                    Type = CPKValueType.Int64;
                } // int 16
                else if (value is short) {
                    Type = CPKValueType.Int16;
                } // int 32
                else if (value is uint) {
                    Type = CPKValueType.UInt32;
                } // int 64
                else if (value is ulong) {
                    Type = CPKValueType.UInt64;
                } // int 16
                else if (value is ushort) {
                    Type = CPKValueType.UInt16;
                } // int 8
                else if (value is byte) {
                    Type = CPKValueType.Byte;
                } // int 16
                else if (value is char) {
                    Type = CPKValueType.Char | CPKValueType.UInt16;
                } // DateTime
                else if (value is DateTime) {
                    Type = CPKValueType.DateTime | CPKValueType.Int64;
                } // Boolean
                else if (value is bool) {
                    Type = CPKValueType.Boolean | CPKValueType.Byte;
                }
                else if (value is byte[]) {
                    Type = CPKValueType.Binary;
                }
                else if (value is decimal) {
                    Type = CPKValueType.Decimal;
                }
                else if (value is Guid? || value is Guid) {
                    Type = CPKValueType.Guid;
                }
                else if (value is IEnumerable<CPKNode>) { // 如果值是列表
                    Type = CPKValueType.List;

                    this.Nodes.Clear();
                    var nodelist = value as IEnumerable<CPKNode>;

                    foreach (var node in nodelist) {
                        this.Nodes.Add(node.Name, node);
                    }

                    value = this.Nodes;
                }
                else if (value is IEnumerable<CPKValue>) { // 如果值是数组
                    Type = CPKValueType.Array;

                    this.Items.Clear();
                    var valList = value as IEnumerable<CPKValue>;
                    this.mItems.AddRange(valList);

                    value = this.Items;
                }
                else if (value == null) {
                    Type = CPKValueType.None;
                }
                //value = setRawValue(value);

                mValue = value;
            }
        }

        private void setRawValue(object value) {
            if (value is string) {
                Type = CPKValueType.String | CPKValueType.EncodingUTF8;
                RawValue = Encoding.UTF8.GetBytes(value as string);
            } // int 32
            else if (value is int) {
                Type = CPKValueType.Int32;
                RawValue = BitConverter.GetBytes((int)value);
            } // int 64
            else if (value is long) {
                Type = CPKValueType.Int64;
                RawValue = BitConverter.GetBytes((long)value);
            } // int 16
            else if (value is short) {
                Type = CPKValueType.Int16;
                RawValue = BitConverter.GetBytes((short)value);
            } // int 32
            else if (value is uint) {
                Type = CPKValueType.UInt32;
                RawValue = BitConverter.GetBytes((uint)value);
            } // int 64
            else if (value is ulong) {
                Type = CPKValueType.UInt64;
                RawValue = BitConverter.GetBytes((ulong)value);
            } // int 16
            else if (value is ushort) {
                Type = CPKValueType.UInt16;
                RawValue = BitConverter.GetBytes((ushort)value);
            } // int 8
            else if (value is byte) {
                Type = CPKValueType.Byte;
                RawValue = BitConverter.GetBytes((byte)value);
            } // int 16
            else if (value is char) {
                Type = CPKValueType.Char | CPKValueType.UInt16;
                RawValue = BitConverter.GetBytes((char)value);
            } // DateTime
            else if (value is DateTime) {
                Type = CPKValueType.DateTime | CPKValueType.Int64;
                RawValue = BitConverter.GetBytes((long)(((DateTime)value).ToBinary()));
            } // Boolean
            else if (value is bool) {
                Type = CPKValueType.Boolean | CPKValueType.Byte;
                RawValue = ((bool)value) ? mRawTrue : mRawFalse;
            }
            else if (value is byte[]) {
                Type = CPKValueType.Binary;
                RawValue = (byte[])value;
            }
            else if (value is decimal) {
                Type = CPKValueType.Decimal;
                RawValue = CPKNode.GetBytes((decimal)value);
            }
            else if (value is Guid? || value is Guid) {
                Type = CPKValueType.Guid;
                var v = (Guid?)value;
                RawValue = v.HasValue ? v.Value.ToByteArray() : mNullValue;
            }
            else if (value is IEnumerable<CPKNode>) { // 如果值是列表
                Type = CPKValueType.List;
                RawValue = getListRaw();
            }
            else if (value is IEnumerable<CPKValue>) { // 如果值是数组
                Type = CPKValueType.Array;
                RawValue = getArrayRaw();
            }
            else if (value == null) {
                Type = CPKValueType.None;
                RawValue = mNullValue;
            }
            else
                throw new InvalidOperationException("不支持此类型 " + value.GetType());
        }

        private byte[] getListRaw() {
            // 对于列表类型的存储方式，就是直接将所有的内容放进去
            using (var ms = new MemoryStream()) {
                getListRawToStream(ms);

                return ms.ToArray();
            }
        }

        private void getListRawToStream(Stream s) {
            // 对于列表类型的存储方式，就是直接将所有的内容放进去，再加一个 0
            foreach (var node in this.Nodes) {
                node.Value.SerializeToStream(s);
            }

            s.WriteByte((byte)0);
        }

        private uint getListRawLength() {
            uint l = 0;
            foreach (var node in this.Nodes) {
                l += node.Value.SerializedLength;
            }
            return l + 1;
        }

        private byte[] getArrayRaw() {
            // 对于数组类型的存储方式，就是直接将所有的内容放进去
            using (var ms = new MemoryStream()) {
                getArrayRawToStream(ms);

                return ms.ToArray();
            }
        }

        private uint getArrayRawLength() {
            uint l = 4;
            foreach (var node in this.Items) {
                l += node.SerializedLength;
            }

            return l;
        }

        private void getArrayRawToStream(Stream ms) {
            // 对于数组类型的存储方式，就是直接将所有的内容放进去
            byte[] binCount = BitConverter.GetBytes((uint)this.Items.Count);
            ms.Write(binCount, 0, 4);

            foreach (var node in this.Items) {
                node.SerializeToStream(ms);
            }
        }

        /// <summary>
        /// 获取或设置节点的子元素的值。
        /// </summary>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        public CPKValue this[string nodeName] {
            get {
                if (!CPKNode.isType(this.Type, CPKValueType.List))
                    throw new InvalidOperationException("节点不是 List 类型，无法使用索引器来访问。");

                CPKNode ret;
                if (this.Nodes.TryGetValue(nodeName, out ret)) {
                    return ret.Value;
                }
                ret = new CPKNode(nodeName, (object)null);
                this.Nodes[nodeName] = ret;
                return ret.Value;
            }
            set {
                if (!CPKNode.isType(this.Type, CPKValueType.List))
                    throw new InvalidOperationException("节点不是 List 类型，无法使用索引器来访问。");

                this.Nodes[nodeName] = new CPKNode(nodeName, value);
            }
        }

        /// <summary>
        /// 获取或设置节点的数组元素的值。
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public CPKValue this[int index] {
            get {
                if (!CPKNode.isType(this.Type, CPKValueType.Array))
                    throw new InvalidOperationException("节点不是 Array 类型，无法使用索引器来访问。");

                return this.Items[index];
            }
            set {
                if (!CPKNode.isType(this.Type, CPKValueType.Array))
                    throw new InvalidOperationException("节点不是 Array 类型，无法使用索引器来访问。");

                this.Items[index] = value;
            }
        }

        /// <summary>
        /// 返回 CPK 节点内的所有子节点的集合。只有 List 类型的节点才可以使用。（Array 类型的节点，请使用 Items 访问其子节点）
        /// </summary>
        public IDictionary<string, CPKNode> Nodes {
            get {
                if (!CPKNode.isType(this.Type, CPKValueType.List))
                    throw new InvalidOperationException("节点不是 List 类型，无法访问子节点。");

                return this.mNodes;
            }
            private set {
                if (!CPKNode.isType(this.Type, CPKValueType.List))
                    throw new InvalidOperationException("节点不是 List 类型，无法访问子节点。");

                if (value is Dictionary<string, CPKNode>)
                    this.mNodes = value as Dictionary<string, CPKNode>;
                else
                    throw new ArgumentException();
            }
        }

        private Dictionary<string, CPKNode> mNodes = null;

        /// <summary>
        /// 返回 CPK 节点内的所有数组元素的集合。只有 Array 类型的节点才可以使用。（List 类型的节点，请使用 Nodes 访问其子节点）
        /// </summary>
        public IList<CPKValue> Items {
            get {
                if (!CPKNode.isType(this.Type, CPKValueType.Array))
                    throw new InvalidOperationException("节点不是 Array 类型，无法访问数组元素。");

                return this.mItems;
            }
            private set {
                if (!CPKNode.isType(this.Type, CPKValueType.Array))
                    throw new InvalidOperationException("节点不是 Array 类型，无法访问数组元素。");

                if (value is List<CPKValue>)
                    this.mItems = value as List<CPKValue>;
                else
                    throw new ArgumentException();
            }
        }

        private List<CPKValue> mItems = null;

        /// <summary>
        /// 序列化 CPK 值到二进制数据中。
        /// </summary>
        /// <returns></returns>
        public byte[] SerializeToBinary() {
            using (var ms = new MemoryStream()) {
                SerializeToStream(ms);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 序列化 CPK 节点到流中。
        /// </summary>
        /// <returns></returns>
        public void SerializeToStream(Stream stream) {
            // var raw = this.RawValue;
            // var lenlen = CPKNode.getLengthLength(raw.Length);
            // var ret = new byte[4 + lenlen + raw.Length];

            var dataType = BitConverter.GetBytes(((uint)this.Type));
            stream.Write(dataType, 0, dataType.Length); // .CopyTo(ret, 0); // 节点类型
            // CPKNode.fillLength(raw.Length, ret, 4);
            var dataLength = CPKNode.getLengthBinary((int)this.getRawValueLength());
            stream.Write(dataLength, 0, dataLength.Length);
            //var raw = this.RawValue;
            this.WriteRawValueToStream(stream);
            // this.RawValue.CopyTo(ret, 4 + lenlen);

            // return ret;
        }

        public static CPKValue DeserializeFromStream(Stream s) {
            var br = new BinaryReader(s);

            var type = (CPKValueType)br.ReadUInt32();
            var rawLength = CPKNode.readDataLength(br);
            object val;

            List<CPKValue> items = null;
            Dictionary<string, CPKNode> nodes = null;

            if (CPKNode.isType(type, CPKValueType.Binary)) {
                val = br.ReadBytes((int)rawLength);
            }
            else if (CPKNode.isType(type, CPKValueType.Boolean)) {
                if (rawLength != 1) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadBoolean();
            }
            else if (CPKNode.isType(type, CPKValueType.Byte)) {
                if (rawLength != 1) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadByte();
            }
            else if (CPKNode.isType(type, CPKValueType.Char)) {
                if (rawLength != 2) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadChar();
            }
            else if (CPKNode.isType(type, CPKValueType.DateTime)) {
                if (rawLength != 8) throw new InvalidDataException(FORMAT_EXCEPTION);
                val = DateTime.FromBinary(br.ReadInt64());
            }
            else if (CPKNode.isType(type, CPKValueType.Decimal)) {
                if (rawLength != 16) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = CPKNode.ToDecimal(br.ReadBytes(16));
            }
            else if (CPKNode.isType(type, CPKValueType.Guid)) {
                if (rawLength != 16) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = new Guid(br.ReadBytes(16));
            }
            else if (CPKNode.isType(type, CPKValueType.Int16)) {
                if (rawLength != 2) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadInt16();
            }
            else if (CPKNode.isType(type, CPKValueType.UInt16)) {
                if (rawLength != 2) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadUInt16();
            }
            else if (CPKNode.isType(type, CPKValueType.Int32)) {
                if (rawLength != 4) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadInt32();
            }
            else if (CPKNode.isType(type, CPKValueType.UInt32)) {
                if (rawLength != 4) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadUInt32();
            }
            else if (CPKNode.isType(type, CPKValueType.Int64)) {
                if (rawLength != 8) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadInt64();
            }
            else if (CPKNode.isType(type, CPKValueType.UInt64)) {
                if (rawLength != 8) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = br.ReadUInt64();
            }
            else if (CPKNode.isType(type, CPKValueType.String)) {
                val = Encoding.UTF8.GetString(br.ReadBytes((int)rawLength));
            }
            else if (CPKNode.isType(type, CPKValueType.Array)) {
                CPKValue item;
                items = new List<CPKValue>();

                var count = br.ReadUInt32();
                if (count > 0) {
                    for (var i = 0; i < count; ++i) {
                        item = CPKValue.DeserializeFromStream(s);
                        items.Add(item);
                    }
                }

                val = items;
            }
            else if (CPKNode.isType(type, CPKValueType.List)) {
                CPKNode item;
                nodes = new Dictionary<string, CPKNode>();

                while ((item = CPKNode.DeserializeFromStream(s)) != null) {
                    nodes.Add(item.Name, item);
                }

                val = nodes;
            }
            else if (type == CPKValueType.None) {
                if (rawLength != 0) throw new InvalidDataException(FORMAT_EXCEPTION);

                val = mNullValue;
            }
            else
                throw new NotSupportedException("不支持此类型的 CPKValue。");

            return new CPKValue(type, val, null, items, nodes);
        }

        /// <summary>
        /// 获取数据的原始值。
        /// </summary>
        public byte[] RawValue {
            get {
                using (var ms = new MemoryStream()) {
                    WriteRawValueToStream(ms);
                    return ms.ToArray();
                }
            }
            private set {
                this.mRawCache = value;
            }
        }

        /// <summary>
        /// 获取数据的原始值的长度。
        /// </summary>
        public uint SerializedLength {
            get {
                var rawLength = getRawValueLength();
                var dataLength = CPKNode.getLengthBinary((int)rawLength);

                return (uint)(dataLength.Length + rawLength + 4);
            }
        }

        /// <summary>
        /// 获取数据的原始值的长度。
        /// </summary>
        private uint getRawValueLength() {
            if (this.Type == CPKValueType.List) {
                return getListRawLength();
            }
            else if (this.Type == CPKValueType.Array)
                return getArrayRawLength();

            using (var ms = new MemoryStream()) {
                WriteRawValueToStream(ms);
                return (uint)ms.Length;
            }
        }

        /// <summary>
        /// 获取数据的原始值。
        /// </summary>
        public void WriteRawValueToStream(Stream s) {
            if (this.Type == CPKValueType.List) {
                getListRawToStream(s);
                return;
            }
            else if (this.Type == CPKValueType.Array) {
                getArrayRawToStream(s);
                return;
            }

            setRawValue(this.Value);
            s.Write(mRawCache, 0, mRawCache.Length);
        }

        private byte[] mRawCache;

        /// <summary>
        /// 获取节点的类型。
        /// </summary>
        public CPKValueType Type {
            get {
                return this.mType;
            }
            private set {
                this.mType = value;

                if (CPKNode.isType(this.mType, CPKValueType.List) && this.Nodes == null) {
                    this.Nodes = new Dictionary<string,CPKNode>();
                }

                if (CPKNode.isType(this.mType, CPKValueType.Array) && this.Items == null) {
                    this.Items = new List<CPKValue>();
                }
            }
        }

        private CPKValueType mType;
        private static byte[] mRawTrue;
        private static byte[] mRawFalse;
        private static byte[] mNullValue;
        private object mValue;
        private const string FORMAT_EXCEPTION = "输入数据的格式不正确，数据可能已经损坏。";

        /// <summary>
        /// 返回子节点的枚举数。只在 List 和 Array 类型的元素中有效。
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator() {
            if (CPKNode.isType(this.mType, CPKValueType.List)) {
                foreach (var node in this.mNodes)
                    yield return node;
            }
        }

        public static implicit operator CPKValue(ValueType obj) {
            return new CPKValue(obj);
        }

        public static implicit operator CPKValue(string obj) {
            return new CPKValue(obj);
        }

        public static implicit operator CPKValue(byte[] obj) {
            return new CPKValue(obj);
        }
    }
}
