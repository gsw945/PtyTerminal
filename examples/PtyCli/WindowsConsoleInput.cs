using Pty.Net;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PtyCli
{
    /// <summary>
    /// Windows console input reader based on ReadConsoleInputW.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Console.OpenStandardInput()/ReadFile cannot deliver IME composition (Chinese,
    /// Japanese, ...) reliably while the console is in raw mode: composed characters
    /// only surface as KEY_EVENT records. This reader consumes KEY_EVENT records
    /// directly so that:
    ///   - IME text (any script) is forwarded as UTF-8 bytes;
    ///   - special keys (arrows, Home/End, PgUp/PgDn, Insert/Delete, F1-F12) are
    ///     forwarded as the same VT escape sequences a terminal emulator would send;
    ///   - Ctrl/Alt modifiers on cursor keys produce xterm-style CSI sequences.
    /// Non-key events (mouse, window resize, focus) are ignored.
    /// </remarks>
    internal static class WindowsConsoleInput
    {
        private const int STD_INPUT_HANDLE = -10;
        private const ushort KEY_EVENT = 0x0001;

        // dwControlKeyState flags
        private const uint RIGHT_ALT_PRESSED = 0x0001;
        private const uint LEFT_ALT_PRESSED = 0x0002;
        private const uint RIGHT_CTRL_PRESSED = 0x0004;
        private const uint LEFT_CTRL_PRESSED = 0x0008;
        private const uint SHIFT_PRESSED = 0x0010;

        private const uint MOD_ALT = LEFT_ALT_PRESSED | RIGHT_ALT_PRESSED;
        private const uint MOD_CTRL = LEFT_CTRL_PRESSED | RIGHT_CTRL_PRESSED;

        // Windows virtual key codes used for the VT mapping.
        private const ushort VK_LEFT = 0x25;
        private const ushort VK_UP = 0x26;
        private const ushort VK_RIGHT = 0x27;
        private const ushort VK_DOWN = 0x28;
        private const ushort VK_HOME = 0x24;
        private const ushort VK_END = 0x23;
        private const ushort VK_PRIOR = 0x21; // PgUp
        private const ushort VK_NEXT = 0x22;  // PgDn
        private const ushort VK_INSERT = 0x2D;
        private const ushort VK_DELETE = 0x2E;
        private const ushort VK_F1 = 0x70;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct KEY_EVENT_RECORD
        {
            // BOOL is 4 bytes on Windows; using uint keeps the layout aligned with
            // the native KEY_EVENT_RECORD (16 bytes) so UnicodeChar etc. are read
            // from the right offsets.
            public uint bKeyDown;

            public ushort wRepeatCount;
            public ushort wVirtualKeyCode;
            public ushort wVirtualScanCode;
            public char UnicodeChar;
            public uint dwControlKeyState;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_RECORD
        {
            [FieldOffset(0)]
            public ushort EventType;

            [FieldOffset(4)]
            public KEY_EVENT_RECORD KeyEvent;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadConsoleInputW(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNumberOfConsoleInputEvents(IntPtr hConsoleInput, out uint lpcNumberOfEvents);

        /// <summary>
        /// Forwards Windows console key events to the pty until the token is cancelled.
        /// </summary>
        public static async Task PumpAsync(System.IO.Stream ptyWriter, CancellationToken ct)
        {
            IntPtr hIn = GetStdHandle(STD_INPUT_HANDLE);
            if (hIn == IntPtr.Zero || hIn == new IntPtr(-1))
            {
                return;
            }

            var records = new INPUT_RECORD[64];
            var pending = new List<byte>(32);
            while (!ct.IsCancellationRequested)
            {
                // Poll instead of blocking on ReadConsoleInputW: a blocking read on
                // the console handle would stall the async pump (and whoever awaits
                // it) until the next key press, delaying startup output and exit.
                if (!GetNumberOfConsoleInputEvents(hIn, out uint available) || available == 0)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                uint toRead = Math.Min(available, (uint)records.Length);
                if (!ReadConsoleInputW(hIn, records, toRead, out uint count))
                {
                    // Console closed (e.g. window destroyed); stop pumping.
                    break;
                }

                for (int i = 0; i < count; i++)
                {
                    if (records[i].EventType != KEY_EVENT || records[i].KeyEvent.bKeyDown == 0)
                    {
                        continue;
                    }

                    pending.Clear();
                    if (!TryEncode(records[i].KeyEvent, pending))
                    {
                        continue;
                    }

                    await ptyWriter.WriteInputAsync(pending.ToArray(), ct);
                }
            }
        }

        /// <summary>
        /// Encodes one key-down event as UTF-8 bytes or a VT sequence. Returns false
        /// when the event should be skipped (e.g. modifier-only key presses).
        /// </summary>
        private static bool TryEncode(KEY_EVENT_RECORD key, List<byte> bytes)
        {
            if (key.UnicodeChar != '\0')
            {
                // Normal character, including IME-composed text and Ctrl+letter
                // control characters (ReadConsoleInput delivers them as UnicodeChar).
                AppendUtf8(bytes, key.UnicodeChar);
                return true;
            }

            // Modifier-only keys (Ctrl/Alt/Shift by themselves) produce no output.
            switch (key.wVirtualKeyCode)
            {
                case 0x10: // VK_SHIFT
                case 0x11: // VK_CONTROL
                case 0x12: // VK_MENU
                case 0x5B: // VK_LWIN
                case 0x5C: // VK_RWIN
                case 0x5D: // VK_APPS
                    return false;
            }

            uint ctrl = key.dwControlKeyState & MOD_CTRL;
            uint alt = key.dwControlKeyState & MOD_ALT;
            uint shift = key.dwControlKeyState & SHIFT_PRESSED;

            if (ctrl != 0 && alt == 0)
            {
                // Ctrl+letter: ASCII control character (only when UnicodeChar was
                // zero, which happens for some layouts/keys).
                if (key.wVirtualKeyCode >= 0x41 && key.wVirtualKeyCode <= 0x5A)
                {
                    bytes.Add((byte)(key.wVirtualKeyCode - 0x40));
                    return true;
                }
            }

            // Cursor key / editing key mapping (xterm-style).
            int modifier = 1 + (shift != 0 ? 1 : 0) + (alt != 0 ? 2 : 0) + (ctrl != 0 ? 4 : 0);
            string? seq = null;

            switch (key.wVirtualKeyCode)
            {
                case VK_UP: seq = modSeq("A", modifier); break;
                case VK_DOWN: seq = modSeq("B", modifier); break;
                case VK_RIGHT: seq = modSeq("C", modifier); break;
                case VK_LEFT: seq = modSeq("D", modifier); break;
                case VK_HOME: seq = modifier == 1 ? "\u001b[H" : modSeq("H", modifier); break;
                case VK_END: seq = modifier == 1 ? "\u001b[F" : modSeq("F", modifier); break;
                case VK_PRIOR: seq = "\u001b[5~"; break;
                case VK_NEXT: seq = "\u001b[6~"; break;
                case VK_INSERT: seq = "\u001b[2~"; break;
                case VK_DELETE: seq = "\u001b[3~"; break;
                default:
                    if (key.wVirtualKeyCode >= VK_F1 && key.wVirtualKeyCode <= VK_F1 + 11)
                    {
                        seq = FKeySequence(key.wVirtualKeyCode - VK_F1);
                    }
                    break;
            }

            if (seq == null)
            {
                return false;
            }

            foreach (char c in seq)
            {
                AppendUtf8(bytes, c);
            }

            return true;
        }

        private static string modSeq(string suffix, int modifier)
        {
            return modifier == 1 ? "\u001b[" + suffix : "\u001b[1;" + modifier + suffix;
        }

        private static string FKeySequence(int index)
        {
            // F1-F4 use SS3; F5-F12 use CSI with the classic mapping.
            switch (index)
            {
                case 0: return "\u001bOP";
                case 1: return "\u001bOQ";
                case 2: return "\u001bOR";
                case 3: return "\u001bOS";
                case 4: return "\u001b[15~";
                case 5: return "\u001b[17~";
                case 6: return "\u001b[18~";
                case 7: return "\u001b[19~";
                case 8: return "\u001b[20~";
                case 9: return "\u001b[21~";
                case 10: return "\u001b[23~";
                case 11: return "\u001b[24~";
                default: return string.Empty;
            }
        }

        private static void AppendUtf8(List<byte> bytes, char c)
        {
            int code = c;
            if (code < 0x80)
            {
                bytes.Add((byte)code);
            }
            else if (code < 0x800)
            {
                bytes.Add((byte)(0xC0 | (code >> 6)));
                bytes.Add((byte)(0x80 | (code & 0x3F)));
            }
            else
            {
                bytes.Add((byte)(0xE0 | (code >> 12)));
                bytes.Add((byte)(0x80 | ((code >> 6) & 0x3F)));
                bytes.Add((byte)(0x80 | (code & 0x3F)));
            }
        }
    }
}