using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FanDiag
{
    public class SpeedFanUiAutomationTest
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
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint VK_RETURN = 0x0D;
        private const uint UDM_SETPOS32 = 0x0471;
        private const uint UDM_GETPOS32 = 0x0472;

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" SPEEDFAN UI AUTOMATION TEST");
            Console.WriteLine("==================================================");

            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0)
            {
                Console.WriteLine("SpeedFan process not found!");
                return;
            }

            uint sfPid = (uint)procs[0].Id;
            Console.WriteLine($"SpeedFan PID: {sfPid}");

            EnumWindows((topHwnd, l) =>
            {
                GetWindowThreadProcessId(topHwnd, out uint pid);
                if (pid == sfPid)
                {
                    StringBuilder title = new StringBuilder(256);
                    GetWindowText(topHwnd, title, 256);
                    Console.WriteLine($"Top Window: 0x{topHwnd.ToInt64():X} | Title: '{title}'");

                    EnumChildWindows(topHwnd, (child, param) =>
                    {
                        StringBuilder cls = new StringBuilder(256);
                        GetClassName(child, cls, 256);
                        StringBuilder txt = new StringBuilder(256);
                        GetWindowText(child, txt, 256);

                        string c = cls.ToString();
                        string t = txt.ToString();

                        Console.WriteLine($"  Child: 0x{child.ToInt64():X} | Class: '{c}' | Text: '{t}'");

                        // If Edit or UpDown, try setting 70
                        if (c.Contains("Edit") && (t.Contains("20") || t.Contains("%") || int.TryParse(t, out _)))
                        {
                            Console.WriteLine($"    >>> Setting Edit text to 70 on 0x{child.ToInt64():X}");
                            SendMessage(child, WM_SETTEXT, IntPtr.Zero, "70");
                            SendMessage(child, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
                            SendMessage(child, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
                        }
                        if (c.Contains("UpDown") || c.Contains("updown"))
                        {
                            Console.WriteLine($"    >>> Setting UpDown pos to 70 on 0x{child.ToInt64():X}");
                            SendMessage(child, UDM_SETPOS32, IntPtr.Zero, (IntPtr)70);
                        }

                        return true;
                    }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }
    }
}
