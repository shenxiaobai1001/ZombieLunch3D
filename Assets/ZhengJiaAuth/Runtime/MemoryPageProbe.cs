using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// Windows PAGE_GUARD 缺页探针：诱饵页被进程内异常读取时置位。
    /// 跨进程 ReadProcessMemory 不一定能捕获，属尽力检测。
    /// </summary>
    public static class MemoryPageProbe
    {
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint MEM_RELEASE = 0x8000;
        const uint PAGE_READWRITE = 0x04;
        const uint PAGE_READONLY = 0x02;
        const uint PAGE_GUARD = 0x100;
        const uint STATUS_GUARD_PAGE_VIOLATION = 0x80000001;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int VectoredHandlerDelegate(IntPtr exceptionInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr AddVectoredExceptionHandler(uint first, IntPtr handler);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint RemoveVectoredExceptionHandler(IntPtr handle);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SystemInfoNative info);

        [StructLayout(LayoutKind.Sequential)]
        struct SystemInfoNative
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        // EXCEPTION_RECORD / EXCEPTION_POINTERS（x64：NumberParameters 后有 4 字节对齐填充）
        [StructLayout(LayoutKind.Sequential)]
        struct ExceptionRecord
        {
            public uint ExceptionCode;
            public uint ExceptionFlags;
            public IntPtr ExceptionRecordPtr;
            public IntPtr ExceptionAddress;
            public uint NumberParameters;
            public uint Padding;
            public IntPtr ExceptionInformation0; // 读/写类型
            public IntPtr ExceptionInformation1; // 故障地址
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ExceptionPointers
        {
            public IntPtr ExceptionRecord;
            public IntPtr ContextRecord;
        }

        static IntPtr _page = IntPtr.Zero;
        static UIntPtr _pageSize = UIntPtr.Zero;
        static IntPtr _veh = IntPtr.Zero;
        static VectoredHandlerDelegate _handlerKeepAlive;
        static int _tripped;
        static bool _active;

        public static bool IsActive => _active;

        public static bool Start(byte[] payload)
        {
            Stop();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                GetSystemInfo(out var si);
                uint pageBytes = si.dwPageSize != 0 ? si.dwPageSize : 4096u;
                _pageSize = new UIntPtr(pageBytes);
                _page = VirtualAlloc(IntPtr.Zero, _pageSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (_page == IntPtr.Zero) return false;

                var buf = new byte[pageBytes];
                if (payload != null && payload.Length > 0)
                {
                    int n = Math.Min(payload.Length, (int)pageBytes - 64);
                    Buffer.BlockCopy(payload, 0, buf, 0, n);
                }
                Marshal.Copy(buf, 0, _page, buf.Length);

                _handlerKeepAlive = VectoredHandler;
                IntPtr fn = Marshal.GetFunctionPointerForDelegate(_handlerKeepAlive);
                _veh = AddVectoredExceptionHandler(1, fn);
                if (_veh == IntPtr.Zero)
                {
                    Stop();
                    return false;
                }
                if (!ArmGuard())
                {
                    Stop();
                    return false;
                }
                System.Threading.Interlocked.Exchange(ref _tripped, 0);
                _active = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CardKey] MemoryPageProbe start failed: " + ex.Message);
                Stop();
                return false;
            }
#else
            return false;
#endif
        }

        public static void Stop()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            _active = false;
            if (_veh != IntPtr.Zero)
            {
                RemoveVectoredExceptionHandler(_veh);
                _veh = IntPtr.Zero;
            }
            if (_page != IntPtr.Zero)
            {
                VirtualFree(_page, UIntPtr.Zero, MEM_RELEASE);
                _page = IntPtr.Zero;
            }
            _handlerKeepAlive = null;
            System.Threading.Interlocked.Exchange(ref _tripped, 0);
#endif
        }

        public static bool ConsumeTripped()
        {
            return System.Threading.Interlocked.Exchange(ref _tripped, 0) != 0;
        }

        public static bool Rearm()
        {
            if (!_active) return false;
            return ArmGuard();
        }

        static bool ArmGuard()
        {
            if (_page == IntPtr.Zero) return false;
            return VirtualProtect(_page, _pageSize, PAGE_READONLY | PAGE_GUARD, out _);
        }

        static int VectoredHandler(IntPtr exceptionInfo)
        {
            if (!_active || _page == IntPtr.Zero || exceptionInfo == IntPtr.Zero)
                return 0; // EXCEPTION_CONTINUE_SEARCH

            var ep = Marshal.PtrToStructure<ExceptionPointers>(exceptionInfo);
            if (ep.ExceptionRecord == IntPtr.Zero) return 0;
            var rec = Marshal.PtrToStructure<ExceptionRecord>(ep.ExceptionRecord);
            if (rec.ExceptionCode != STATUS_GUARD_PAGE_VIOLATION) return 0;

            long fault = rec.ExceptionInformation1.ToInt64();
            long start = _page.ToInt64();
            long end = start + (long)_pageSize.ToUInt64();
            if (fault < start || fault >= end) return 0;

            System.Threading.Interlocked.Exchange(ref _tripped, 1);
            VirtualProtect(_page, _pageSize, PAGE_READONLY, out _);
            return -1; // EXCEPTION_CONTINUE_EXECUTION
        }
    }
}
