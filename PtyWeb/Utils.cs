using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PtyWeb
{
    public class Utils
    {
        public readonly static Regex reNetFramework = new Regex(@"^\.net\sframework");

        public readonly static bool IsFramework = reNetFramework.IsMatch(RuntimeInformation.FrameworkDescription);


        static Utils()
        {
            if (!IsFramework)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
        }

        public static Encoding GetTerminalEncoding()
        {
            var encoding = Encoding.UTF8;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            }
            return encoding;
        }
    }
}
