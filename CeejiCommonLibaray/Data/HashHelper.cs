using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ceeji.Data {
    /// <summary>
    /// 哈希支持类，支持对所有继承于 HashAlgorithm 类型的类计算哈希，支持 MD5、SHA1、SHA256、SHA512 的快捷计算。
    /// </summary>
    public static class HashHelper {
        /// <summary>
        /// 计算 SHA256 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA256(this Stream input) {
            return ComputeStringHash(pGetSHA256(), input);
        }

        /// <summary>
        /// 计算 SHA256 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA256(this byte[] input) {
            return ComputeStringHash(pGetSHA256(), input);
        }

        /// <summary>
        /// 计算 SHA256 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA256(this string input, Encoding encoding = null) {
            var _encoding = encoding == null ? DefaultEncoding : encoding;
            return SHA256(_encoding.GetBytes(input));
        }

        /// <summary>
        /// 计算 SHA1 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA1(this Stream input) {
            return ComputeStringHash(pGetSHA1(), input);
        }

        /// <summary>
        /// 计算 SHA1 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA1(this byte[] input) {
            return ComputeStringHash(pGetSHA1(), input);
        }

        /// <summary>
        /// 计算 SHA1 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA1(this string input, Encoding encoding = null) {
            var _encoding = encoding == null ? DefaultEncoding : encoding;
            return SHA1(_encoding.GetBytes(input));
        }

        /// <summary>
        /// 计算 SHA512 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA512(this Stream input) {
            return ComputeStringHash(pGetSHA512(), input);
        }

        /// <summary>
        /// 计算 SHA512 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA512(this byte[] input) {
            return ComputeStringHash(pGetSHA512(), input);
        }

        /// <summary>
        /// 计算 SHA512 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SHA512(this string input, Encoding encoding = null) {
            var _encoding = encoding == null ? DefaultEncoding : encoding;
            return SHA512(_encoding.GetBytes(input));
        }

        /// <summary>
        /// 计算 MD5 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string MD5(this Stream input) {
            return ComputeStringHash(pGetMD5(), input);
        }

        /// <summary>
        /// 计算 MD5 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string MD5(this byte[] input) {
            return ComputeStringHash(pGetMD5(), input);
        }

        /// <summary>
        /// 计算 MD5 的值。
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string MD5(this string input, Encoding encoding = null) {
            var _encoding = encoding == null ? DefaultEncoding : encoding;
            return MD5(_encoding.GetBytes(input));
        }

        /// <summary>
        /// 使用指定的哈希算法计算哈希。
        /// </summary>
        /// <param name="algorithm"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ComputeRawHash(this byte[] input, HashAlgorithm algorithm) {
            return algorithm.ComputeHash(input);
        }

        /// <summary>
        /// 使用指定的哈希算法计算哈希。
        /// </summary>
        /// <param name="algorithm"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] ComputeRawHash(this Stream input, HashAlgorithm algorithm) {
            return algorithm.ComputeHash(input);
        }

        /// <summary>
        /// 使用指定的哈希算法计算哈希，返回字符串值。
        /// </summary>
        /// <param name="algorithm"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ComputeStringHash(this Stream input, HashAlgorithm algorithm) {
            var tmpByte = ComputeRawHash(input, algorithm);
            return BitConverter.ToString(tmpByte).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 使用指定的哈希算法计算哈希，返回字符串值。
        /// </summary>
        /// <param name="algorithm"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string ComputeStringHash(HashAlgorithm algorithm, Stream input) {
            return ComputeStringHash(input, algorithm);
        }

        /// <summary>
        /// 使用指定的哈希算法计算哈希，返回字符串值。
        /// </summary>
        /// <param name="algorithm"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string ComputeStringHash(HashAlgorithm algorithm, byte[] input) {
            return ComputeStringHash(input, algorithm);
        }

        /// <summary>
        /// 使用指定的哈希算法计算哈希，返回字符串值。
        /// </summary>
        /// <param name="algorithm"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ComputeStringHash(this byte[] input, HashAlgorithm algorithm) {
            var tmpByte = ComputeRawHash(input, algorithm);
            
            return BitConverter.ToString(tmpByte).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 设置对字符串哈希时使用的默认编码。
        /// </summary>
        public static Encoding DefaultEncoding {
            get {
                return mEncodingDefault;
            }
            set {
                mEncodingDefault = value;
            }
        }

        /// <summary>
        /// 获取 SHA256 算法的实例。
        /// </summary>
        public static HashAlgorithm SHA256Algorithm {
            get {
                return pGetSHA256();
            }
        }

        /// <summary>
        /// 获取 SHA512 算法的实例。
        /// </summary>
        public static HashAlgorithm SHA512Algorithm {
            get {
                return pGetSHA512();
            }
        }

        /// <summary>
        /// 获取 SHA1 算法的实例。
        /// </summary>
        public static HashAlgorithm SHA1Algorithm {
            get {
                return pGetSHA1();
            }
        }

        /// <summary>
        /// 获取 MD5 算法的实例。
        /// </summary>
        public static HashAlgorithm MD5Algorithm {
            get {
                return pGetMD5();
            }
        }

        private static SHA256 pGetSHA256() {
            if (objSHA256 == null) {
                return objSHA256 = new SHA256Managed();
            }
            return objSHA256;
        }

        private static SHA512 pGetSHA512() {
            if (objSHA512 == null) {
                return objSHA512 = new SHA512Managed();
            }
            return objSHA512;
        }

        private static SHA1 pGetSHA1() {
            if (objSHA1 == null) {
                return objSHA1 = new SHA1Managed();
            }
            return objSHA1;
        }

        private static MD5 pGetMD5() {
            if (objMD5 == null) {
                return objMD5 = new MD5Cng();
            }
            return objMD5;
        }

        /// <summary>
        /// 将密码哈希，使用盐。用户每次创建或者修改密码一定要使用一个新的随机的盐。
        /// </summary>
        /// <param name="password"></param>
        /// <param name="salt"></param>
        /// <returns></returns>
        public static string GetHashedPassword(string password, string salt) {
            return SHA256(password + salt);
        }

        [ThreadStatic]
        private static SHA256 objSHA256 = null;
        [ThreadStatic]
        private static SHA512 objSHA512 = null;
        [ThreadStatic]
        private static SHA1 objSHA1 = null;
        [ThreadStatic]
        private static MD5 objMD5 = null;
        private static Encoding mEncodingDefault = Encoding.UTF8;

    }
}
