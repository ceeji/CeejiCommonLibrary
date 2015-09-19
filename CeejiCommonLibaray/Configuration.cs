using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Ceeji {
    /// <summary>
    /// 用于载入、保存、读取或修改配置的类。配置可以容纳所有可以被序列化的类型，也支持配置的嵌套存储。
    /// </summary>
    [XmlType(TypeName = "Configuration")]
    public class Configuration : List<ConfigItemPair<string, object>> {
        /// <summary>
        /// 创建 Ceeji.Testing.Configuration.TestingConfiguration 的新实例。
        /// </summary>
        public Configuration() {

        }

        /// <summary>
        /// 创建 Ceeji.Testing.Configuration.TestingConfiguration 的新实例。
        /// </summary>
        /// <param name="source">要从中复制数据的 System.Collections.Generic.IDictionary&lt;TKey, TValue&gt;。 </param>
        public Configuration(List<ConfigItemPair<string, object>> source) {
            foreach (var pair in source)
                this.Add(new ConfigItemPair<string, object>(pair.Key, pair.Value));

            this.Sort((x, y) => x.Key.CompareTo(y.Key));
        }

        /// <summary>
        /// 返回或设置指定 key 的元素。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public object this[string key] {
            get {
                var ret = this.FindIndex(x => x.Key == key);
                if (ret == -1)
                    return null;
                return this[ret].Value;
            }
            set {
                var ret = this.FindIndex(x => x.Key == key);
                if (ret == -1) {
                    this.Add(new ConfigItemPair<string, object>(key, value));
                }
                else {
                    this[ret] = new ConfigItemPair<string, object>(key, value);
                }
            }
        }

        /// <summary>
        /// 从文件中加载配置。
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static Configuration LoadFromXMLFile(string filename, Encoding encode = null) {
            return LoadFromXMLText(File.ReadAllText(filename, encode == null ? Encoding.UTF8 : encode));
        }

        /// <summary>
        /// 从文件中加载配置。
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        /// <exception cref="System.TypeLoadException">当特定的类型无法加载时导致的反序列化失败。</exception>
        public static Configuration LoadFromXMLText(string text) {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

            XmlDocument xd = new XmlDocument();
            xd.Load(ms);
            ms.Position = 0;
            var extraList = xd.SelectNodes("/Configuration/Config[@Key='___extraTypes___']/Value/string").Cast<XmlNode>().Select(x => Type.GetType(x.InnerText)).ToList();
            xd = null;
            extraList = extraList.Concat(knownTypes).Distinct().ToList();
            extraList.Remove(typeof(Configuration));
            extraList.Remove(typeof(Configuration[]));

            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(Configuration), extraList.ToArray());

            var r = (Configuration)serializer.Deserialize(ms);

            var index = r.FindIndex(x => x.Key == "___extraTypes___");

            if (index != -1)
                r.RemoveAt(index);

            r.Sort((x, y) => x.Key.CompareTo(y.Key));

            return r;
        }

        /// <summary>
        /// 序列化配置到文本。
        /// </summary>
        /// <returns></returns>
        public string SaveToXML(ICollection<Type> extraTypes = null) {
            return serialization(this, extraTypes);
        }

        private static Type[] knownTypes = new Type[] {
            typeof(int),
            typeof(double),
            typeof(decimal),
            typeof(string),
            typeof(bool),
            typeof(uint),
            typeof(ulong),
            typeof(long),
            typeof(ushort),
            typeof(byte),
            typeof(char),
            typeof(short),
            typeof(Configuration)
        };

        static Configuration() {
            knownTypes = knownTypes.Concat(knownTypes.Select(x => x.MakeArrayType())).ToArray();
            // knownTypes = knownTypes.Concat(knownTypes.Select(x => typeof(Dictionary<>) x.MakeArrayType())).ToArray();
        }

        private void preserialization(Configuration c, List<Type> typeList) {
            foreach (var p in c) {
                var v = p.Value.GetType();

                if (!knownTypes.Contains(v) && !typeList.Contains(v)) {
                    typeList.Add(v);
                }
                if (v.IsAssignableFrom(typeof(Configuration))) {
                    preserialization(p.Value as Configuration, typeList);
                }
            }
        }

        private string serialization(Configuration c, ICollection<Type> extraTypes) {
            List<Type> typeList;
            if (extraTypes != null)
                typeList = new List<Type>(extraTypes);
            else
                typeList = new List<Type>();

            // 递归获取所有其他额外类型
            preserialization(c, typeList);

            // 额外类型查重
            typeList = typeList.Distinct().ToList();
            typeList.Remove(typeof(Configuration));

            // 保存额外类型
            if (typeList.Count > 0) {
                this.Add(new ConfigItemPair<string, object>("___extraTypes___", typeList.Select(x => x.FullName).ToArray()));
            }

            typeList = typeList.Concat(knownTypes).ToList();
            typeList.Remove(typeof(Configuration));
            typeList.Remove(typeof(Configuration[]));

            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(Configuration), typeList.ToArray());
            //执行序列化并将序列化结果输出到控制台
            using (var res = new MemoryStream()) {
                serializer.Serialize(res, c);
                var text = Encoding.UTF8.GetString(res.ToArray());

                if (this.Count != 0 && this[this.Count - 1].Key == "___extraTypes___")
                    this.RemoveAt(this.Count - 1);
                return text;
            }
        }
    }

    /// <summary>
    /// 代表配置项。
    /// </summary>
    /// <typeparam name="K">键的类型。</typeparam>
    /// <typeparam name="V">值的类型。</typeparam>
    [Serializable]
    [XmlType(TypeName = "Config")]
    public struct ConfigItemPair<K, V> {
        public ConfigItemPair(K k, V v)
            : this() {
            this.Key = k;
            this.Value = v;
        }

        [XmlAttribute]
        public K Key { get; set; }
        
        public V Value { get; set; }

        public override string ToString() {
            return Key + " = " + (Value == null ? "null" : Value.ToString());
        }
    }
}
