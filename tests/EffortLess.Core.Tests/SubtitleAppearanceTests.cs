#nullable enable
using System;
using System.Globalization;
using System.Text.Json;
using Xunit;
using EffortLess.Core.Playback;

namespace EffortLess.Core.Tests
{
    public class SubtitleAppearanceTests
    {
        [Fact]
        public void ToMpvProperties_ExactDefaults_MatchesVerifiedMap()
        {
            var properties = new SubtitleAppearance().ToMpvProperties();

            Assert.Equal(14, properties.Count);
            Assert.Equal("outline-and-shadow", properties["sub-border-style"]);
            Assert.Equal("sans-serif", properties["sub-font"]);
            Assert.Equal("38", properties["sub-font-size"]);
            Assert.Equal("#FFFFFFFF", properties["sub-color"]);
            Assert.Equal("100", properties["sub-pos"]);
            Assert.Equal("no", properties["sub-bold"]);
            Assert.Equal("no", properties["sub-italic"]);
            Assert.Equal("1.65", properties["sub-border-size"]);
            Assert.Equal("#FF000000", properties["sub-border-color"]);
            Assert.Equal("0", properties["sub-shadow-offset"]);
            Assert.Equal("#AF000000", properties["sub-back-color"]);
            Assert.Equal("1", properties["sub-scale"]);
            Assert.Equal("34", properties["sub-margin-y"]);
            Assert.Equal("scale", properties["sub-ass-override"]);
        }

        [Fact]
        public void ToMpvProperties_CustomizedAppearance_UsesForceOverride()
        {
            var properties = new SubtitleAppearance { Bold = true }.ToMpvProperties();

            Assert.Equal("force", properties["sub-ass-override"]);
            Assert.Equal("yes", properties["sub-bold"]);
        }

        [Fact]
        public void ToMpvProperties_CustomOpacity_EncodesAlphaBytes()
        {
            var appearance = new SubtitleAppearance
            {
                TextOpacity = 0.5d,
                BackgroundOpacity = 0.5d,
            };

            var properties = appearance.ToMpvProperties();

            Assert.Equal("#80FFFFFF", properties["sub-color"]);
            Assert.Equal("#80000000", properties["sub-border-color"]);
            Assert.Equal("#80000000", properties["sub-back-color"]);
        }

        [Fact]
        public void Normalize_NonFiniteAndInvalidValues_FallBackToDefaults()
        {
            var normalized = new SubtitleAppearance
            {
                FontFamily = null!,
                FontSize = double.NaN,
                TextColor = "not-a-color",
                TextOpacity = double.PositiveInfinity,
                VerticalPosition = double.NegativeInfinity,
                OutlineSize = double.NaN,
                OutlineColor = null!,
                ShadowOffset = double.PositiveInfinity,
                BackgroundOpacity = double.NaN,
                Scale = double.NaN,
                MarginBottom = double.NegativeInfinity,
            }.Normalize();

            Assert.Equal(new SubtitleAppearance(), normalized);
        }

        [Fact]
        public void Normalize_FiniteOutOfRangeValues_ClampsToBounds()
        {
            var normalized = new SubtitleAppearance
            {
                FontSize = 1d,
                TextOpacity = 3d,
                VerticalPosition = 150d,
                OutlineSize = 11d,
                ShadowOffset = -1d,
                BackgroundOpacity = -0.5d,
                Scale = 10d,
                MarginBottom = 201d,
            }.Normalize();

            Assert.Equal(8d, normalized.FontSize);
            Assert.Equal(1d, normalized.TextOpacity);
            Assert.Equal(100d, normalized.VerticalPosition);
            Assert.Equal(10d, normalized.OutlineSize);
            Assert.Equal(0d, normalized.ShadowOffset);
            Assert.Equal(0d, normalized.BackgroundOpacity);
            Assert.Equal(4d, normalized.Scale);
            Assert.Equal(200d, normalized.MarginBottom);
        }

