#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EffortLess.Core.Playback
{
    public sealed record SubtitleAppearance
    {
        public const string DefaultFontFamily = "sans-serif";
        public const double DefaultFontSize = 38d;
        public const string DefaultTextColor = "#FFFFFF";
        public const double DefaultTextOpacity = 1d;
        public const double DefaultVerticalPosition = 100d;
        public const double DefaultOutlineSize = 1.65d;
        public const string DefaultOutlineColor = "#000000";
        public const double DefaultShadowOffset = 0d;
        public const double DefaultBackgroundOpacity = 175d / 255d;
        public const double DefaultScale = 1d;
        public const double DefaultMarginBottom = 34d;

        private const double MinFontSize = 8d;
        private const double MaxFontSize = 120d;
        private const double MinOpacity = 0d;
        private const double MaxOpacity = 1d;
        private const double MinPosition = 0d;
        private const double MaxPosition = 100d;
        private const double MinOutlineSize = 0d;
        private const double MaxOutlineSize = 10d;
        private const double MinShadowOffset = 0d;
        private const double MaxShadowOffset = 10d;
        private const double MinScale = 0.25d;
        private const double MaxScale = 4d;
        private const double MinMarginBottom = 0d;
        private const double MaxMarginBottom = 200d;
        private const int MaxFontFamilyLength = 128;
        private const string NumberFormat = "0.############################";

        private static readonly SubtitleAppearance Defaults = new();

        public string FontFamily { get; init; } = DefaultFontFamily;
        public double FontSize { get; init; } = DefaultFontSize;
        public string TextColor { get; init; } = DefaultTextColor;
        public double TextOpacity { get; init; } = DefaultTextOpacity;
        public double VerticalPosition { get; init; } = DefaultVerticalPosition;
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public double OutlineSize { get; init; } = DefaultOutlineSize;
        public string OutlineColor { get; init; } = DefaultOutlineColor;
        public double ShadowOffset { get; init; } = DefaultShadowOffset;
        public bool BackgroundBoxEnabled { get; init; }
        public double BackgroundOpacity { get; init; } = DefaultBackgroundOpacity;
        public double Scale { get; init; } = DefaultScale;
        public double MarginBottom { get; init; } = DefaultMarginBottom;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDefault => this == Defaults;

        public SubtitleAppearance Normalize()
        {
            return new SubtitleAppearance
            {
                FontFamily = NormalizeFontFamily(FontFamily),
                FontSize = NormalizeNumber(FontSize, DefaultFontSize, MinFontSize, MaxFontSize),
                TextColor = NormalizeColor(TextColor, DefaultTextColor),
                TextOpacity = NormalizeNumber(TextOpacity, DefaultTextOpacity, MinOpacity, MaxOpacity),
                VerticalPosition = NormalizeNumber(VerticalPosition, DefaultVerticalPosition, MinPosition, MaxPosition),
                Bold = Bold,
                Italic = Italic,
                OutlineSize = NormalizeNumber(OutlineSize, DefaultOutlineSize, MinOutlineSize, MaxOutlineSize),
                OutlineColor = NormalizeColor(OutlineColor, DefaultOutlineColor),
                ShadowOffset = NormalizeNumber(ShadowOffset, DefaultShadowOffset, MinShadowOffset, MaxShadowOffset),
                BackgroundBoxEnabled = BackgroundBoxEnabled,
                BackgroundOpacity = NormalizeNumber(BackgroundOpacity, DefaultBackgroundOpacity, MinOpacity, MaxOpacity),
                Scale = NormalizeNumber(Scale, DefaultScale, MinScale, MaxScale),
                MarginBottom = Math.Round(NormalizeNumber(MarginBottom, DefaultMarginBottom, MinMarginBottom, MaxMarginBottom), MidpointRounding.AwayFromZero),
            };
        }

        public IReadOnlyDictionary<string, string> ToMpvProperties()
        {
            SubtitleAppearance appearance = Normalize();

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sub-font"] = appearance.FontFamily,
                ["sub-font-size"] = FormatNumber(appearance.FontSize),
                ["sub-color"] = ToArgbColor(appearance.TextColor, appearance.TextOpacity),
                ["sub-pos"] = FormatNumber(appearance.VerticalPosition),
                ["sub-bold"] = YesNo(appearance.Bold),
                ["sub-italic"] = YesNo(appearance.Italic),
                ["sub-border-size"] = FormatNumber(appearance.OutlineSize),
                ["sub-border-color"] = ToArgbColor(appearance.OutlineColor, appearance.TextOpacity),
                ["sub-shadow-offset"] = FormatNumber(appearance.ShadowOffset),
                ["sub-border-style"] = appearance.BackgroundBoxEnabled ? "background-box" : "outline-and-shadow",
                ["sub-back-color"] = ToArgbColor("#000000", appearance.BackgroundOpacity),
                ["sub-scale"] = FormatNumber(appearance.Scale),
                ["sub-margin-y"] = FormatNumber(appearance.MarginBottom),
                ["sub-ass-override"] = appearance.IsDefault ? "scale" : "force",
            };
        }

        private static string NormalizeFontFamily(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultFontFamily;
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed.Length > MaxFontFamilyLength)
            {
                return DefaultFontFamily;
            }

            foreach (char character in trimmed)
            {
                if (char.IsControl(character))
                {
                    return DefaultFontFamily;
                }
            }

            return trimmed;
        }

        private static string NormalizeColor(string? value, string fallback)
        {
            if (value is null || value.Length != 7 || value[0] != '#')
            {
                return fallback;
            }

            for (int index = 1; index < 7; index++)
            {
                if (!IsHexDigit(value[index]))
                {
                    return fallback;
                }
            }

            return value.ToUpperInvariant();
        }

        private static bool IsHexDigit(char character)
        {
            return (character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'F')
                || (character >= 'a' && character <= 'f');
        }

        private static double NormalizeNumber(double value, double fallback, double minimum, double maximum)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            double clamped = Math.Clamp(value, minimum, maximum);
            return clamped == 0d ? 0d : clamped;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static string ToArgbColor(string rgb, double opacity)
        {
            if (rgb is null || rgb.Length != 7 || rgb[0] != '#')
            {
                return "#00000000";
            }

            double boundedOpacity;
            if (double.IsNaN(opacity) || double.IsInfinity(opacity))
            {
                boundedOpacity = 0d;
            }
            else
            {
                boundedOpacity = Math.Clamp(opacity, 0d, 1d);
            }

            int alpha = (int)Math.Round(boundedOpacity * 255d, MidpointRounding.AwayFromZero);
            if (alpha < 0)
            {
                alpha = 0;
            }
            else if (alpha > 255)
            {
                alpha = 255;
            }

            return $"#{alpha:X2}{rgb.Substring(1)}";
        }
    }
}
