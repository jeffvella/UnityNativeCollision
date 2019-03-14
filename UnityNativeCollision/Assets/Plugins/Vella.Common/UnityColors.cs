using UnityEngine;

namespace Vella.Common
{
    public static class UnityColors
    {
        public static Color ToOpacity(this Color color, float alpha)
        {
            return new Color(color.r,color.g,color.b, alpha);
        }

        // Unity Default Colors
        public static Color Black { get; } = Color.black;
        public static Color Blue { get; } = Color.blue;
        public static Color Clear { get; } = Color.clear;
        public static Color Cyan { get; } = Color.cyan;
        public static Color Gray { get; } = Color.gray;
        public static Color Green { get; } = Color.green;
        public static Color Grey { get; } = Color.grey;
        public static Color Magenta { get; } = Color.magenta;
        public static Color Red { get; } = Color.red;
        public static Color White { get; } = Color.white;
        public static Color Yellow { get; } = Color.yellow;

        // Custom Colors (Note Unity uses a 0-1 scale instead of 0-255)
        public static Color GhostDodgerBlue { get; } = new Color(30 / 255f, 144 / 255f, 255 / 255f, 0.65f);
        public static Color DarkDodgerBlue { get; } = new Color(19 / 255f, 90 / 255f, 159 / 255f, 1f);

