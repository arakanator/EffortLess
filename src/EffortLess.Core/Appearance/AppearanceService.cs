using System;
using System.Threading;
using System.Threading.Tasks;
using EffortLess.Core.Library;

namespace EffortLess.Core.Appearance;

public sealed class AppearanceService
{
    private readonly JsonLocalStore<AppearanceSettings> _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppearanceSettings _current = new AppearanceSettings().Normalize();

    public AppearanceService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _store = new JsonLocalStore<AppearanceSettings>(filePath);
    }

    public AppearanceSettings Current => Volatile.Read(ref _current);

    public async Task<AppearanceSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var loaded = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            var normalized = loaded.Normalize();
            // Persist recognized legacy labels, preserving every other appearance preference.
            if (!AppearancePresetIds.IsKnown(loaded.ThemeId) &&
                normalized.ThemeId != AppearancePresetIds.Default)
                await _store.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _current, normalized);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppearanceSettings> UpdateAsync(
        AppearanceSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _store.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _current, normalized);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppearanceSettings> ResetAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new AppearanceSettings().Normalize();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _store.SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _current, defaults);
            return defaults;
        }
        finally
        {
            _gate.Release();
        }
    }
}
