namespace EffortLess.Core.Playback;

public enum MediaTrackKind { Video, Audio, Subtitle }

public sealed record MediaTrack(long Id, MediaTrackKind Kind, string Title, string Language,
    string Codec, bool Selected, bool External)
{
    public string DisplayName => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(Title) ? $"{Kind} {Id}" : Title,
        Language, Codec, External ? "External" : ""
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
