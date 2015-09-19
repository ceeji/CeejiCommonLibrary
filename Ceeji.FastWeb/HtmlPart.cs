using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ceeji.FastWeb.HtmlElements;

namespace Ceeji.FastWeb
{
    /// <summary>
    /// 代表 Html 页面的一部分。
    /// </summary>
    public abstract class HtmlPart
    {
        protected HtmlPart() {
            Scripts = new List<Script>();
            Styles = new List<Style>();
        }
        /// <summary>
        /// 将对象所表达的 Html 内容输出至流。
        /// </summary>
        /// <param name="s"></param>
        public abstract void WriteToStream(Stream s, HtmlOutputFormat format, int indent);
        /// <summary>
        /// 返回对象所表达的 Html 内容。
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return ToString(HtmlOutputFormat.Indent);
        }
        /// <summary>
        /// 返回对象所表达的 Html 内容。
        /// </summary>
        /// <returns></returns>
        public string ToString(HtmlOutputFormat format) {
            using (var ms = new MemoryStream()) {
                this.WriteToStream(ms, format, 0);
                return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }
        /// <summary>
        /// 设置运行此 HtmlPart 所需要的脚本。
        /// </summary>
        public IList<Script> Scripts { get; private set; }
        /// <summary>
        /// 设置运行此 HtmlPart 所需要的样式表。
        /// </summary>
        public IList<Style> Styles { get; private set; }

        /// <summary>
        /// 获取或设置此 AsyncHtmlPart 的 ID。
        /// </summary>
        public string ID {
            get { return this.mId; }
            set {
                if (value.Any(x => !allowdIDChars.Contains(x))) {
                    throw new ArgumentOutOfRangeException("ID 非法");
                }
                this.mId = value;
            }
        }

        private string mId;
        private static char[] allowdIDChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ01234567889_-".ToCharArray();
    }

    /// <summary>
    /// 代表 Html 的输出格式
    /// </summary>
    public enum HtmlOutputFormat {
        /// <summary>
        /// 指示 Html 尽可能缩进
        /// </summary>
        Indent,
        /// <summary>
        /// 指示 Html 尽可能压缩
        /// </summary>
        Zipped
    }
}
