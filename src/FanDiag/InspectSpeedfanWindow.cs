using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FanDiag
{
    public class InspectSpeedfanWindow
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint VK_RETURN = 0x0D;

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" INSPECT SPEEDFAN WINDOW & CONTROLS");
            Console.WriteLine("==================================================");

            IntPtr hWnd = FindWindow(null, "SpeedFan 4.52");
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("SpeedFan 4.52 window not found by title, trying class...");
            }
            else
            {
                Console.WriteLine($"[FOUND] SpeedFan window handle: 0x{hWnd.ToInt64():X}");
            }

            int count = 0;
            EnumChildWindows(hWnd, (child, param) =>
            {
                count++;
                StringBuilder text = new StringBuilder(256);
                GetWindowText(child, text, 256);

                StringBuilder cls = new StringBuilder(256);
                GetClassName(child, cls, 256);

                string t = text.ToString();
                string c = cls.ToString();

                if (t.Contains("20") || t.Contains("Pwm") || c.Contains("Edit") || c.Contains("UpDown") || c.Contains("Track"))
                {
                    Console.WriteLine($"  Control #{count}: Hwnd=0x{child.ToInt64():X} | Class='{c}' | Text='{t}'");
                }
                return true;
            }, IntPtr.Zero);
        }
    }
}
