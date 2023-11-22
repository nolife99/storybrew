using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BrewLib.Util
{
    public static partial class Native
    {
        #region Memory

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport("kernel32")]
        private static partial void RtlCopyMemory(nint dest, nint src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [LibraryImport("kernel32")]
        private static partial void RtlMoveMemory(nint dest, nint src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CopyMemory(nint source, nint destination, int count)
        {
            if (Environment.Is64BitProcess)
            {
                if (source != default && destination != default)
                {
                    if (source < destination + count && source + count > destination)
                    {
                        RtlMoveMemory(destination, source, (uint)count);
                        return false;
                    }
                    RtlCopyMemory(destination, source, (uint)count);
                    return true;
                }
                return false;
            }
            unsafe
            {
                Buffer.MemoryCopy(destination.ToPointer(), destination.ToPointer(), count, count);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static nint AddrOfPinnedArray(this Array arr) => (nint)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(arr));

        #endregion

        #region Win32

        [MethodImpl(MethodImplOptions.AggressiveInlining)] [LibraryImport("user32")]
        private static partial nint SendMessageA(nint hWnd, uint msg, nuint wParam, nint lParam);

        ///<summary> Sends the specified message to a window or windows. </summary>
        ///<remarks> Help: <see href="https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessage"/></remarks>
        ///<typeparam name="T1"> The type of the first parameter, which is platform-dependent. </typeparam>
        ///<typeparam name="T2"> The type of the second parameter, which is platform-dependent. </typeparam>
        ///<typeparam name="TResult"> The return type, which is platform-dependent. </typeparam>
        ///<param name="windowHandle"> The window to send the message to. </param>
        ///<param name="message"> The type of message to send. </param>
        ///<param name="param1"> The first value to wrap within the message. </param>
        ///<param name="param2"> The second value to wrap within the message. </param>
        ///<returns> The result from the call procedure. </returns>
        ///<exception cref="NotSupportedException"> Type can't be casted to or from <see cref="nint"/>. </exception>
        ///<exception cref="OverflowException"> Type can't be represented as <see cref="nint"/>. </exception>
        public static TResult SendMessage<T1, T2, TResult>(nint windowHandle, Message message, T1 param1, T2 param2) 
            where T1 : INumberBase<T1> where T2 : INumberBase<T2> where TResult : INumberBase<TResult>
            => TResult.CreateChecked(SendMessageA(windowHandle, (uint)message, nuint.CreateChecked(param1), nint.CreateChecked(param2)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetWindowIcon(nint iconHandle)
        {
            SendMessage<int, nint, int>(MainWindowHandle, Message.SetIcon, 0, iconHandle);
            SendMessage<int, nint, int>(MainWindowHandle, Message.SetIcon, 1, iconHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetWindowText(nint hWnd)
        {
            var length = SendMessage<int, int, int>(hWnd, Message.GetTextLength, 0, 0);
            if (length == 0) return "";

            var buffer = Marshal.AllocCoTaskMem(length * 2 + 1);
            SendMessage<int, nint, int>(hWnd, Message.GetText, length + 1, buffer);

            var result = Marshal.PtrToStringAnsi(buffer);
            Marshal.FreeCoTaskMem(buffer);
            return result;
        }

        delegate bool EnumThreadWndProc(nint hWnd, nint lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnumThreadWindows(int dwThreadId, EnumThreadWndProc lpfn, nint lParam);

        static nint hWnd;
        public static nint MainWindowHandle 
        { 
            get
            {
                if (hWnd == nint.Zero) throw new InvalidOperationException("hWnd was not initialized");
                return hWnd;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeHandle(string windowTitle, nint hWndFallback = default)
        {
            var handle = nint.Zero;
            var cont = true;
            var threads = Process.GetCurrentProcess().Threads;

            for (var i = 0; i < threads.Count && cont; ++i) EnumThreadWindows(threads[i].Id, (hWnd, lParam) =>
            {
                if (GetWindowText(hWnd) == windowTitle)
                {
                    handle = hWnd;
                    cont = false;
                }
                return cont;
            }, default);

            hWnd = handle != nint.Zero ? handle : hWndFallback;
        }

        #endregion

        ///<summary><see href="https://learn.microsoft.com/en-us/windows/win32/winmsg/window-reference"/></summary>
        public enum Message : uint
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
            ///<returns>If successful, the menu handle for the current window; otherwise <see cref="nint.Zero"/>.</returns>
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
            ///<returns>Nonzero if it erases the background; otherwise 0.</returns>
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
            ///<returns>A handle to the font used by the control, or <see cref="nint.Zero"/> if the control is using the system font.</returns>
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
            ///<returns>The number of characters copied, not including the terminating null character.</returns>
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
            ///<returns>The length of the text in characters, not including the terminating null character.</returns>
            GetTextLength = 0x000E,

            ///<summary>Sets the font that a control is to use when drawing text.</summary>
            ///<remarks> 
            /// <list type="bullet">
            ///  <item>
            ///   <term>wParam</term>
            ///   <description>A handle to the font. If <see langword="null"/>, the control uses the default system font to draw text.</description>
            ///  </item>
            ///  <item>
            ///   <term>lParam</term>
            ///   <description>
            ///    The low-order word of <c>lParam</c> specifies whether the control should be redrawn immediately upon setting the font. 
            ///    If <see langword="true"/>, the control redraws itself.
            ///   </description>
            ///  </item>
            /// </list> 
            ///</remarks>
            ///<returns>Nothing.</returns>
            SetFont = 0x0030,

            ///<summary>
            /// Associates a new large or small icon with a window. 
            /// The system displays the large icon in the <c>ALT+TAB</c> dialog box, and the small icon in the window caption.
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
            ///   <description>A handle to the new large or small icon. If <see langword="null"/>, the icon indicated by <c>wParam</c> is removed.</description>
            ///  </item>
            /// </list> 
            ///</remarks>
            ///<returns>
            /// A handle to the previous large or small icon, depending on the value of <c>wParam</c>. 
            /// It is <see cref="nint.Zero"/> if the window previously had no icon of the type indicated by <c>wParam</c>.
            ///</returns>
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
            ///<returns> 1 if the text is set. </returns>
            SetText = 0x000C,

            /* Notifications
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
            WindowPosChanging = 0x0046 */
        }
    }
}