        // Standard Colors
        public static Color Transparent { get; } = FromArgb(16777215);
        public static Color AliceBlue { get; } = FromArgb(-984833);
        public static Color AntiqueWhite { get; } = FromArgb(-332841);
        public static Color Aqua { get; } = FromArgb(-16711681);
        public static Color Aquamarine { get; } = FromArgb(-8388652);
        public static Color Azure { get; } = FromArgb(-983041);
        public static Color Beige { get; } = FromArgb(-657956);
        public static Color Bisque { get; } = FromArgb(-6972);
        //public static Color Black { get; } = FromArgb(-16777216);
        public static Color BlanchedAlmond { get; } = FromArgb(-5171);
        //public static Color Blue { get; } = FromArgb(-16776961);
        public static Color BlueViolet { get; } = FromArgb(-7722014);
        //public static Color Brown { get; } = FromArgb(-5952982, )
        public static Color BurlyWood { get; } = FromArgb(-2180985);
        public static Color CadetBlue { get; } = FromArgb(-10510688);
        public static Color Chartreuse { get; } = FromArgb(-8388864);
        public static Color Chocolate { get; } = FromArgb(-2987746);
        public static Color Coral { get; } = FromArgb(-32944);
        public static Color CornflowerBlue { get; } = FromArgb(-10185235);
        public static Color Cornsilk { get; } = FromArgb(-1828);
        public static Color Crimson { get; } = FromArgb(-2354116);
        //public static Color Cyan { get; } = FromArgb(-16711681);
        public static Color DarkBlue { get; } = FromArgb(-16777077);
        public static Color DarkCyan { get; } = FromArgb(-16741493);
        public static Color DarkGoldenrod { get; } = FromArgb(-4684277);
        public static Color DarkGray { get; } = FromArgb(-5658199);
        public static Color DarkGreen { get; } = FromArgb(-16751616);
        public static Color DarkKhaki { get; } = FromArgb(-4343957);
        public static Color DarkMagenta { get; } = FromArgb(-7667573);
        public static Color DarkOliveGreen { get; } = FromArgb(-11179217);
        public static Color DarkOrange { get; } = FromArgb(-29696);
        public static Color DarkOrchid { get; } = FromArgb(-6737204);
        public static Color DarkRed { get; } = FromArgb(-7667712);
        public static Color DarkSalmon { get; } = FromArgb(-1468806);
        public static Color DarkSeaGreen { get; } = FromArgb(-7357301);
        public static Color DarkSlateBlue { get; } = FromArgb(-12042869);
        public static Color DarkSlateGray { get; } = FromArgb(-13676721);
        public static Color DarkTurquoise { get; } = FromArgb(-16724271);
        public static Color DarkViolet { get; } = FromArgb(-7077677);
        public static Color DeepPink { get; } = FromArgb(-60269);
        public static Color DeepSkyBlue { get; } = FromArgb(-16728065);
        public static Color DimGray { get; } = FromArgb(-9868951);
        public static Color DodgerBlue { get; } = FromArgb(-14774017);
        public static Color Firebrick { get; } = FromArgb(-5103070);
        public static Color FloralWhite { get; } = FromArgb(-1296);
        public static Color ForestGreen { get; } = FromArgb(-14513374);
        public static Color Fuchsia { get; } = FromArgb(-65281);
        public static Color Gainsboro { get; } = FromArgb(-2302756);
        public static Color GhostWhite { get; } = FromArgb(-460545);
        public static Color Gold { get; } = FromArgb(-10496);
        public static Color Goldenrod { get; } = FromArgb(-2448096);
        //public static Color Gray { get; } = FromArgb(-8355712);
        //public static Color Green { get; } = FromArgb(-16744448);
        public static Color GreenYellow { get; } = FromArgb(-5374161);
        public static Color Honeydew { get; } = FromArgb(-983056);
        public static Color HotPink { get; } = FromArgb(-38476);
        public static Color IndianRed { get; } = FromArgb(-3318692);
        public static Color Indigo { get; } = FromArgb(-11861886);
        public static Color Ivory { get; } = FromArgb(-16);
        public static Color Khaki { get; } = FromArgb(-989556);
        public static Color Lavender { get; } = FromArgb(-1644806);
        public static Color LavenderBlush { get; } = FromArgb(-3851);
        public static Color LawnGreen { get; } = FromArgb(-8586240);
        public static Color LemonChiffon { get; } = FromArgb(-1331);
        public static Color LightBlue { get; } = FromArgb(-5383962);
        public static Color LightCoral { get; } = FromArgb(-1015680);
        public static Color LightCyan { get; } = FromArgb(-2031617);
        public static Color LightGoldenrodYellow { get; } = FromArgb(-329006);
        public static Color LightGray { get; } = FromArgb(-2894893);
        public static Color LightGreen { get; } = FromArgb(-7278960);
        public static Color LightPink { get; } = FromArgb(-18751);
        public static Color LightSalmon { get; } = FromArgb(-24454);
        public static Color LightSeaGreen { get; } = FromArgb(-14634326);
        public static Color LightSkyBlue { get; } = FromArgb(-7876870);
        public static Color LightSlateGray { get; } = FromArgb(-8943463);
        public static Color LightSteelBlue { get; } = FromArgb(-5192482);
        public static Color LightYellow { get; } = FromArgb(-32);
        public static Color Lime { get; } = FromArgb(-16711936);
        public static Color LimeGreen { get; } = FromArgb(-13447886);
        public static Color Linen { get; } = FromArgb(-331546);
        //public static Color Magenta { get; } = FromArgb(-65281);
        public static Color Maroon { get; } = FromArgb(-8388608);
        public static Color MediumAquamarine { get; } = FromArgb(-10039894);
        public static Color MediumBlue { get; } = FromArgb(-16777011);
        public static Color MediumOrchid { get; } = FromArgb(-4565549);
        public static Color MediumPurple { get; } = FromArgb(-7114533);
        public static Color MediumSeaGreen { get; } = FromArgb(-12799119);
        public static Color MediumSlateBlue { get; } = FromArgb(-8689426);
        public static Color MediumSpringGreen { get; } = FromArgb(-16713062);
        public static Color MediumTurquoise { get; } = FromArgb(-12004916);
        public static Color MediumVioletRed { get; } = FromArgb(-3730043);
        public static Color MidnightBlue { get; } = FromArgb(-15132304);
        public static Color MintCream { get; } = FromArgb(-655366);
        public static Color MistyRose { get; } = FromArgb(-6943);
        public static Color Moccasin { get; } = FromArgb(-6987);
        public static Color NavajoWhite { get; } = FromArgb(-8531);
        public static Color Navy { get; } = FromArgb(-16777088);
        public static Color OldLace { get; } = FromArgb(-133658);
        public static Color Olive { get; } = FromArgb(-8355840);
        public static Color OliveDrab { get; } = FromArgb(-9728477);
        public static Color Orange { get; } = FromArgb(-23296);
        public static Color OrangeRed { get; } = FromArgb(-47872);
        public static Color Orchid { get; } = FromArgb(-2461482);
        public static Color PaleGoldenrod { get; } = FromArgb(-1120086);
        public static Color PaleGreen { get; } = FromArgb(-6751336);
        public static Color PaleTurquoise { get; } = FromArgb(-5247250);
        public static Color PaleVioletRed { get; } = FromArgb(-2396013);
        public static Color PapayaWhip { get; } = FromArgb(-4139);
        public static Color PeachPuff { get; } = FromArgb(-9543);
        public static Color Peru { get; } = FromArgb(-3308225);
        public static Color Pink { get; } = FromArgb(-16181);
        public static Color Plum { get; } = FromArgb(-2252579);
        public static Color PowderBlue { get; } = FromArgb(-5185306);
        public static Color Purple { get; } = FromArgb(-8388480);
        //public static Color Red { get; } = FromArgb(-65536);
        public static Color RosyBrown { get; } = FromArgb(-4419697);
        public static Color RoyalBlue { get; } = FromArgb(-12490271);
        public static Color SaddleBrown { get; } = FromArgb(-7650029);
        public static Color Salmon { get; } = FromArgb(-360334);
        public static Color SandyBrown { get; } = FromArgb(-744352);
        public static Color SeaGreen { get; } = FromArgb(-13726889);
        public static Color SeaShell { get; } = FromArgb(-2578);
        public static Color Sienna { get; } = FromArgb(-6270419);
        public static Color Silver { get; } = FromArgb(-4144960);
        public static Color SkyBlue { get; } = FromArgb(-7876885);
        public static Color SlateBlue { get; } = FromArgb(-9807155);
        public static Color SlateGray { get; } = FromArgb(-9404272);
        public static Color Snow { get; } = FromArgb(-1286);
        public static Color SpringGreen { get; } = FromArgb(-16711809);
        public static Color SteelBlue { get; } = FromArgb(-12156236);
        public static Color Tan { get; } = FromArgb(-2968436);
        public static Color Teal { get; } = FromArgb(-16744320);
        public static Color Thistle { get; } = FromArgb(-2572328);
        public static Color Tomato { get; } = FromArgb(-40121);
        public static Color Turquoise { get; } = FromArgb(-12525360);
        public static Color Violet { get; } = FromArgb(-1146130);
        public static Color Wheat { get; } = FromArgb(-663885);
        //public static Color White { get; } = FromArgb(-1);
        public static Color WhiteSmoke { get; } = FromArgb(-657931);
        //public static Color Yellow { get; } = FromArgb(-256);
        public static Color YellowGreen { get; } = FromArgb(-6632142);



