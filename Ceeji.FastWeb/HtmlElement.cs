using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.FastWeb {
    /// <summary>
    /// 代表一个 Html 元素。
    /// </summary>
    public class HtmlElement : HtmlPart {
        /// <summary>
        /// 创建 Html 元素的新实例。
        /// </summary>
        /// <param name="tagName">要创建的元素的标签名。</param>
        /// <exception cref="System.ArgumentException">当标签名无效时返回该异常。</exception>
        public HtmlElement(string tagName) {
            // 检查tag为空
            if (tagName == null || tagName == string.Empty)
                throw new ArgumentNullException("tagName 不能为空");

            // 检查tag为合法 html5 元素
            if (Array.BinarySearch(sTagList, tagName) == -1)
                throw new ArgumentException("tagName is not standerd HTML5 tag");

            this.TagName = tagName;
            this.Attributes = new AttributeDictionary();
        }

        /// <summary>
        /// 返回或设置 HtmlElement 的子元素。
        /// </summary>
        public HtmlPart InnerHtml { get; set; }


        public override void WriteToStream(System.IO.Stream s, HtmlOutputFormat format, int indent) {
            var writer = new StreamWriter(s);
            var writeEndTag = (InnerHtml != null) || TagName == "script" || TagName == "div" || TagName == "style";

            if (format == HtmlOutputFormat.Indent)
                writer.Write(new string(' ', indent)); // 缩进

            writer.Write("<");
            writer.Write(this.TagName);
            var htmlAttr = GetAttributesHtml();
            if (htmlAttr != string.Empty) {
                writer.Write(" ");
                writer.Write(htmlAttr);
            }
            if (!writeEndTag) {
                writer.Write("/>");
                if (format == HtmlOutputFormat.Indent)
                    writer.WriteLine();
            }
            else {
                writer.Write(">");
                if (InnerHtml != null) {
                    if (format == HtmlOutputFormat.Indent)
                        writer.WriteLine();

                    writer.Flush();
                    InnerHtml.WriteToStream(s, format, indent + 4);
                    if (format == HtmlOutputFormat.Indent) {
                        writer.Write(new string(' ', indent)); // 缩进
                    }
                    
                }
                writer.Write("</");
                writer.Write(this.TagName);
                writer.Write(">");
                if (format == HtmlOutputFormat.Indent) {
                    writer.WriteLine();
                }
            }

            writer.Flush();
        }

        /// <summary>
        /// 返回 Html 元素的所有属性所组成的 Html。
        /// </summary>
        /// <returns></returns>
        public string GetAttributesHtml() {
            return string.Join(" ", this.Attributes.Select(x => x.Key + "=\"" + x.Value + "\"").ToArray());
        }

        static HtmlElement() {
            // 初始化支持的 Tag 列表。
            sTagList = new string[] { "html", "head", "title", "base", "link", "meta", "style", "script", "noscript", "template", "body", "section", "nav", "article", "aside", "h1", "header", "footer", "address", "main", "p", "hr", "pre", "blockquote", "ol", "ul", "li", "dl", "dt", "dd", "figure", "figcaption", "div", "a", "em", "strong", "small", "s", "cite", "q", "dfn", "abbr", "data", "time", "code", "var", "samp", "kbd", "sub", "i", "b", "u", "mark", "ruby", "rt", "rp", "bdi", "bdo", "span", "br", "wbr", "ins", "del", "img", "iframe", "embed", "object", "param", "video", "audio", "source", "track", "canvas", "map", "area", "svg", "math", "table", "caption", "colgroup", "col", "tbody", "thead", "tfoot", "tr", "td", "th", "form", "fieldset", "legend", "label", "input", "button", "select", "datalist", "optgroup", "option", "textarea", "keygen", "output", "progress", "meter", "details", "summary", "menuitem", "menu" };
            sTagList = sTagList.OrderBy(x => x).ToArray();
        }

        /// <summary>
        /// 获取对象的 Html 标签名。
        /// </summary>
        public string TagName { get; private set; }

        /// <summary>
        /// 获取对象的 Html 属性集的列表。
        /// </summary>
        public IDictionary<string, string> Attributes {
            get;
            private set;
        }

        /// <summary>
        /// 返回或设置对象的 ID。
        /// </summary>
        public string ID {
            get {
                if (this.Attributes.ContainsKey("id"))
                    return this.Attributes["id"];

                return null;
            }
            set {
                Attributes["id"] = value;
            }
        }

        private static string[] sTagList;

        public class AttributeDictionary : IDictionary<string, string> {

            private Dictionary<string, string> dic = new Dictionary<string, string>();

            #region IDictionary<string,string> 成员

            public void Add(string key, string value) {
                dic.Add(key.ToLowerInvariant(), value);
            }

            public bool ContainsKey(string key) {
                return dic.ContainsKey(key);
            }

            public ICollection<string> Keys {
                get { return dic.Keys; }
            }

            public bool Remove(string key) {
                return dic.Remove(key);
            }

            public bool TryGetValue(string key, out string value) {
                return dic.TryGetValue(key, out value);
            }

            public ICollection<string> Values {
                get { return dic.Values; }
            }

            public string this[string key] {
                get {
                    return dic[key.ToLowerInvariant()];
                }
                set {
                    dic[key] = value;
                }
            }

            #endregion

            #region ICollection<KeyValuePair<string,string>> 成员

            public void Add(KeyValuePair<string, string> item) {
                dic.Add(item.Key, item.Value);
            }

            public void Clear() {
                dic.Clear();
            }

            public bool Contains(KeyValuePair<string, string> item) {
                return dic.Contains(item);
            }

            public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) {
                dic.ToArray().CopyTo(array, arrayIndex);
            }

            public int Count {
                get { return dic.Count; }
            }

            public bool IsReadOnly {
                get { return false; }
            }

            public bool Remove(KeyValuePair<string, string> item) {
                if (this.Contains(item)) {
                    return this.Remove(item.Key);
                }
                return false;
            }

            #endregion

            #region IEnumerable<KeyValuePair<string,string>> 成员

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator() {
                return dic.GetEnumerator();
            }

            #endregion

            #region IEnumerable 成员

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return this.GetEnumerator();
            }

            #endregion
        }
    }
}
