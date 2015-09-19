using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Ceeji.Win32
{
    /// <summary>
    /// 提供创建快捷方式的静态方法。
    /// </summary>
    public static class Shortcuts {
        /// <summary>
        /// 创建快捷方式。
        /// </summary>
        /// <param name="shortcutsPath">指定快捷方式的路径。</param>
        /// <param name="referencePath">指定快捷方式引用的目标路径。</param>
        /// <param name="allowOverride">指定是否允许覆盖已经存在的快捷方式。若不允许，则文件存在会导致引发 System.IOException。</param>
        public static void Create(string shortcutsPath, string referencePath, bool allowOverride = false) {
            Create(shortcutsPath, referencePath, true, null, null);
        }

        /// <summary>
        /// 创建快捷方式。
        /// </summary>
        /// <param name="shortcutsPath">指定快捷方式的路径。</param>
        /// <param name="referencePath">指定快捷方式引用的目标路径。</param>
        /// <param name="workingDirectory">指定快捷方式的工作目录，如果为 null，则自动设置为应用的目标路径的目录名。</param>
        /// <param name="description">指定快捷方式的友好描述，可以为 null。</param>
        /// <param name="allowOverride">指定是否允许覆盖已经存在的快捷方式。若不允许，则文件存在会导致引发 System.IOException。</param>
        /// <exception cref="IOException">指定的快捷方式已经存在。</exception>
        /// <param name="arguments">指定快捷方式的运行参数。可以为 null。</param>
        public static void Create(string shortcutsPath, string referencePath, bool allowOverride = false, string arguments = null, string workingDirectory = null, string description = null) {
            shortcutsPath = shortcutsPath.EndsWith(".lnk", StringComparison.InvariantCultureIgnoreCase) ? shortcutsPath : (shortcutsPath + ".lnk");
            if (File.Exists(shortcutsPath)) {
                if (allowOverride)
                    File.Delete(shortcutsPath);
                else
                    throw new IOException("Shortcuts already exists");
            }

            ClassHelper.throwIfNull(shortcutsPath, referencePath);

            IShellLink link = (IShellLink)new ShellLink();

            // setup shortcut information
            if (description != null)
                link.SetDescription(description);

            if (arguments != null)
                link.SetArguments(arguments);

            link.SetPath(referencePath);
            link.SetWorkingDirectory(workingDirectory ?? Path.GetDirectoryName(referencePath));

            // save it
            IPersistFile file = (IPersistFile)link;
            file.Save(shortcutsPath, false);
        }

        /// <summary>
        /// 创建快捷方式。
        /// </summary>
        /// <param name="folder">指定快捷方式的位置。</param>
        /// <param name="shortcutsName">指定快捷方式的名称，不必包括扩展名。</param>
        /// <param name="referencePath">指定快捷方式引用的目标路径。</param>
        /// <param name="workingDirectory">指定快捷方式的工作目录，如果为 null，则自动设置为应用的目标路径的目录名。</param>
        /// <param name="description">指定快捷方式的友好描述，可以为 null。</param>
        /// <param name="allowOverride">指定是否允许覆盖已经存在的快捷方式。若不允许，则文件存在会导致引发 System.IOException。</param>
        /// <param name="arguments">指定快捷方式的运行参数。可以为 null。</param>
        public static void Create(System.Environment.SpecialFolder folder, string shortcutsName, string referencePath, bool allowOverride = false, string arguments = null, string workingDirectory = null, string description = null) {
            ClassHelper.throwIfNull(shortcutsName, referencePath);

            Create(Path.Combine(Environment.GetFolderPath(folder), shortcutsName), referencePath, allowOverride, arguments, workingDirectory, description);
        }
    }

    static class ClassHelper {
        public static void throwIfNull(params object[] objs) {
            foreach (var item in objs) {
                if (item == null)
                    throw new ArgumentNullException();
            }
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLink {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
