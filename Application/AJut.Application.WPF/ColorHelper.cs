namespace AJut.Application
{
    using System;
    using System.Windows.Media;
    public static class ColorHelper
    {
        /// <summary>
        /// Supports #RGB (Converts to #RRGGBB), #RRGGBB, and #AARRGGBB
        /// </summary>
        public static bool TryGetColorFromHex (string hex, out Color color)
        {
            if (hex.StartsWith("#"))
            {
                switch (hex.Length)
                {
                    // #RGB -> #RRGGBB
                    case 4:
                        {
                            byte[] bytes = _ReadHexBytes(3, charsPerByte: 1);
                            color = new Color { A = 255, R = bytes[0], G = bytes[1], B = bytes[2] };
                            return true;
                        }

                    // #ARGB -> #AARRGGBB
                    case 5:
                        {
                            byte[] bytes = _ReadHexBytes(4, charsPerByte: 1);
                            color = new Color { A = bytes[0], R = bytes[1], G = bytes[2], B = bytes[3] };
                            return true;
                        }

                    // #RRGGBB
                    case 7:
                        {
                            byte[] bytes = _ReadHexBytes(3, charsPerByte: 2);
                            color = new Color { A = 255, R = bytes[0], G = bytes[1], B = bytes[2] };
                            return true;
                        }

                    // #AARRGGBB
                    case 9:
                        {
                            byte[] bytes = _ReadHexBytes(4, charsPerByte: 2);
                            color = new Color { A = bytes[0], R = bytes[1], G = bytes[2], B = bytes[3] };
                            return true;
                        }
                }
            }

            color = Colors.Black;
            return false;

            byte[] _ReadHexBytes (int byteCount, int charsPerByte = 2)
            {
                byte[] bytes = new byte[byteCount];
                for (int index = 0; index < byteCount; ++index)
                {
                    int start = (index * charsPerByte) + 1;

                    string digit = hex.Substring(start, charsPerByte);
                    if (digit.Length == 1)
                    {
                        digit = $"{digit}{digit}";
                    }

                    bytes[index] = (byte)(Convert.ToUInt32(digit, 16));
                }

                return bytes;
            }
        }

        /// <summary>
        /// Color's normal ToString will give you hex, but if you have something like #CC000000 that can be stored as #C000 or #FF112233 can be stored as #123
        /// </summary>
        public static string GetSmallestHexString (this Color color)
        {
            string colorStr = color.ToString();
            switch (colorStr.Length)
            {
                // #RRGGBB -> #RGB
                case 7:
                    {
                        if (_IsMatch(0) && _IsMatch(1) && _IsMatch(2))
                        {
                            return $"#{_Color(0)}{_Color(1)}{_Color(2)}";
                        }

                        return colorStr;
                    }

                // #AARRGGBB -> #ARGB
                case 9:
                    {
                        if (_IsMatch(0) && _IsMatch(1) && _IsMatch(2) && _IsMatch(3))
                        {
                            // Alpha of FF is assumed by default
                            if (_Color(0) == 'F' || _Color(0) == 'f')
                            {
                                return $"#{_Color(1)}{_Color(2)}{_Color(3)}";
                            }

                            return $"#{_Color(0)}{_Color(1)}{_Color(2)}{_Color(3)}";
                        }

                        return colorStr;
                    }

                default:
                    return colorStr;
            }

            char _Color (int _color) => colorStr[_color * 2];
            bool _IsMatch (int _color) => colorStr[_color * 2] == colorStr[_color * 2 + 1];
        }
    }
}
