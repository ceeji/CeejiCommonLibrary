using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ceeji.Data;

namespace Ceeji.FastWeb {
    /// <summary>
    /// 提供和 Http 相关的实用方法。
    /// </summary>
    public static class HttpUtil {
        public static string EncodeHtml(string source) {
            return WebUtility.HtmlEncode(source);
        }

        /// <summary>
        /// 序列化异步 HtmlPart，实现浏览器动态解析并加载，且不需要使用 Ajax。
        /// </summary>
        /// <param name="part"></param>
        /// <returns></returns>
        public static string SerializeAsyncPart(HtmlPart part) {
            var obj = new {
                id = part.ID,
                content = part.ToString()
            };

            var json = JsonConvert.SerializeObject(obj);

            var js = "<script>showPart(" + json + ")</script>";
            return js;
        }

        /// <summary>
        /// 根据用户传递的 User Agent 字段，判断用户从何浏览器进行浏览。
        /// </summary>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public static DeviceType GetDeviceTypeFromUserAgent(string userAgent) {
            var type = DeviceType.None;

            // 先判断一些简单的情况
            if (userAgent.IndexOf("Android", StringComparison.InvariantCultureIgnoreCase) != -1 && userAgent.IndexOf("Linux", StringComparison.InvariantCultureIgnoreCase) != -1) { // Android
                type |= DeviceType.Android;
            }
            if (userAgent.IndexOf("msie ", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("; Trident/") != -1) {
                type |= DeviceType.IE;
            }
            if (userAgent.IndexOf("(iPhone; ", StringComparison.InvariantCultureIgnoreCase) != -1) {
                type |= DeviceType.iPhone;
            }
            if (userAgent.IndexOf("(iPod; ", StringComparison.InvariantCultureIgnoreCase) != -1) {
                type |= DeviceType.iPod;
            }
            if (userAgent.IndexOf("(iPad; ", StringComparison.InvariantCultureIgnoreCase) != -1) {
                type |= DeviceType.iPad;
                type |= DeviceType.Tablet;
            }
            if (type.HasFlag(DeviceType.iPad) || type.HasFlag(DeviceType.iPhone) || type.HasFlag(DeviceType.iPod)) {
                type |= DeviceType.iOS;
            }
            if (userAgent.IndexOf("AppleWebKit/", StringComparison.InvariantCultureIgnoreCase) != -1) {
                type |= DeviceType.Webkit;
            }
            
            // 是否为搜索引擎
            if (userAgent.IndexOf("bingbot", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("Sosospider", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("Yahoo! Slurp", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("spider", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("Googlebot", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("Baiduspider", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("Transcoder", StringComparison.InvariantCultureIgnoreCase) != -1 || userAgent.IndexOf("YoudaoBot", StringComparison.InvariantCultureIgnoreCase) != -1) {
                type |= DeviceType.Spider;
            }

            // 是否为边做边学内置浏览器
            if (userAgent.IndexOf("BzbxBroswer/", StringComparison.InvariantCultureIgnoreCase) != -1)
                type |= DeviceType.BzbxApp;
            

            // 暂时把所有 Android 设备归类为 Smartphone
            if (type.HasFlag(DeviceType.Android) || type.HasFlag(DeviceType.iPhone) || type.HasFlag(DeviceType.iPod)) {
                type |= DeviceType.Smartphone;
            }
            if ((!type.HasFlag(DeviceType.Tablet) && !type.HasFlag(DeviceType.Smartphone)) 
                || type.HasFlag(DeviceType.Spider)
                || type.HasFlag(DeviceType.IE)) {
                // 如果用户使用 IE、用户为搜索引擎，或用户不能被准确识别为平板和智能手机，则认为用户为电脑登陆
                type |= DeviceType.Computer;
            }

            return type;
        }
    }

    /// <summary>
    /// 代表客户端浏览器所在的设备类型
    /// </summary>
    [Flags]
    public enum DeviceType {
        /// <summary>
        /// 代表空值
        /// </summary>
        None = 0,
        /// <summary>
        /// 代表设备为计算机
        /// </summary>
        Computer = 1,
        /// <summary>
        /// 代表设备为智能手机
        /// </summary>
        Smartphone = 2,
        /// <summary>
        /// 代表设备为平板电脑
        /// </summary>
        Tablet = 2 << 1,
        /// <summary>
        /// 代表设备为 iPhone
        /// </summary>
        iPhone = 2 << 2,
        /// <summary>
        /// 代表设备为 iPad
        /// </summary>
        iPad = 2 << 3,
        /// <summary>
        /// 代表设备为 iOS 设备
        /// </summary>
        iOS = 2 << 4,
        /// <summary>
        /// 代表设备为 Android 设备
        /// </summary>
        Android = 2 << 5,
        /// <summary>
        /// 代表设备使用了 Webkit 内核
        /// </summary>
        Webkit = 2 << 6,
        /// <summary>
        /// 代表设备使用了 Firefox
        /// </summary>
        Firefox = 2 << 7,
        /// <summary>
        /// 代表设备使用了 IE 内核
        /// </summary>
        IE = 2 << 8,
        /// <summary>
        /// 代表设备使用了 iPod
        /// </summary>
        iPod = 2 << 9,
        /// <summary>
        /// 代表设备为搜索引擎
        /// </summary>
        Spider = 2 << 10,
        /// <summary>
        /// 代表设备为边做边学内置浏览器
        /// </summary>
        BzbxApp = 2 << 11,
    }
}
