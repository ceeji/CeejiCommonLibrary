using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ceeji.FastWeb.HtmlElements;

namespace Ceeji.FastWeb {
    /// <summary>
    /// 代表一个 Html 请求的响应。
    /// </summary>
    public class HtmlResponse : HtmlPart {
        /// <summary>
        /// 创建 HtmlResponse 的新实例。
        /// </summary>
        public HtmlResponse() {
            Parts = new HtmlPart[0];
            ContentType = "text/html; charset=utf-8";
            UseBigPipe = true;
        }

        /// <summary>
        /// 创建 HtmlResponse 的新实例。
        /// </summary>
        /// <param name="title">页面的标题。</param>
        public HtmlResponse(string title) : this() {
            this.Title = title;
        }

        /// <summary>
        /// 返回或设置页面的标题。
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 返回或设置页面的 Part 列表。
        /// </summary>
        public IEnumerable<HtmlPart> Parts { get; set; }

        /// <summary>
        /// 返回或设置正文类型。
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// 获取或设置是否使用 BigPipe 进行输出。默认为 true，设置为 false 可以方便搜索引擎进行索引。
        /// </summary>
        public bool UseBigPipe { get; set; }

        public override void WriteToStream(System.IO.Stream s, HtmlOutputFormat format, int indent) {
            var writer = new StreamWriter(s);
            writer.Write(format == HtmlOutputFormat.Indent ? html_header_before_content_type : html_header_before_content_type_zip);
            writer.Write(ContentType);
            writer.Write(format == HtmlOutputFormat.Indent ? html_header_before_title : html_header_before_title_zip);
            writer.Write(HttpUtil.EncodeHtml(this.Title));
            writer.Write("</title>");
            if (format == HtmlOutputFormat.Indent)
                writer.WriteLine();
            writer.Flush();

            // 输出脚本和 CSS
            var writeScriptAndCss = new Action<HtmlPart>(x => {
                foreach (var item in x.Scripts) {
                    item.WriteToStream(s, format, 4);
                }
                foreach (var item in x.Styles) {
                    item.WriteToStream(s, format, 4);
                }
            });

            // 添加用来支持 Bigpipe 的 JS 脚本
            if (UseBigPipe) {
                this.Scripts.Add(new Script("function showPart(part) {try{console.debug('received part ' + part.id)}catch(e){} document.getElementById(part.id).innerHTML=part.content;}", null));
            }
            writeScriptAndCss(this);

            foreach (var parts in this.Parts) {
                writeScriptAndCss(parts);
            }

            writer.Write("</head>");
            if (format == HtmlOutputFormat.Indent)
                writer.WriteLine();
            writer.Write("<body>");
            if (format == HtmlOutputFormat.Indent)
                writer.WriteLine();
            writer.Flush(); // 让客户端可以先下载 CSS
            s.Flush();

            if (UseBigPipe) {
                // 输出内容占位符
                foreach (var part in this.Parts) {
                    (new HtmlElement("div") { ID = part.ID }).WriteToStream(s, format, 4);
                    s.Flush();
                }
            }

            // 输出正文
            var partStatic = this.Parts.FindAll(x => !(x is AsyncHtmlPart));
            var partDynamic = this.Parts.FindAll(x => x is AsyncHtmlPart).Cast<AsyncHtmlPart>().ToArray();


            // 先输出静态内容
            foreach (var part in partStatic) {
                writePart(s, part, format);
            }

            // 再输出动态内容
            List<Task> tasks = new List<Task>();
            foreach (var part in partDynamic) {
                var task = part.PrepareStreamAsync();
                tasks.Add(task);
            }

            bool[] isFinish = new bool[partDynamic.Length];
            isFinish.Fill(false);
            var remainCount = partDynamic.Length;
            while (remainCount != 0) {
                var i = Task.WaitAny(tasks.WithIndex().FindAll(x => !isFinish[x.Index]).Select(y => y.Value).ToArray());
                remainCount--;
                isFinish[i] = true;
                //partDynamic[i].WriteToStream(s, format, 4);
                writePart(s, partDynamic[i], format);
            }

            // 输出页尾
            (new Script("try{finishDownload();}catch(e){}", null)).WriteToStream(s, format, 4);
            writer.Write("</body>");
            if (format == HtmlOutputFormat.Indent)
                writer.WriteLine();
            writer.Write("</html>");
            if (format == HtmlOutputFormat.Indent)
                writer.WriteLine();
            writer.Flush();
        }

        private void writePart(Stream s, HtmlPart part, HtmlOutputFormat format) {
            if (UseBigPipe) {
                var c = HttpUtil.SerializeAsyncPart(part);
                s.Write(c, true);
            }
            else {
                (new HtmlElement("div") { ID = part.ID, InnerHtml = part }).WriteToStream(s, format, 4);
                s.Flush();
            }
        }

        private const string html_header_before_content_type = @"<!DOCTYPE html>
<!--Made by Ceeji.FastWeb, Join us by sending email to job@bzbx.com.cn -->
<html>
<head>
    <meta http-equiv=" + "\"Content-Type\" content=\"";

        private const string html_header_before_title = "\"" + @">
    <" + "meta name=\"viewport\" content=\"width=device-width; initial-scale=1.0\"" + @">
    <" + "meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge,chrome=1\">" + @"
    <title>";

        private const string html_header_before_content_type_zip = @"<!DOCTYPE html>
<!--Made by Ceeji.FastWeb, Join us by sending email to job@bzbx.com.cn --><html><head><meta http-equiv=" + "\"Content-Type\" content=\"";

        private const string html_header_before_title_zip = "\"" + @"><" + "meta name=\"viewport\" content=\"width=device-width; initial-scale=1.0\"" + @"><" + "meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge,chrome=1\"><title>";
    }
}
