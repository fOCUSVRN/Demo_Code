using DigitalStation.Processes;
using DigitalStation.Rk7;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalStation.Services
{

    static class ProcHelper
    {
        private const string MidDesktopArg = "/desktop";



        [StructLayout(LayoutKind.Sequential)]
        internal sealed class SERVICE_STATUS_PROCESS
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint dwServiceType;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwCurrentState;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwControlsAccepted;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwWin32ExitCode;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwServiceSpecificExitCode;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwCheckPoint;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwWaitHint;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwProcessId;
            [MarshalAs(UnmanagedType.U4)]
            public uint dwServiceFlags;
        }

        internal const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
        internal const int SC_STATUS_PROCESS_INFO = 0;

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool QueryServiceStatusEx(SafeHandle hService, int infoLevel, IntPtr lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);





        public static string GetCommandLine(this Process process)
        {
            using var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
            using var objects = searcher.Get();
            return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
        }

        private static void RestartDesktop(Process process)
        {
            var oldFileName = process.MainModule.FileName;
            Log.Logger.Debug($"oldFileName: {oldFileName}");

            process.Kill();

            Log.Logger.Debug($"Kill ok");

            var newProc = new Process
            {
                StartInfo =
                {
                    FileName = oldFileName,
                    WorkingDirectory = Path.GetDirectoryName(oldFileName),
                    Arguments = MidDesktopArg,
                    UseShellExecute = true,
                }
            };

            newProc.Start();
        }

        public static int GetServiceProcessId(this ServiceController sc)
        {
            if (sc == null)
                throw new ArgumentNullException("sc");

            IntPtr zero = IntPtr.Zero;

            try
            {
                UInt32 dwBytesNeeded;
                // Call once to figure the size of the output buffer.
                QueryServiceStatusEx(sc.ServiceHandle, SC_STATUS_PROCESS_INFO, zero, 0, out dwBytesNeeded);
                if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
                {
                    // Allocate required buffer and call again.
                    zero = Marshal.AllocHGlobal((int)dwBytesNeeded);

                    if (QueryServiceStatusEx(sc.ServiceHandle, SC_STATUS_PROCESS_INFO, zero, dwBytesNeeded, out dwBytesNeeded))
                    {
                        var ssp = new SERVICE_STATUS_PROCESS();
                        Marshal.PtrToStructure(zero, ssp);
                        return (int)ssp.dwProcessId;
                    }
                }
            }
            finally
            {
                if (zero != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(zero);
                }
            }
            return -1;
        }

        public static int GetServiceProcessId2(this ServiceController sc)
        {
            ManagementObject service = new ManagementObject(@"Win32_service.Name='" + sc.ServiceName + "'");
            object o = service.GetPropertyValue("ProcessId");
            int processId = (int)((UInt32)o);

            return processId;
        }

        private static void RestartService(Process process)
        {
            var allServices = ServiceController.GetServices();

            Log.Logger.Debug("Get all svc");

            var rkServices = allServices.Where(x => x.DisplayName.StartsWith("RKeeper")).ToList();

            var currentSvc = rkServices.FirstOrDefault(x => x.GetServiceProcessId2() == process.Id);

            Log.Logger.Debug("Found one svc");

            if (currentSvc == null)
                throw new Exception("Could not found service");

            currentSvc.Stop();

            Thread.Sleep(3000);

            currentSvc.Start();
        }

        public static ExitCodes RestartProcess(int pid)
        {
            var process = Process.GetProcessById(pid);

            Log.Logger.Debug("Get process ok");

            var cmdLine = process.GetCommandLine()??string.Empty;

            Log.Logger.Debug($"CmdLine: {cmdLine}");

            if (cmdLine.EndsWith(MidDesktopArg, StringComparison.CurrentCultureIgnoreCase))
            {
                Log.Logger.Debug($"begin restart desktop: {cmdLine}");
                RestartDesktop(process);

                return ExitCodes.Ok;
            }

            // пусто
            Log.Logger.Debug($"begin restart svc: {cmdLine}");
            RestartService(process);

            return ExitCodes.Ok;
        }
    }



    public class MidserverRestarter
    {
        private readonly Rk7Api _rk7;

        public MidserverRestarter(Rk7Api rk7)
        {
            _rk7 = rk7;
        }

        private ExitCodes ElevateProcess(int pid)
        {
            //restart process
            var args = $"{Program.RestartArg} {pid}";

            var elevated = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, args)
            {
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var newProcess = Process.Start(elevated);

            Log.Logger.Debug($"process started");

            newProcess.WaitForExit();

            Log.Logger.Debug($"process exitCode: {newProcess.ExitCode}");

            return (ExitCodes)newProcess.ExitCode;
        }



        public async Task<ExitCodes> RestartMid()
        {
            var sysInfo = await _rk7.GetSystemInfo2();

            Log.Logger.Debug($"RestartMid: getsystemInfo ok");

            var process = Process.GetProcessById(sysInfo.ProcessID);

            Log.Logger.Debug($"RestartMid: GetProcessById ok");

            var processName = process.ProcessName;

            Log.Logger.Debug($"procName: {processName}");
            //_log.LogDebug($"module: {process.MainModule.ModuleName}");
            //_log.LogDebug($"arg: {process.MainModule.ModuleName}");
            //_log.LogDebug($"fileName: {process.MainModule.FileName}");
            //_log.LogDebug($"cmdLine: {process.GetCommandLine()}");

            if (UacHelper.IsAdmin())
            {
                Log.Logger.Debug($"RestartMid: IsAdmin");
                return ProcHelper.RestartProcess(sysInfo.ProcessID);
            }

            Log.Logger.Debug($"RestartMid: no admin");

            return ElevateProcess(sysInfo.ProcessID);
        }
    }
}
