using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.FastWeb.HtmlElements {
    public class Script : HtmlElement {
        private Script()
            : base("script") {

        }

        /// <summary>
        /// 使用指定的 innerScript 创建新实例。
        /// </summary>
        /// <param name="javascriptContent">要添加的 javaScript 正文。</param>
        public Script(string javascriptContent, string type = "text/javascript") : this() {
            this.Attributes["type"] = type ?? "text/javascript";
            this.InnerHtml = new Raw(javascriptContent);
        }

        public string SrcAttribute { get { return this.Attributes["src"]; } }

        public string TypeAttribult { get { return this.Attributes["type"]; } set { this.Attributes["type"] = value; } }

        /// <summary>
        /// 使用指定的 src 创建新实例。
        /// </summary>
        /// <param name="src">要指定的 src 属性</param>
        public Script(string src)
            : this() {
            this.Attributes["type"] = "text/javascript";
            this.Attributes["src"] = src;
        }
    }

    public class Style : HtmlElement {
        private Style()
            : base("style") {

        }

        /// <summary>
        /// 使用指定的 innerStyle 创建新实例。
        /// </summary>
        /// <param name="content"></param>
        public Style(string styleContent, string type = "text/css")
            : this() {
            this.Attributes["type"] = type;
            this.InnerHtml = new Raw(styleContent);
        }

        public string SrcAttribute { get { try { return this.Attributes["src"]; } catch { return null; } } }

        public string TypeAttribult { get { try { return this.Attributes["type"]; } catch { return null; } } set { this.Attributes["type"] = value; } }

        /// <summary>
        /// 使用指定的 innerStyle 创建新实例。
        /// </summary>
        /// <param name="content"></param>
        public Style(string src)
            : base("link") {
            this.Attributes["type"] = "text/css";
            this.Attributes["href"] = src;
            this.Attributes["rel"] = "stylesheet";
        }
    }
}
