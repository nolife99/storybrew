namespace BrewLib.Util;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using Image = OpenTK.Windowing.GraphicsLibraryFramework.Image;

public static unsafe partial class Native
{
    /// <summary>
    ///     <see href="https://learn.microsoft.com/en-us/windows/win32/winmsg/window-reference"/>
    /// </summary>
    public enum Message : uint
    {
        // Messages

        /// <summary>Retrieves the menu handle for the current window.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>This parameter is not used.</description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>This parameter is not used.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>If successful, the menu handle for the current window; otherwise <see cref="nint.Zero"/>.</returns>
        GetHMenu = 0x01E1,

        /// <summary>
        ///     Sent when the window background must be erased (for example, when a window is resized).
        ///     The message is sent to prepare an invalidated portion of a window for painting.
        /// </summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>A handle to the device context.</description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>This parameter is not used.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>Nonzero if it erases the background; otherwise 0.</returns>
        EraseBG = 0x0014,

        /// <summary>Retrieves the font with which the control is currently drawing its text.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>This parameter is not used and must be zero.</description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>This parameter is not used and must be zero.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>A handle to the font used by the control, or <see cref="nint.Zero"/> if the control is using the system font.</returns>
        GetFont = 0x0031,

        /// <summary>Copies the text that corresponds to a window into a buffer provided by the caller.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>
        ///                 The maximum number of characters to be copied, including the terminating null character.
        ///                 <para/>
        ///                 ANSI applications may have the string in the buffer reduced in size (to a minimum of half that of the
        ///                 wParam value) due to conversion from ANSI to Unicode.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>A pointer to the buffer that is to receive the text.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>The number of characters copied, not including the terminating null character.</returns>
        GetText = 0x000D,

        /// <summary>Determines the length, in characters, of the text associated with a window.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>This parameter is not used and must be zero.</description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>This parameter is not used and must be zero.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>The length of the text in characters, not including the terminating null character.</returns>
        GetTextLength = 0x000E,

        /// <summary>Sets the font that a control is to use when drawing text.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>
        ///                 A handle to the font. If <see langword="null"/>, the control uses the default system font to
        ///                 draw text.
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>
        ///                 The low-order word of <c>lParam</c> specifies whether the control should be redrawn immediately upon
        ///                 setting the font.
        ///                 If <see langword="true"/>, the control redraws itself.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>Nothing.</returns>
        SetFont = 0x0030,

        /// <summary>
        ///     Associates a new large or small icon with a window.
        ///     The system displays the large icon in the <c>ALT+TAB</c> dialog box, and the small icon in the window caption.
        /// </summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>
        ///                 The type of icon to be set. This parameter can be one of the following values:
        ///                 <list type="table">
        ///                     <item>
        ///                         <term>1</term>
        ///                         <description>Set the large icon for the window.</description>
        ///                     </item>
        ///                     <item>
        ///                         <term>0</term>
        ///                         <description>Set the small icon for the window.</description>
        ///                     </item>
        ///                 </list>
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>
        ///                 A handle to the new large or small icon. If <see langword="null"/>, the icon indicated by
        ///                 <c>wParam</c> is removed.
        ///             </description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns>
        ///     A handle to the previous large or small icon, depending on the value of <c>wParam</c>.
        ///     It is <see cref="nint.Zero"/> if the window previously had no icon of the type indicated by <c>wParam</c>.
        /// </returns>
        SetIcon = 0x0080,

        /// <summary>Sets the text of a window.</summary>
        /// <remarks>
        ///     <list type="bullet">
        ///         <item>
        ///             <term>wParam</term>
        ///             <description>This parameter is not used.</description>
        ///         </item>
        ///         <item>
        ///             <term>lParam</term>
        ///             <description>A pointer to a null-terminated string that is the window text.</description>
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <returns> 1 if the text is set. </returns>
        SetText = 0x000C
    }

    public static nint AllocateMemory(int cb) => (nint)NativeMemory.Alloc((nuint)cb);
    public static void FreeMemory(nint addr) => NativeMemory.Free((void*)addr);

    sealed class UnmanagedMemoryAllocator : MemoryAllocator
    {
        protected override int GetBufferCapacityInBytes() => int.MaxValue;
        public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
            => new UnmanagedBuffer<T>(length, options);
    }

    #region Win32

    [LibraryImport("user32")] private static partial nint SendMessageW(nint hWnd, uint msg, nuint wParam, nint lParam);

    static nint handle;
    public static Window* GLFWPtr { get; private set; }

    public static nint MainWindowHandle => handle != 0 ? handle : throw new InvalidOperationException("hWnd isn't initialized");

    public static void InitializeHandle(NativeWindow glfwWindow)
    {
        Configuration.Default.MemoryAllocator = new UnmanagedMemoryAllocator();

        GLFWPtr = glfwWindow.WindowPtr;
        handle = GLFW.GetWin32Window(GLFWPtr);
    }

    public static void SetWindowIcon(Type type, string iconPath)
    {
        IconBitmapDecoder decoder;
        using (var iconResource = type.Assembly.GetManifestResourceStream(type, iconPath))
            decoder = new(iconResource, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];
        var bytesLength = frame.PixelWidth * frame.PixelHeight * 4;
        var bytes = stackalloc byte[bytesLength];

        frame.CopyPixels(default, (nint)bytes, bytesLength, frame.PixelWidth * 4);
        Image icon = new(frame.PixelWidth, frame.PixelHeight, bytes);
        GLFW.SetWindowIconRaw(GLFWPtr, 1, &icon);
    }

    #endregion
}

public sealed unsafe class UnmanagedBuffer<T>(int length, AllocationOptions options = AllocationOptions.None)
    : MemoryManager<T> where T : struct
{
    readonly void* ptr = options is AllocationOptions.None ?
        NativeMemory.Alloc((nuint)length, (nuint)Marshal.SizeOf<T>()) :
        NativeMemory.AllocZeroed((nuint)length, (nuint)Marshal.SizeOf<T>());

    public nint Address => (nint)ptr;
    public ref T this[int index] => ref Unsafe.AsRef<T>(Unsafe.Add<T>(ptr, index));

    protected override void Dispose(bool disposing) => NativeMemory.Free(ptr);
    public override Span<T> GetSpan() => new(ptr, length);
    public override MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>(ptr, elementIndex));
    public override void Unpin() { }
}