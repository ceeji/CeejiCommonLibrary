using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ceeji.DirectCall {
    /// <summary>
    /// 可通过此类注册、缓存并最终调用本地 API。
    /// </summary>
    public class DirectCaller {
        /// <summary>
        /// 创建 <see cref="DirectCaller"/> 的新实例。该实例将自动注册常用类型的二进制序列化和反序列化器。
        /// </summary>
        public DirectCaller() {
            // 注册 int32 类型
            RegisterSerialableType<int>(
                (i, s) => s.Write((int)i),
                s => s.ReadInt32());

            // 注册 int64 类型
            RegisterSerialableType<long>(
                (i, s) => s.Write((long)i),
                s => s.ReadInt64());

            // 注册 uint32 类型
            RegisterSerialableType<uint>(
                (i, s) => s.Write((uint)i),
                s => s.ReadUInt32());

            // 注册 uint64 类型
            RegisterSerialableType<ulong>(
                (i, s) => s.Write((ulong)i),
                s => s.ReadUInt64());

            // 注册 int16 类型
            RegisterSerialableType<short>(
                (i, s) => s.Write((short)i),
                s => s.ReadInt16());

            // 注册 decimal 类型
            RegisterSerialableType<decimal>(
                (i, s) => s.Write((decimal)i),
                s => s.ReadDecimal());

            // 注册 float 类型
            RegisterSerialableType<float>(
                (i, s) => s.Write((float)i),
                s => s.ReadSingle());

            // 注册 double 类型
            RegisterSerialableType<double>(
                (i, s) => s.Write((double)i),
                s => s.ReadDouble());

            // 注册 byte 类型
            RegisterSerialableType<byte>(
                (i, s) => s.Write((byte)i),
                s => s.ReadByte());

            // 注册 bool 类型
            RegisterSerialableType<bool>(
                (i, s) => s.Write((bool)i),
                s => s.ReadBoolean());

            // 注册 bool? 类型
            RegisterSerialableType<bool?>(
                (i, s) => {
                    byte val = 0;
                    if (i == null) val = 2;
                    else if (((bool)i) == true) val = 1;
                    else val = 0;

                    s.Write(val);
                },
                s => {
                    var b = s.ReadByte();
                    if (b == 0) return false;
                    else if (b == 1) return true;
                    else return null;
                });

            // 注册 string 类型
            RegisterSerialableType<string>(
                (i, s) => s.Write((string)i),
                s => s.ReadString());

            // 注册 Guid 类型
            RegisterSerialableType<Guid>(
                (i, s) => s.Write(((Guid)i).ToByteArray()),
                s => new Guid(s.ReadBytes(16)));

            // 注册 Exception 类型
            RegisterSerialableType<Exception>(
                (i, s) => {
                    var ex = (Exception)i;
                    // 写入 Exception 的名称
                    s.Write(ex.GetType().FullName);
                    // 写入 Exception 的 Message
                    s.Write(ex.Message);
                    // 写入 StackTrace
                    s.Write(Ceeji.Log.LogWriter.GetExceptionDetailMessage(ex, false));
                },
                s => {
                    var name = s.ReadString();
                    var msg = s.ReadString();
                    var st = s.ReadString();

                    return new Exception($"远程调用方发生错误 {name}：{msg}{Environment.NewLine}{st}");
                });

            // 缓存一些数组对象
            objectCache = new List<object[]>();
            for (int i = 0; i < 30; ++i) {
                objectCache.Add(new object[i]);
            }
        }

        /// <summary>
        /// 注册指定的 <typeparamref name="T"/> 类型的类实例，使该类中的 public 方法可以被外界直接调用。此方法不是线程安全的。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="classInstance">要注册的类实例。</param>
        public void Register<T>(T classInstance) where T : class {
            var className = classInstance.GetType().Name;

            // 查找所有公有方法
            var methods = classInstance.GetType().GetMethods().Where(x => x.Name != "Equals" && x.Name != "ToString" && x.Name != "GetType" && x.Name != "GetHashCode");


            // 检查公有方法是否重名
            var allNames = methods.Select(m => m.Name.ToLowerInvariant()).ToList();

            if (allNames.Distinct().Count() != allNames.Count)
                throw new InvalidOperationException("为 DirectCall 注册的类型不允许拥有重名的方法。");

            // 检查公有方法是否携带了非法的参数类型
            foreach (var m in methods) {
                foreach (var p in m.GetParameters()) {
                    if (!isValidType(p.ParameterType)) {
                        throw new InvalidOperationException($"为 DirectCall 注册的 public 函数 {m} 含有不支持的参数类型 {p.ParameterType}");
                    }
                }

                if (!isValidType(m.ReturnType)) {
                    throw new InvalidOperationException($"为 DirectCall 注册的 public 函数 {m} 含有不支持的参数类型 {m.ReturnType}");
                }
            }

            // 缓存公有方法
            foreach (var m in methods) {
                // 定义调用该函数的委托
                var p = m.GetParameters();

                var dg = new Action<BinaryReader, BinaryWriter>((reader, writer) => {
                    // 循环读取参数并压入数组
                    var pObj = objectCache[p.Length];

                    for (var i = 0; i < p.Length; ++i) {
                        pObj[i] = ReadValue(p[i].ParameterType, reader);
                    }

                    // 执行方法
                    try {
                        var ret = m.Invoke(classInstance, pObj);

                        // 将返回值序列化
                        writer.Write((byte)ReturnFlags.OK);
                        WriteValue(m.ReturnType, ret, writer);
                    }
                    catch (TargetInvocationException ex) {
                        writer.Write((byte)ReturnFlags.IsThrowed);
                        WriteValue(typeof(Exception), ex.InnerException, writer);
                    }
                    catch (Exception ex) {
                        writer.Write((byte)ReturnFlags.IsThrowed);
                        WriteValue(typeof(Exception), ex, writer);
                    }
                });

                mMethodTable.Add(string.Concat(className, ":" + m.Name), new MethodCallingInfo() {
                    MethodDelegate = dg
                });
            }
        }

        /// <summary>
        /// 获取指定调用的返回值。如果该调用出现异常，则此处将重新抛出该异常。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetReturnValue<T>(BinaryReader reader) {
            var status = (ReturnFlags)reader.ReadByte();

            if ((status & ReturnFlags.IsThrowed) == ReturnFlags.IsThrowed) {
                // 返回的是异常，则重新抛出异常
                var exception = (Exception)ReadValue(typeof(Exception), reader);

                throw exception;
            }

            // 否则，取出返回值
            return (T)ReadValue(typeof(T), reader);
        }

        /// <summary>
        /// 将对某些方法的调用信息写入指定的 <see cref="BinaryWriter"/>。
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="methodName"></param>
        /// <param name="paramList"></param>
        /// <param name="writer"></param>
        public void MakeMethodCall(string typeName, string methodName, BinaryWriter writer) {
            // 写入方法 key
            writer.Write($"{typeName}:{methodName}");
        }

        /// <summary>
        /// 通过序列化方式，本地调用某个方法，并获取返回值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parameterReader"></param>
        /// <param name="returnValueWriter"></param>
        public void Call(BinaryReader parameterReader, BinaryWriter returnValueWriter) {
            var key = parameterReader.ReadString();

            if (!mMethodTable.ContainsKey(key)) {
                returnValueWriter.Write((byte)ReturnFlags.NotFound);

                return;
            }

            mMethodTable[key].MethodDelegate(parameterReader, returnValueWriter);
        }

        /// <summary>
        /// 向一个 <see cref="BinaryReader"/> 中写入序列化的值。
        /// </summary>
        /// <param name="t"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        public void WriteValue(Type t, object val, BinaryWriter writer) {
            if (t == typeof(void)) return;

            Type undrlyingType = null;

            if (!t.IsValueType || (undrlyingType = Nullable.GetUnderlyingType(t)) != null) {
                // 对于所有引用类型，需要判断其是否为空，此处使用 1 个字节进行判断

                writer.Write(val != null);
            }
            else if (val == null) throw new ArgumentException("值不能为 Null");

            // 调用对应的反序列化函数
            if (val != null) {
                undrlyingType = undrlyingType ?? t;

                if (!mSerialTable.ContainsKey(undrlyingType))
                    throw new InvalidOperationException($"类型无法被序列化，找不到对应的序列化函数：{undrlyingType}");

                mSerialTable[undrlyingType].serialFunc(val, writer);
            }
        }

        /// <summary>
        /// 从一个 <see cref="BinaryReader"/> 中读取可反序列化的值。
        /// </summary>
        /// <param name="t"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        public object ReadValue(Type t, BinaryReader reader) {
            Type undrlyingType = null;

            if (!t.IsValueType || (undrlyingType = Nullable.GetUnderlyingType(t)) != null) {
                // 对于所有引用类型，需要判断其是否为空，此处使用 1 个字节进行判断
                var hasValue = reader.ReadBoolean();
                if (!hasValue) {
                    // 如果没有值，则返回可空类型的默认值
                    return getDefaultValue(t);
                }
            }

            // 调用对应的反序列化函数
            undrlyingType = undrlyingType ?? t;

            if (!mSerialTable.ContainsKey(undrlyingType))
                throw new InvalidOperationException($"类型无法被反序列化，找不到对应的反序列化函数：{undrlyingType}");

            return mSerialTable[undrlyingType].deserialFunc(reader);
        }

        internal bool isValidType(Type t) {
            if (t == typeof(void)) return true;

            if (mSerialTable.ContainsKey(t)) {
                return true;
            }

            var u = Nullable.GetUnderlyingType(t);
            if (u != null && mSerialTable.ContainsKey(u)) return true;

            return false;
        }

        internal object getDefaultValue(Type t) {
            if (t.IsValueType) {
                return Activator.CreateInstance(t);
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 为某种参数或返回值类型，注册一个序列化和反序列化器。此方法不是线程安全的。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serialFunc"></param>
        /// <param name="deserialFunc"></param>
        public void RegisterSerialableType<T>(Action<object, BinaryWriter> serialFunc, Func<BinaryReader, object> deserialFunc) {
            mSerialTable.Add(typeof(T), new TypeSerialInfo() { serialFunc = serialFunc, deserialFunc = deserialFunc });
        }

        private Dictionary<string, MethodCallingInfo> mMethodTable = new Dictionary<string, MethodCallingInfo>();
        private Dictionary<Type, TypeSerialInfo> mSerialTable = new Dictionary<Type, TypeSerialInfo>();
        private IList<object[]> objectCache;
    }

    internal class MethodCallingInfo {
        public Action<BinaryReader, BinaryWriter> MethodDelegate;
    }

    internal class TypeSerialInfo {
        public Action<object, BinaryWriter> serialFunc;
        public Func<BinaryReader, object> deserialFunc;
    }

    /// <summary>
    /// 返回给调用者的调用标记。
    /// </summary>
    [Flags]
    internal enum ReturnFlags : byte {
        /// <summary>
        /// 代表该方法返回了异常。
        /// </summary>
        IsThrowed = 1,
        /// <summary>
        /// 代表指定的方法不存在。
        /// </summary>
        NotFound = 2,
        /// <summary>
        /// 代表指定的方法执行成功。
        /// </summary>
        OK = 4
    }
}
