using System;
using System.Text;

using Google.Protobuf.Collections;

namespace DemoApp {

    /// <summary>
    /// Some general utility methods.
    /// </summary>
    public static class Util {

        public static string Join(this RepeatedField<string> f, string separator) {
            if (f == null || f.Count == 0)
                return "";
            var builder = new StringBuilder();
            for (int i = 0; i < f.Count; i++) {
                if (i != 0) {
                    builder.Append(separator);
                }
                builder.Append(f[i]);
            }
            return builder.ToString();
        }

        public static bool EqualsIgnoreCase(this string s, string other) {
            if (s == null && other == null)
                return true;
            if (s == null || other == null)
                return false;
            var sl = s.Trim().ToLowerInvariant();
            var ol = other.Trim().ToLowerInvariant();
            return sl.Equals(ol);
        }

        public static bool IsEmpty(this string s) {
            return string.IsNullOrWhiteSpace(s);
        }

        public static void Log(string s) {
            Console.WriteLine(s);
        }
    }
}
