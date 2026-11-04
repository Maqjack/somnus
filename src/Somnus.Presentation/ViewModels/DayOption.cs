using CommunityToolkit.Mvvm.ComponentModel;
using Somnus.Core.Scheduling;

namespace Somnus.Presentation.ViewModels;

/// <summary>
/// One day of the week in the strip under the clock time (#83).
/// </summary>
public sealed partial class DayOption : ObservableObject
{
    private readonly Action _selectionChanged;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="day">The day.</param>
    /// <param name="selectionChanged">Called when the day is ticked or unticked.</param>
    public DayOption(WeekDays day, Action selectionChanged)
    {
        ArgumentNullException.ThrowIfNull(selectionChanged);
        Day = day;
        _selectionChanged = selectionChanged;
    }

    /// <summary>Gets the day.</summary>
    public WeekDays Day { get; }

    /// <summary>Gets or sets a value indicating whether the countdown repeats on this day.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets or sets the short name on the strip.</summary>
    [ObservableProperty]
    public partial string ShortName { get; set; } = string.Empty;

    /// <summary>Gets or sets the full name, which screen readers read out.</summary>
    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();
}
