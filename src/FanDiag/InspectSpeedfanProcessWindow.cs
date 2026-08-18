using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FanDiag
{
    public class InspectSpeedfanProcessWindow
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SETTEXT = 0x000C;
        private const uint UDM_SETPOS32 = 0x0471; // Sets position of an UpDown32 control

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" INSPECT SPEEDFAN PROCESS MAIN WINDOW");
            Console.WriteLine("==================================================");

            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0)
            {
                Console.WriteLine("Process 'speedfan' not found");
                return;
            }

            var proc = procs[0];
            IntPtr mainHwnd = proc.MainWindowHandle;
            Console.WriteLine($"SpeedFan PID: {proc.Id}, MainWindowHandle: 0x{mainHwnd.ToInt64():X}, Title: '{proc.MainWindowTitle}'");

            int idx = 0;
            EnumChildWindows(mainHwnd, (child, param) =>
            {
                idx++;
                StringBuilder text = new StringBuilder(256);
                GetWindowText(child, text, 256);

                StringBuilder cls = new StringBuilder(256);
                GetClassName(child, cls, 256);

                string t = text.ToString();
                string c = cls.ToString();

                Console.WriteLine($"  [{idx,2}] Hwnd: 0x{child.ToInt64():X} | Class: {c,-20} | Text: '{t}'");
                return true;
            }, IntPtr.Zero);
        }
    }
}
