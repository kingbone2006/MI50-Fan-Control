using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FanDiag
{
    public class InspectSpeedFanControls
    {
        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);
        private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public static void Run()
        {
            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0) return;

            var proc = procs[0];
            Console.WriteLine($"SpeedFan PID: {proc.Id}, MainWindowHandle=0x{proc.MainWindowHandle.ToInt64():X}");

            foreach (ProcessThread th in proc.Threads)
            {
                EnumThreadWindows(th.Id, (hWnd, lp) =>
                {
                    StringBuilder cls = new StringBuilder(256);
                    GetClassName(hWnd, cls, 256);
                    StringBuilder txt = new StringBuilder(256);
                    GetWindowText(hWnd, txt, 256);

                    Console.WriteLine($"Thread {th.Id} Window: 0x{hWnd.ToInt64():X}, Class='{cls}', Text='{txt}'");

                    EnumChildWindows(hWnd, (child, param) =>
                    {
                        StringBuilder cCls = new StringBuilder(256);
                        GetClassName(child, cCls, 256);
                        StringBuilder cText = new StringBuilder(256);
                        GetWindowText(child, cText, 256);
                        Console.WriteLine($"    Child: 0x{child.ToInt64():X}, Class='{cCls}', Text='{cText}'");
                        return true;
                    }, IntPtr.Zero);

                    return true;
                }, IntPtr.Zero);
            }
        }
    }
}
