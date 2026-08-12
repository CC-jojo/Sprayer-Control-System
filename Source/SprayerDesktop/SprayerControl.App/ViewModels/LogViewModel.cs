using System.Collections.ObjectModel;

namespace SprayerControl.App.ViewModels;

public sealed class LogViewModel : ObservableObject
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly ObservableCollection<string> _entries = [];
    private readonly ObservableCollection<string> _recentEntries = [];
    private readonly ReadOnlyObservableCollection<string> _readOnlyEntries;
    private readonly ReadOnlyObservableCollection<string> _readOnlyRecentEntries;

    public LogViewModel(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.Now);
        _readOnlyEntries = new ReadOnlyObservableCollection<string>(_entries);
        _readOnlyRecentEntries = new ReadOnlyObservableCollection<string>(_recentEntries);
    }

    public ReadOnlyObservableCollection<string> Entries => _readOnlyEntries;

    public ReadOnlyObservableCollection<string> RecentEntries => _readOnlyRecentEntries;

    public void Add(string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);
        _entries.Add($"{_clock():HH:mm:ss}  {entry}");
        RebuildRecentEntries();
    }

    public void Clear()
    {
        _entries.Clear();
        _recentEntries.Clear();
    }

    private void RebuildRecentEntries()
    {
        _recentEntries.Clear();
        foreach (string entry in _entries.Skip(Math.Max(0, _entries.Count - 5)))
            _recentEntries.Add(entry);
    }
}
