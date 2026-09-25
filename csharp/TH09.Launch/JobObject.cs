using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TH09.Launch;

internal sealed class JobObject
{
    private const uint KillOnJobClose = 0x00002000;
    private const int ExtendedLimitInformation = 9;

    private readonly SafeJobHandle handle;

    internal static JobObject Shared { get; } = new();

    private JobObject()
    {
        SafeJobHandle created = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (created.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            created.Dispose();
            throw new Win32Exception(error, "子を保つ Job を作れません");
        }

        var info = new JobExtendedLimitInformation
        {
            BasicLimitInformation = new JobBasicLimitInformation
            {
                LimitFlags = KillOnJobClose,
            },
        };
        if (!NativeMethods.SetInformationJobObject(
                created,
                ExtendedLimitInformation,
                ref info,
                (uint)Marshal.SizeOf<JobExtendedLimitInformation>()))
        {
            int error = Marshal.GetLastWin32Error();
            created.Dispose();
            throw new Win32Exception(error, "Job に kill-on-close を設定できません");
        }

        handle = created;
    }

    internal void Assign(Process process)
    {
        if (!NativeMethods.AssignProcessToJobObject(handle, process.Handle))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "子を Job へ入れられません");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimitInformation
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize;
        internal UIntPtr MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal UIntPtr Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobExtendedLimitInformation
    {
        internal JobBasicLimitInformation BasicLimitInformation;
        internal IoCounters IoInfo;
        internal UIntPtr ProcessMemoryLimit;
        internal UIntPtr JobMemoryLimit;
        internal UIntPtr PeakProcessMemoryUsed;
        internal UIntPtr PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeJobHandle() : base(ownsHandle: true) { }

        protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeJobHandle CreateJobObject(IntPtr jobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(
            SafeJobHandle job,
            int informationClass,
            ref JobExtendedLimitInformation information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr value);
    }
}
