using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MapSorter.Input;

public sealed class MouseController
{
    public void CtrlClick(Point position)
    {
        NativeMethods.SetCursorPos(position.X, position.Y);
        SendKeyDown(Keys.LControlKey);
        SendMouseClick(NativeMethods.MouseEventFlags.LeftDown, NativeMethods.MouseEventFlags.LeftUp);
        SendKeyUp(Keys.LControlKey);
    }

    public void RightClick(Point position)
    {
        NativeMethods.SetCursorPos(position.X, position.Y);
        SendMouseClick(NativeMethods.MouseEventFlags.RightDown, NativeMethods.MouseEventFlags.RightUp);
    }

    public void CtrlRightClick(Point position)
    {
        NativeMethods.SetCursorPos(position.X, position.Y);
        SendKeyDown(Keys.LControlKey);
        SendMouseClick(NativeMethods.MouseEventFlags.RightDown, NativeMethods.MouseEventFlags.RightUp);
        SendKeyUp(Keys.LControlKey);
    }

    private static void SendMouseClick(NativeMethods.MouseEventFlags down, NativeMethods.MouseEventFlags up)
    {
        var inputs = new[]
        {
            NativeMethods.CreateMouseInput(down),
            NativeMethods.CreateMouseInput(up)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendKeyDown(Keys key)
    {
        var inputs = new[]
        {
            NativeMethods.CreateKeyboardInput((ushort)key, NativeMethods.KeyEventFlags.KeyDown)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendKeyUp(Keys key)
    {
        var inputs = new[]
        {
            NativeMethods.CreateKeyboardInput((ushort)key, NativeMethods.KeyEventFlags.KeyUp)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public InputType type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public MouseEventFlags dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public KeyEventFlags dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    public enum InputType : uint
    {
        Mouse = 0,
        Keyboard = 1
    }

    [Flags]
    public enum MouseEventFlags : uint
    {
        Move = 0x0001,
        LeftDown = 0x0002,
        LeftUp = 0x0004,
        RightDown = 0x0008,
        RightUp = 0x0010
    }

    [Flags]
    public enum KeyEventFlags : uint
    {
        KeyDown = 0x0000,
        ExtendedKey = 0x0001,
        KeyUp = 0x0002
    }

    public static INPUT CreateMouseInput(MouseEventFlags flags)
    {
        return new INPUT
        {
            type = InputType.Mouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = flags
                }
            }
        };
    }

    public static INPUT CreateKeyboardInput(ushort keyCode, KeyEventFlags flags)
    {
        return new INPUT
        {
            type = InputType.Keyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = keyCode,
                    dwFlags = flags
                }
            }
        };
    }
}


