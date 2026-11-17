using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CardKeyAuth
{
    /// <summary>
    /// 检测本机是否运行常见调试 / 逆向 / 抓包 / 注入类风险软件。
    /// Windows 用 Toolhelp32 枚举（IL2CPP 下 System.Diagnostics.Process.GetProcesses 不可靠）。
    /// 比较时用不含扩展名、忽略大小写的进程名。
    /// </summary>
    public static class RiskProcessScanner
    {
        static readonly string[] RiskyNames =
        {
            // IDA / Ghidra / Binary Ninja
            "ida", "ida64", "idaq", "idaq64", "idat", "idat64", "ida-win64",
            "ghidra", "ghidrarun", "binaryninja",
            // 调试器
            "x64dbg", "x32dbg", "x96dbg", "ollydbg", "windbg", "windbgx", "cdb",
            "immunitydebugger", "edb",
            // .NET 逆向
            "dnspy", "dnspy-x86", "ilspy", "dotpeek", "de4dot", "megadumper", "extremedumper",
            // 抓包 / 代理
            "fiddler", "fiddler everywhere", "charles", "wireshark", "httpdebuggerpro",
            "proxifier", "mitmproxy", "burpsuite",
            // 分析 / 注入 / Cheat
            "processhacker", "processhacker2", "procexp", "procexp64", "procmon", "procmon64",
            "cheatengine-x86_64", "cheatengine-i386", "cheat engine", "ce-x64",
            "scylla_x64", "scylla_x86", "x64netdumper", "hxd", "reclass", "reclass.net",
            "apimonitor", "apimonitor-x64", "apimonitor-x86",
            // 常见 agent / 注入器命名
            "frida", "frida-server", "frida-helper", "injector", "dllinjector",
            "softice", "syser",
            // AI 客户端 / Coding Agent（用于辅助逆向、改码、出利用）
            "cursor", "cursor helper", "trae", "trae cn", "windsurf", "windsurf helper",
            "claude", "chatgpt", "openai chatbot", "copilot", "githubdesktop",
            "codeium", "tabnine", "continue", "aide", "void", "zed", "kiro",
            "qoder", "lingma", "comate", "codebuddy", "codegeex", "cody",
            "supermaven", "pieces", "warp", "warp-terminal",
            "lm studio", "lmstudio", "ollama", "ollama app", "anythingllm",
            "chatbox", "cherry studio", "cherrystudio", "lobechat", "lobe-chat",
            "sillytavern", "typingmind", "monica", "sider", "perplexity",
            "poe", "gemini", "bard", "doubao", "豆包", "kimi", "通义", "yuanbao", "元宝",
            "iflytek", "讯飞星火", "coze", "dify", "open-webui", "textgen",
            "stable diffusion", "comfyui", "a1111",
        };

        const uint Th32CsSnapProcess = 0x00000002;
        const int MaxPath = 260;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessEntry32W
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool Process32FirstW(IntPtr hSnapshot, ref ProcessEntry32W lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool Process32NextW(IntPtr hSnapshot, ref ProcessEntry32W lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        public static List<string> Scan()
        {
            var hits = new List<string>();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                EnumerateWindowsProcessNames(hits);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CardKey] RiskProcessScanner: " + ex.Message);
            }
#endif
            return hits;
        }

        static void EnumerateWindowsProcessNames(List<string> hits)
        {
            var snap = CreateToolhelp32Snapshot(Th32CsSnapProcess, 0);
            if (snap == IntPtr.Zero || snap == InvalidHandleValue)
            {
                Debug.LogWarning("[CardKey] RiskProcessScanner: CreateToolhelp32Snapshot failed");
                return;
            }

            try
            {
                var entry = new ProcessEntry32W();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(ProcessEntry32W));
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!Process32FirstW(snap, ref entry))
                    return;

                do
                {
                    var key = NormalizeProcessName(entry.szExeFile);
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!IsRisky(key) || !seen.Add(key)) continue;
                    hits.Add(key);
                } while (Process32NextW(snap, ref entry));
            }
            finally
            {
                CloseHandle(snap);
            }
        }

        /// <summary>去掉路径与 .exe，转小写。</summary>
        static string NormalizeProcessName(string szExeFile)
        {
            if (string.IsNullOrEmpty(szExeFile)) return null;
            var name = szExeFile.Trim();
            var slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
            if (slash >= 0 && slash + 1 < name.Length)
                name = name.Substring(slash + 1);
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            return name.ToLowerInvariant();
        }

        static bool IsRisky(string processNameNoExt)
        {
            if (string.IsNullOrEmpty(processNameNoExt)) return false;
            for (int i = 0; i < RiskyNames.Length; i++)
            {
                var n = RiskyNames[i];
                if (processNameNoExt == n) return true;
                // 兼容 "cheatengine-x86_64-7.5" 这类带版本后缀
                if (processNameNoExt.StartsWith(n + "-", StringComparison.Ordinal) ||
                    processNameNoExt.StartsWith(n + "_", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    /// <summary>本机风险预警次数（PlayerPrefs）。</summary>
    public static class RiskWarnStore
    {
        const string PrefCount = "ck_risk_warn_count";
        const string PrefResetAt = "ck_risk_warn_reset_at";
        public const int DefaultMax = 3;

        public static int GetCount()
        {
            return Math.Max(0, PlayerPrefs.GetInt(PrefCount, 0));
        }

        public static void Reset()
        {
            PlayerPrefs.SetInt(PrefCount, 0);
            PlayerPrefs.Save();
        }

        public static int Increment()
        {
            var next = GetCount() + 1;
            PlayerPrefs.SetInt(PrefCount, next);
            PlayerPrefs.Save();
            return next;
        }

        /// <summary>
        /// 若服务端解除拉黑/加白产生的 reset_at 更新，则清零本地预警次数。
        /// </summary>
        public static void ApplyServerReset(long riskWarnResetAt)
        {
            if (riskWarnResetAt <= 0) return;
            long applied = 0;
            long.TryParse(PlayerPrefs.GetString(PrefResetAt, "0"), out applied);
            if (riskWarnResetAt <= applied) return;
            PlayerPrefs.SetInt(PrefCount, 0);
            PlayerPrefs.SetString(PrefResetAt, riskWarnResetAt.ToString());
            PlayerPrefs.Save();
        }
    }
}
