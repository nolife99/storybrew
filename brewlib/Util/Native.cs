using osuTK;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BrewLib.Util
{
    public static partial class Native
    {
        #region Memory

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport("kernel32.dll", EntryPoint = "RtlCopyMemory")] 
        private static partial void memcpy(nint dest, nint src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static partial void memmove(nint dest, nint src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CopyMemory(nint source, nint destination, uint count)
        {
            if (source != default && destination != default)
            {
                if (source < destination + count && source + count > destination)
                {
                    memmove(destination, source, count);
                    return false;
                }
                memcpy(destination, source, count);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static nint AddrOfPinnedArray(this Array arr) => (nint)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(arr));

        #endregion

        #region Win32

        /// <summary>
        /// Sends the specified message to a window or windows. <para/> 
        /// The <see cref="SendMessage"/> function calls the window procedure for the specified window and does not return until the window procedure has processed the message.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose window procedure will receive the message.</param>
        /// <param name="msg">The message to be sent.</param>
        /// <param name="wParam">Additional message-specific information.</param>
        /// <param name="lParam">Additional message-specific information.</param>
        /// <returns>The return value specifies the result of the message processing; it depends on the message sent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport("user32.dll", EntryPoint = "SendMessageA")]
        public static partial nint SendMessage(nint hWnd, Win32Message msg, int wParam, nint lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetWindowIcon(nint iconHandle)
        {
            SendMessage(MainWindowHandle, Win32Message.SetIcon, 0, iconHandle);
            SendMessage(MainWindowHandle, Win32Message.SetIcon, 1, iconHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetWindowText(nint hWnd)
        {
            var length = SendMessage(hWnd, Win32Message.GetTextLength, 0, 0).ToInt32();
            if (length == 0) return "";

            var buffer = Marshal.AllocCoTaskMem(length * 2 + 1);
            SendMessage(hWnd, Win32Message.GetText, length + 1, buffer);

            var result = Marshal.PtrToStringAnsi(buffer);
            Marshal.FreeCoTaskMem(buffer);
            return result;
        }

        delegate bool EnumThreadWndProc(nint hWnd, nint lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnumThreadWindows(int dwThreadId, EnumThreadWndProc lpfn, nint lParam);

        static nint hWnd;
        public static nint MainWindowHandle 
        { 
            get
            {
                if (hWnd == nint.Zero) throw new InvalidOperationException("hWnd was not initialized by UpdateHWND");
                return hWnd;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeHandle(this GameWindow window)
        {
            var handle = nint.Zero;
            var cont = true;
            var threads = Process.GetCurrentProcess().Threads;

            for (var i = 0; i < threads.Count && cont; ++i) EnumThreadWindows(threads[i].Id, (hWnd, lParam) =>
            {
                if (GetWindowText(hWnd) == window.Title)
                {
                    handle = hWnd;
                    cont = false;
                }
                return cont;
            }, default);

            hWnd = handle != nint.Zero ? handle : window.WindowInfo.Handle;
        }

        #endregion

        ///<summary><see href="https://learn.microsoft.com/en-us/windows/win32/winmsg/window-reference"/></summary>
        public enum Win32Message : uint
        {
            // Messages

            ///<summary>Retrieves the menu handle for the current window.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>This parameter is not used.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>This parameter is not used.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            GetHMenu = 0x01E1,

            ///<summary> 
            /// Sent when the window background must be erased (for example, when a window is resized). 
            /// The message is sent to prepare an invalidated portion of a window for painting. 
            ///</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>A handle to the device context.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>This parameter is not used.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            EraseBG = 0x0014,

            ///<summary>Retrieves the font with which the control is currently drawing its text.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>This parameter is not used and must be zero.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>This parameter is not used and must be zero.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            GetFont = 0x0031,

            ///<summary>Copies the text that corresponds to a window into a buffer provided by the caller.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>
            ///    The maximum number of characters to be copied, including the terminating null character. <para/>
            ///    ANSI applications may have the string in the buffer reduced in size (to a minimum of half that of the wParam value) due to conversion from ANSI to Unicode.
            ///   </description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>A pointer to the buffer that is to receive the text.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            GetText = 0x000D,

            ///<summary>Determines the length, in characters, of the text associated with a window.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>This parameter is not used and must be zero.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>This parameter is not used and must be zero.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            GetTextLength = 0x000E,

            ///<summary>Sets the font that a control is to use when drawing text.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>A handle to the font. If this parameter is <see langword="null"/>, the control uses the default system font to draw text.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>
            ///    The low-order word of <b>lParam</b> specifies whether the control should be redrawn immediately upon setting the font. 
            ///    If this parameter is <see langword="true"/>, the control redraws itself.
            ///   </description>
            ///  </item>
            /// </list> 
            ///</remarks>
            SetFont = 0x0030,

            ///<summary>
            /// Associates a new large or small icon with a window. 
            /// The system displays the large icon in the ALT+TAB dialog box, and the small icon in the window caption.
            ///</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>
            ///    The type of icon to be set. This parameter can be one of the following values:
            ///    <list type="table">
            ///     <item>
            ///      <term>1</term>
            ///      <description>Set the large icon for the window.</description>
            ///     </item>
            ///     <item>
            ///      <term>0</term>
            ///      <description>Set the small icon for the window.</description>
            ///     </item>
            ///    </list>
            ///   </description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>A handle to the new large or small icon. If this parameter is <see langword="null"/>, the icon indicated by <b>wParam</b> is removed.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            SetIcon = 0x0080,

            ///<summary>Sets the text of a window.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>This parameter is not used.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>A pointer to a null-terminated string that is the window text.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            SetText = 0x000C,

            // Notifications
            ActivateApp = 0x001C,
            CancelMode = 0x001F,
            ChildActivate = 0x0022,
            Close = 0x0010,
            Compacting = 0x0041,
            Create = 0x0001,
            Destroy = 0x0002,
            Enable = 0x000A,
            EnterSizeMove = 0x0231,
            ExitSizeMove = 0x0232,
            GetIcon = 0x007F,
            GetMinMaxInfo = 0x0024,
            InputLangChange = 0x0051,
            InputLangChangeRequest = 0x0050,
            Move = 0x0003,
            Moving = 0x0216,
            NCActivate = 0x0086,
            NCCalcSize = 0x0083,
            NCCreate = 0x0081,
            NCDestroy = 0x0082,
            Null = 0x0000,
            QueryDragIcon = 0x0037,
            QueryOpen = 0x0013,
            Quit = 0x0012,
            ShowWindow = 0x0018,
            Size = 0x0005,
            Sizing = 0x0214,
            StyleChanged = 0x007D,
            StyleChanging = 0x007C,
            ThemeChanged = 0x031A,

            [Obsolete("This message is not supported as of Windows Vista.")]
            UserChanged = 0x0054,

            WindowPosChanged = 0x0047,
            WindowPosChanging = 0x0046
        }
    }
}