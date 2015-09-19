using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ceeji.Data.BinaryPackage {
    /// <summary>
    /// 代表 CPK 数据格式中的一个节点。
    /// </summary>
    public class CPKNode : IEnumerable {
        /// <summary>
        /// 创建 CPK 节点的新实例。
        /// </summary>
        /// <param name="name">节点的名称，此名称不能在同一层次中重复。</param>
        /// <param name="value">节点的值。</param>
        public CPKNode(string name, CPKValue value) {
            if (value == null)
                throw new ArgumentNullException("value");

            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// 创建 CPK 节点的新实例。
        /// </summary>
        /// <param name="name">节点的名称，此名称不能在同一层次中重复。</param>
        /// <param name="value">节点的值。</param>
        public CPKNode(string name, object value = null) {
            this.Name = name;
            this.Value = new CPKValue(value);
        }

        /// <summary>
        /// 获取节点的值。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Get<T>() {
            return this.Value.Get<T>();
        }

        public override string ToString() {
            return string.Format("Name: {0}, Value: {1}", this.Name, this.Value);
        }

        /// <summary>
        /// 返回或设置 CPK 节点的名称。
        /// </summary>
        public string Name {
            get {
                return this.mName;
            }
            set {
                if (value == null || value == "") throw new ArgumentNullException("Name");

                this.mName = value;
            }
        }

        /// <summary>
        /// 返回或设置 CPK 节点的值。
        /// </summary>
        public CPKValue Value {
            get {
                return mValue;
            }
            set {
                mValue = value;
            }
        }

        /// <summary>
        /// 获取或设置节点的子元素。
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public CPKValue this[string name] {
            get {
                return this.Value[name];
            }
            set {
                this.Value[name] = value;
            }
        }

        internal static byte[] getBinaryWithLength(byte[] data) {
            var length = data.Length;
            byte[] ret;
            if (length < 253) {
                ret = new byte[length + 1];
                ret[0] = (byte)length;
            }
            else if (length < 65534) {
                ret = new byte[length + 3];
                ret[0] = 253;
                BitConverter.GetBytes((ushort)length).CopyTo(ret, 1);
            }
            else {
                ret = new byte[length + 5];
                ret[0] = 254;
                BitConverter.GetBytes((uint)length).CopyTo(ret, 1);
            }
            data.CopyTo(ret, ret.Length - length);
            return ret;
        }

        internal static void fillLength(int length, byte[] fillTo, int pos) {
            if (length < 253) {
                fillTo[pos] = (byte)length;
            }
            else if (length < 65534) {
                fillTo[pos] = 253;
                BitConverter.GetBytes((ushort)length).CopyTo(fillTo, pos + 1);
            }
            else {
                fillTo[pos] = 254;
                BitConverter.GetBytes((uint)length).CopyTo(fillTo, pos + 1);
            }
        }

        internal static byte[] getLengthBinary(int length) {
            byte[] fillTo;
            if (length < 253) {
                fillTo = new byte[1];
                fillTo[0] = (byte)length;
            }
            else if (length < 65534) {
                fillTo = new byte[3];
                fillTo[0] = 253;
                BitConverter.GetBytes((ushort)length).CopyTo(fillTo, 1);
            }
            else {
                fillTo = new byte[5];
                fillTo[0] = 254;
                BitConverter.GetBytes((uint)length).CopyTo(fillTo, 1);
            }

            return fillTo;
        }

        internal static int getLengthLength(int length) {
            if (length < 253) {
                return 1;
            }
            else if (length < 65534) {
                return 3;
            }
            else {
                return 5;
            }
        }

        /// <summary>
        /// 序列化 CPK 节点到二进制数据中。
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
            var binName = getBinaryWithLength(Encoding.UTF8.GetBytes(Name));
            stream.Write(binName, 0, binName.Length);
            Value.SerializeToStream(stream);
        }

        /// <summary>
        /// 返回序列化 CPK 节点到流中的长度。
        /// </summary>
        /// <returns></returns>
        public uint SerializedLength {
            get {
                var binName = getBinaryWithLength(Encoding.UTF8.GetBytes(Name));
                return (uint)(binName.Length + Value.SerializedLength);
            }
        }

        /// <summary>
        /// 从流中反序列化 CPK 节点。
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static CPKNode DeserializeFromStream(Stream s) {
            var br = new BinaryReader(s);

            var name = readStringWithLength(br);
            if (name == String.Empty) {
                return null;
            }

            var val = CPKValue.DeserializeFromStream(s);
            return new CPKNode(name, val);
        }

        internal static byte[] GetBytes(decimal dec) {
            //Load four 32 bit integers from the Decimal.GetBits function
            Int32[] bits = decimal.GetBits(dec);
            //Create a temporary list to hold the bytes
            List<byte> bytes = new List<byte>();
            //iterate each 32 bit integer
            foreach (Int32 i in bits) {
                //add the bytes of the current 32bit integer
                //to the bytes list
                bytes.AddRange(BitConverter.GetBytes(i));
            }
            //return the bytes list as an array
            return bytes.ToArray();
        }

        internal static decimal ToDecimal(byte[] bytes) {
            //check that it is even possible to convert the array
            if (bytes.Length != 16)
                throw new Exception("A decimal must be created from exactly 16 bytes");
            //make an array to convert back to int32's
            Int32[] bits = new Int32[4];
            for (int i = 0; i <= 15; i += 4) {
                //convert every 4 bytes into an int32
                bits[i / 4] = BitConverter.ToInt32(bytes, i);
            }
            //Use the decimal's new constructor to
            //create an instance of decimal
            return new decimal(bits);
        }

        internal static bool isType(CPKValueType type, CPKValueType t) {
            return (type & t) != 0;
        }

        internal static string readStringWithLength(BinaryReader br) {
            var l = readDataWithLength(br);
            if (l.Length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(l);
        }

        internal static byte[] readDataWithLength(BinaryReader br) {
            var len1 = br.ReadByte();
            if (len1 < 253) {
                return br.ReadBytes(len1);
            }
            else if (len1 == 253) {
                return br.ReadBytes(br.ReadUInt16());
            }
            else
                return br.ReadBytes((int)br.ReadUInt32());
        }

        internal static uint readDataLength(BinaryReader br) {
            var len1 = br.ReadByte();
            if (len1 < 253) {
                return len1;
            }
            else if (len1 == 253) {
                return br.ReadUInt16();
            }
            else
                return br.ReadUInt32();
        }

        /// <summary>
        /// 返回此 CPKNode 的类型。
        /// </summary>
        public CPKValueType Type {
            get {
                return this.Value.Type;
            }
        }

        private string mName;
        private CPKValue mValue;

        /// <summary>
        /// 返回子节点的枚举数。只在 List 和 Array 类型的元素中有效。
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator() {
            return this.Value.GetEnumerator();
        }
    }
}
