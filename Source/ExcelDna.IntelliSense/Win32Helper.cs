﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using ExcelDna.Integration;

namespace ExcelDna.IntelliSense
{
    static class Win32Helper
    {
        enum WM : uint
        {
            GETTEXT = 0x000D,
            GETTEXTLENGTH = 0x000E,
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")]
        static extern int GetClassNameW(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buf, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, // x-coordinate of upper-left corner
            int nTopRect, // y-coordinate of upper-left corner
            int nRightRect, // x-coordinate of lower-right corner
            int nBottomRect, // y-coordinate of lower-right corner
            int nWidthEllipse, // height of ellipse
            int nHeightEllipse); // width of ellipse

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject([In] IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, WM Msg, IntPtr wParam, [Out] StringBuilder lParam);

        // TODO: Not for 64-bit ...?
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SendMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public Rectangle rcCaret;
        }

        // Different to Rectangle ...?
        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        enum GetAncestorFlags
        {
            // Retrieves the parent window. This does not include the owner, as it does with the GetParent function. 
            GetParent = 1,
            // Retrieves the root window by walking the chain of parent windows.
            GetRoot = 2,
            // Retrieves the owned root window by walking the chain of parent and owner windows returned by GetParent. 
            GetRootOwner = 3
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        [DllImport("user32.dll", SetLastError=true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr SetCapture(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();


        // Returns the WindowHandle of the focused window, if that window is in our process.
        public static IntPtr GetFocusedWindowHandle()
        {
            var info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(info);
            if (!GetGUIThreadInfo(0, ref info))
                throw new Win32Exception();

            var focusedWindow = info.hwndFocus;
            if (focusedWindow == IntPtr.Zero)
                return focusedWindow;

            uint processId;
            uint threadId = GetWindowThreadProcessId(focusedWindow, out processId);
            if (threadId == 0)
                throw new Win32Exception();

            uint currentProcessId = GetCurrentProcessId();
            if (processId == currentProcessId)
                return focusedWindow;

            return IntPtr.Zero;
        }

        // Should return null if there is no such ancestor
        public static IntPtr GetRootAncestor(IntPtr hWnd)
        {
            return GetAncestor(hWnd, GetAncestorFlags.GetRoot);
        }

        public static System.Drawing.Point GetClientCursorPos(IntPtr hWnd)
        {
            Point pt;
            bool ok = GetCursorPos(out pt);
            bool ok2 = ScreenToClient(hWnd, ref pt);
            return pt;
        }

        // We use System.Windows.Rect to be consistent with the UIAutomation we use elsewhere.
        // Returns Rect.Empty if the Win32 call fails (Windows is not visible?)
        public static System.Windows.Rect GetWindowBounds(IntPtr hWnd)
        {
            RECT rect; // Not sure if the struct layout is like Win32 RECT or System.Drawing.Rectangle (these are different)
            if (GetWindowRect(hWnd, out rect))
            {
                return new System.Windows.Rect(rect.Left, rect.Right, rect.Right - rect.Left + 1, rect.Bottom - rect.Top + 1);
            }
            else
            {
                return System.Windows.Rect.Empty;
            }
        }

        public static string GetWindowTextRaw(IntPtr hwnd)
        {
            // Allocate correct string length first
            int length = (int)SendMessage(hwnd, WM.GETTEXTLENGTH, IntPtr.Zero, null);
            StringBuilder sb = new StringBuilder(length + 1);
            SendMessage(hwnd, WM.GETTEXT, (IntPtr)sb.Capacity, sb);
            return sb.ToString();
        }

        const int SW_HIDE = 0;
        
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public static bool HideWindow(IntPtr hWnd)
        {
            // Happy to suppress any errors here
            try
            {
                return ShowWindow(hWnd, SW_HIDE);
            }
            catch (Exception ex)
            {
                Debug.Print($"Win32Helper.HideWindow Error: {ex.ToString()}");
                return false;
            }
        }

        public static string GetXllName()
        {
            return ExcelDnaUtil.XllPath;
        }

        public static IntPtr GetXllModuleHandle()
        {
            return GetModuleHandle(GetXllName());
        }

        public static uint GetExcelProcessId()
        {
            return GetCurrentProcessId();
        }

        static StringBuilder _buffer = new StringBuilder(65000);
        public static string GetClassName(IntPtr hWnd)
        {
            _buffer.Length = 0;
            GetClassNameW(hWnd, _buffer, _buffer.Capacity);
            return _buffer.ToString();
        }

        public static string GetText(IntPtr hWnd)
        {
            // Allocate correct string length first
            int length = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static int GetPosFromChar(IntPtr hWnd, int ch)
        {
            return SendMessage(hWnd, 214 /*EM_POSFROMCHAR*/, ch, 0);
        }
    }
}