        [Fact]
        public void Normalize_FontFamily_TrimsAndRejectsInvalidValues()
        {
            Assert.Equal(SubtitleAppearance.DefaultFontFamily, new SubtitleAppearance { FontFamily = null! }.Normalize().FontFamily);
            Assert.Equal(SubtitleAppearance.DefaultFontFamily, new SubtitleAppearance { FontFamily = "sans\u0000serif" }.Normalize().FontFamily);
            Assert.Equal(SubtitleAppearance.DefaultFontFamily, new SubtitleAppearance { FontFamily = new string('x', 129) }.Normalize().FontFamily);

            string longestAllowed = new string('a', 128);
            Assert.Equal(longestAllowed, new SubtitleAppearance { FontFamily = longestAllowed }.Normalize().FontFamily);
            Assert.Equal("Arial", new SubtitleAppearance { FontFamily = "  Arial  " }.Normalize().FontFamily);
        }

        [Fact]
        public void Normalize_Colors_RequireExactRgbFormatAndCanonicalizeCase()
        {
            Assert.Equal("#FFFFFF", new SubtitleAppearance { TextColor = "FFFFFF" }.Normalize().TextColor);
            Assert.Equal("#FFFFFF", new SubtitleAppearance { TextColor = "#12345" }.Normalize().TextColor);
            Assert.Equal("#FFFFFF", new SubtitleAppearance { TextColor = "#FFFFFF " }.Normalize().TextColor);
            Assert.Equal("#ABCDEF", new SubtitleAppearance { TextColor = "#abcdef" }.Normalize().TextColor);
            Assert.Equal("#000000", new SubtitleAppearance { OutlineColor = null! }.Normalize().OutlineColor);
        }

        [Fact]
        public void Normalize_IsIdempotent()
        {
            var source = new SubtitleAppearance
            {
                FontFamily = " Arial ",
                FontSize = 5d,
                TextColor = "#abcdef",
                TextOpacity = 2d,
                VerticalPosition = 150d,
                OutlineSize = 11d,
                OutlineColor = "#123456",
                ShadowOffset = -1d,
                BackgroundOpacity = 1.5d,
                Scale = 10d,
                MarginBottom = 201d,
            };

            var once = source.Normalize();
            var twice = once.Normalize();

            Assert.Equal(once, twice);
        }

        [Fact]
        public void Normalize_ReturnsNewInstanceWithoutMutatingSource()
        {
            var source = new SubtitleAppearance { FontSize = double.NaN, TextColor = "#123456" };

            var normalized = source.Normalize();

            Assert.NotSame(source, normalized);
            Assert.True(double.IsNaN(source.FontSize));
            Assert.Equal("#123456", source.TextColor);
        }

        [Fact]
        public void JsonSerialization_RoundTripsAppearance()
        {
            var original = new SubtitleAppearance
            {
                FontFamily = "Arial",
                FontSize = 42.5d,
                TextColor = "#00FF00",
                TextOpacity = 0.75d,
                VerticalPosition = 88d,
                Bold = true,
                Italic = false,
                OutlineSize = 2d,
                OutlineColor = "#123456",
                ShadowOffset = 1d,
                BackgroundOpacity = 0.25d,
                Scale = 1.5d,
                MarginBottom = 60d,
            };

            string json = JsonSerializer.Serialize(original);
            SubtitleAppearance restored = JsonSerializer.Deserialize<SubtitleAppearance>(json)!;

            Assert.Equal(original, restored);
        }

        [Fact]
        public void ToMpvProperties_UsesInvariantCultureForNumbers()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

                var properties = new SubtitleAppearance
                {
                    FontSize = 38.5d,
                    OutlineSize = 1.25d,
                    Scale = 1.5d,
                    MarginBottom = 10.5d,
                }.ToMpvProperties();

                Assert.Equal("38.5", properties["sub-font-size"]);
                Assert.Equal("1.25", properties["sub-border-size"]);
                Assert.Equal("1.5", properties["sub-scale"]);
                Assert.Equal("11", properties["sub-margin-y"]);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void RecordEquality_DefaultInstances_AreEqual()
        {
            var first = new SubtitleAppearance();
            var second = new SubtitleAppearance();

            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void WithExpression_CanResetCustomizedAppearanceToDefaults()
        {
            var customized = new SubtitleAppearance
            {
                Bold = true,
                FontFamily = "Arial",
                FontSize = 50d,
            };

            var reset = customized with
            {
                Bold = false,
                FontFamily = SubtitleAppearance.DefaultFontFamily,
                FontSize = SubtitleAppearance.DefaultFontSize,
            };

            Assert.Equal(new SubtitleAppearance(), reset);
        }
    }
}
