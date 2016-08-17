using System;
using System.Text;

namespace Pospa.Mirror.Common.Utils
{
    public static class StringExtensions
    {
        public static string EncodeBase64(this string text, Encoding encoding = null)
        {
            if (text == null)
            {
                return null;
            }

            encoding = encoding ?? Encoding.UTF8;

            byte[] textAsBytes = encoding.GetBytes(text);
            return Convert.ToBase64String(textAsBytes);
        }

        public static string DecodeBase64(this string text, Encoding encoding = null)
        {
            if (text == null)
            {
                return null;
            }

            encoding = encoding ?? Encoding.UTF8;

            var base64EncodedBytes = Convert.FromBase64String(text);
            return encoding.GetString(base64EncodedBytes, 0, base64EncodedBytes.Length);
        }
    }
}
