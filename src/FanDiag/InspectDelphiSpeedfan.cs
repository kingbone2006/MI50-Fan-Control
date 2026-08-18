using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FanDiag
{
    public class InspectDelphiSpeedfan
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_COMMAND = 0x0111;
        private const uint EN_CHANGE = 0x0300;

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" INSPECT SPEEDFAN DELPHI CONTROLS");
            Console.WriteLine("==================================================");

            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0)
            {
                Console.WriteLine("SpeedFan not running");
                return;
            }

            uint sfPid = (uint)procs[0].Id;
            Console.WriteLine($"SpeedFan PID: {sfPid}");

            EnumWindows((hWnd, l) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == sfPid)
                {
                    StringBuilder title = new StringBuilder(256);
                    GetWindowText(hWnd, title, 256);

                    StringBuilder cls = new StringBuilder(256);
                    GetClassName(hWnd, cls, 256);

                    Console.WriteLine($"Window: 0x{hWnd.ToInt64():X} | Class: '{cls}' | Title: '{title}'");

                    int idx = 0;
                    EnumChildWindows(hWnd, (child, param) =>
                    {
                        idx++;
                        StringBuilder cText = new StringBuilder(256);
                        GetWindowText(child, cText, 256);

                        StringBuilder cCls = new StringBuilder(256);
                        GetClassName(child, cCls, 256);

                        Console.WriteLine($"  [{idx,2}] Child 0x{child.ToInt64():X} | Class: '{cCls,-16}' | Text: '{cText}'");

                        // If TEdit and contains "20", set to 70 and notify
                        if (cCls.ToString() == "TEdit" && cText.ToString() == "20")
                        {
                            Console.WriteLine($"    >>> Changing TEdit 0x{child.ToInt64():X} to 70%");
                            SendMessage(child, WM_SETTEXT, IntPtr.Zero, "70");
                            // Send EN_CHANGE notification to parent
                            IntPtr wParam = (IntPtr)((EN_CHANGE << 16) | (child.ToInt32() & 0xFFFF));
                            SendMessage(hWnd, WM_COMMAND, wParam, child);
                        }

                        return true;
                    }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }
    }
}
