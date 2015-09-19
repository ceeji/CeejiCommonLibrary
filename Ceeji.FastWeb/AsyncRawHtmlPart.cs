using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.FastWeb {
    public class AsyncRawHtmlPart : AsyncHtmlPart {
        public AsyncRawHtmlPart(string id, Func<HtmlPart> callback) {
            this.GetRawCallback = callback;
            this.ID = id;
        }

        public Func<HtmlPart> GetRawCallback { get; set; }

        public override Task PrepareStreamAsync() {
            return Task.Run(() => {
                content = GetRawCallback();
            });
        }

        public override void WriteToStream(System.IO.Stream s, HtmlOutputFormat format, int indent) {
            content.WriteToStream(s, format, indent);
        }

        private HtmlPart content;
    }
}
