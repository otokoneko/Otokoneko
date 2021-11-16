using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Otokoneko.Server.Gui.Utils
{
    public class AnsiFont
    {
        public Color Foreground { get; set; }
        public Color Background { get; set; }

        public static bool operator ==(AnsiFont c1, AnsiFont c2)
        {
            return c1.Foreground == c2.Foreground && c1.Background == c2.Background;
        }

        public static bool operator !=(AnsiFont c1, AnsiFont c2)
        {
            return c1.Foreground != c2.Foreground || c1.Background != c2.Background;
        }
    }

    public class TextWithColor
    {
        public string Text { get; set; }
        public AnsiFont Font { get; set; }
    }

    public class AnsiEscapeCodeUtils
    {
        public static AnsiFont DefaultFont => new AnsiFont()
        {
            Foreground = Color.FromArgb(0xC0, 0xC0, 0xC0),
            Background = Color.FromArgb(0x00, 0x00, 0x00)
        };

        private readonly static Color[] ColorsCodeLowerBetween0And7 = new Color[]
        {
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(128, 0, 0),
            Color.FromArgb(0,128,0),
            Color.FromArgb(128,128,0),
            Color.FromArgb(0, 0, 128),
            Color.FromArgb(128,0,128 ),
            Color.FromArgb(0,128,128),
            Color.FromArgb(192,192,192),
        };

        private static AnsiFont GetAnsiFont(string ansi, in AnsiFont currentFont)
        {
            AnsiFont result = new AnsiFont()
            {
                Foreground = currentFont.Foreground,
                Background = currentFont.Background,
            };
            string[] parameters = ansi.Split(';');

            var parseForeground = false;
            var parseBackground = false;
            int needParseColorParamNum = -1;
            var rgbPtr = 0;
            var rgb = new byte[3];

            foreach (var parameter in parameters)
            {
                var byteParameter = byte.Parse(parameter);
                if (parseForeground || parseBackground)
                {
                    if (needParseColorParamNum == -1)
                    {
                        needParseColorParamNum = byteParameter == 5 ? 1 : 3;
                        rgbPtr = 0;
                    }
                    else
                    {
                        Color? color = null;
                        if (needParseColorParamNum == 1)
                        {
                            // not implemented
                            color = parseForeground ? DefaultFont.Foreground : DefaultFont.Background;
                        }
                        else if (needParseColorParamNum == 3)
                        {
                            rgb[rgbPtr++] = byteParameter;
                            if (rgbPtr == 3)
                            {
                                color = Color.FromArgb(rgb[0], rgb[1], rgb[2]);
                            }
                        }
                        if (color != null)
                        {
                            if (parseForeground) result.Foreground = (Color)color;
                            else result.Background = (Color)color;
                            parseForeground = parseBackground = false;
                        }
                    }
                }
                else
                {
                    if (byteParameter == 0)
                    {
                        result = DefaultFont;
                    }
                    else if (byteParameter == 38)
                    {
                        parseForeground = true;
                    }
                    else if (byteParameter == 48)
                    {
                        parseBackground = true;
                    }
                    else if (byteParameter is >= 30 and <= 37)
                    {
                        result.Foreground = ColorsCodeLowerBetween0And7[byteParameter - 30];
                    }
                    else if (byteParameter is >= 40 and <= 47)
                    {
                        result.Background = ColorsCodeLowerBetween0And7[byteParameter - 40];
                    }
                }
            }

            return result;
        }

        public static List<TextWithColor> ParseAnsiEscapeColor(string ansitext, AnsiFont currentFont)
        {
            TextWithColor last = null;
            var result = new List<TextWithColor>();
            if (!ansitext.Contains('\u001b'))
            {
                last = new TextWithColor { Text = ansitext, Font = currentFont };
                result.Add(last);
                return result;
            }

            string[] items = ansitext.Split('\u001b');
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];

                if (string.IsNullOrWhiteSpace(item)) continue;

                if (i == 0 && (ansitext.Length == 0 || ansitext[0] != '\u001b'))
                {
                    if (last != null && last.Font == currentFont)
                    {
                        last.Text += item;
                    }
                    else
                    {
                        last = new TextWithColor { Text = item, Font = currentFont };
                        result.Add(last);
                    }
                    continue;
                }

                string[] splitted = item.Split(new char[] { 'm' }, 2);

                currentFont = GetAnsiFont(splitted[0].Trim('['), currentFont);

                var currentText = splitted.Length == 2 ? splitted[1] : null;
                if (last != null && last.Font == currentFont)
                {
                    last.Text += currentText;
                }
                else
                {
                    last = new TextWithColor { Text = currentText, Font = currentFont };
                    result.Add(last);
                }
            }

            return result;
        }
    }
}
