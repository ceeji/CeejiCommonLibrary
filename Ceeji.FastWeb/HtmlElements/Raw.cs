using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.FastWeb.HtmlElements {
    public class Raw : HtmlPart {
        /// <summary>
        /// 创建 Raw 的新实例。
        /// </summary>
        public Raw(string innerHtml) {
            this.InnerHtml = innerHtml;
        }

        public string InnerHtml { get; set; }

        public override void WriteToStream(System.IO.Stream s, HtmlOutputFormat format, int indent) {
            var buffer = Encoding.UTF8.GetBytes(format == HtmlOutputFormat.Indent ? (InnerHtml + Environment .NewLine) : InnerHtml);
            s.Write(buffer, 0, buffer.Length);
            s.Flush();
        }
    }
}
