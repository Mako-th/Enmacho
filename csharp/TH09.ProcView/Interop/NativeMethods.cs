using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TH09.ProcView.Interop;

[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
    public const uint ProcessVmRead = 0x0010;

    public const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern nint OpenProcess(uint desiredAccess, int inheritHandle, uint processId);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    public static extern int ReadProcessMemory(nint process, nint address,
                                               ref byte buffer, nuint size, out nuint read);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    public static extern int CloseHandle(nint handle);


    public static readonly nint InvalidHandle = -1;

    public const uint Th32csSnapProcess = 0x00000002;

    public const uint Th32csSnapModule = 0x00000008;

    public const uint Th32csSnapModule32 = 0x00000010;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    public static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    public static extern int Process32FirstW(nint snapshot, ref ProcessEntry32W entry);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    public static extern int Process32NextW(nint snapshot, ref ProcessEntry32W entry);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    public static extern int Module32FirstW(nint snapshot, ref ModuleEntry32W entry);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    public static extern int Module32NextW(nint snapshot, ref ModuleEntry32W entry);

    [InlineArray(260)]
    internal struct Wchar260
    {
        private ushort _element0;
    }

    [InlineArray(256)]
    internal struct Wchar256
    {
        private ushort _element0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessEntry32W
    {
        public uint Size;
        public uint CntUsage;
        public uint ProcessId;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint CntThreads;
        public uint ParentProcessId;
        public int PriClassBase;
        public uint Flags;
        public Wchar260 ExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ModuleEntry32W
    {
        public uint Size;
        public uint ModuleId;
        public uint ProcessId;
        public uint GlblCntUsage;
        public uint ProcCntUsage;
        public nint ModBaseAddr;
        public uint ModBaseSize;
        public nint HModule;
        public Wchar256 Module;
        public Wchar260 ExePath;
    }
}
