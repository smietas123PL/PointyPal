using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PointyPal.Infrastructure;

internal static class ConsoleOutput
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(
        IntPtr hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    public static void WriteLine(string text)
    {
        try
        {
            AttachConsole(AttachParentProcess);
            byte[] bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            WriteFile(GetStdHandle(StdOutputHandle), bytes, (uint)bytes.Length, out _, IntPtr.Zero);
        }
        catch
        {
            Console.WriteLine(text);
        }
    }
}