        private static long ToArgb(this Color color)
        {
            return (long)(uint)((byte)color.r * 255 << 16 | (byte)color.g * 255 << 8 | (byte)color.b * 255 | (byte)color.a * 255 << 24) & uint.MaxValue;
        }

        public static Color FromArgb(long argb)
        {
            var r = (byte)((ulong)(argb >> 16) & (ulong)byte.MaxValue);
            var g = (byte)((ulong)(argb >> 8) & (ulong)byte.MaxValue);
            var b = (byte)((ulong)argb & (ulong)byte.MaxValue);
            var a = (byte)((ulong)(argb >> 24) & (ulong)byte.MaxValue);
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        //public static Color FromHex(string colorcode)
        //{
        //    return FromArgb(int.Parse(colorcode.Replace("#", ""), NumberStyles.HexNumber));
        //}

        private static string ToHex(this Color color)
        {
            return "#" + color.a.ToString("X2") + color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
        }

        public static float GetBrightness(this Color color)
        {
            float num1 = (float)color.r * 255 / (float)byte.MaxValue;
            float num2 = (float)color.g * 255 / (float)byte.MaxValue;
            float num3 = (float)color.b * 255 / (float)byte.MaxValue;
            float num4 = num1;
            float num5 = num1;
            if ((double)num2 > (double)num4)
                num4 = num2;
            if ((double)num3 > (double)num4)
                num4 = num3;
            if ((double)num2 < (double)num5)
                num5 = num2;
            if ((double)num3 < (double)num5)
                num5 = num3;
            return (float)(((double)num4 + (double)num5) / 2.0);
        }

        public static float GetHue(this Color color)
        {
            if ((int)color.r == (int)color.g && (int)color.g == (int)color.b)
                return 0.0f;
            float num1 = (float)color.r * 255 / (float)byte.MaxValue;
            float num2 = (float)color.g * 255 / (float)byte.MaxValue;
            float num3 = (float)color.b * 255 / (float)byte.MaxValue;
            float num4 = 0.0f;
            float num5 = num1;
            float num6 = num1;
            if ((double)num2 > (double)num5)
                num5 = num2;
            if ((double)num3 > (double)num5)
                num5 = num3;
            if ((double)num2 < (double)num6)
                num6 = num2;
            if ((double)num3 < (double)num6)
                num6 = num3;
            float num7 = num5 - num6;
            if ((double)num1 == (double)num5)
                num4 = (num2 - num3) / num7;
            else if ((double)num2 == (double)num5)
                num4 = (float)(2.0 + ((double)num3 - (double)num1) / (double)num7);
            else if ((double)num3 == (double)num5)
                num4 = (float)(4.0 + ((double)num1 - (double)num2) / (double)num7);
            float num8 = num4 * 60f;
            if ((double)num8 < 0.0)
                num8 += 360f;
            return num8;
        }

        public static float GetSaturation(this Color color)
        {
            float num1 = (float)color.r * 255 / (float)byte.MaxValue;
            float num2 = (float)color.g * 255 / (float)byte.MaxValue;
            float num3 = (float)color.b * 255 / (float)byte.MaxValue;
            float num4 = 0.0f;
            float num5 = num1;
            float num6 = num1;
            if ((double)num2 > (double)num5)
                num5 = num2;
            if ((double)num3 > (double)num5)
                num5 = num3;
            if ((double)num2 < (double)num6)
                num6 = num2;
            if ((double)num3 < (double)num6)
                num6 = num3;
            if ((double)num5 != (double)num6)
                num4 = ((double)num5 + (double)num6) / 2.0 > 0.5 ? (float)(((double)num5 - (double)num6) / (2.0 - (double)num5 - (double)num6)) : (float)(((double)num5 - (double)num6) / ((double)num5 + (double)num6));
            return num4;
        }
    }

}