using Pty.Net.Windows;
using System;

namespace PtyWeb
{
    class Program
    {
        static void Main(string[] args)
        {
            Dummy();

            CliDemo.Run();

            // WebDemo.Run(args);
        }

        static void Dummy()
        {
            var (major, minor, build, productType) = WindowsVersion.GetRealVersion();
            Console.WriteLine($"Raw: {major}.{minor}.{build}, ProductType={productType}");

            var (kind, release) = WindowsReleaseInfo.GetRelease();
            Console.WriteLine($"Kind: {kind}, Release: {release}");

            Console.WriteLine($"DisplayName: {WindowsReleaseInfo.GetDisplayName(kind, release)}");
        }
    }
}
