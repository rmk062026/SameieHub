namespace SameieHub.Web.Components.Shared;

/// <summary>A filter that is switched on in a list, shown as a small removable label.</summary>
/// <param name="Key">Tells the page which filter to switch off.</param>
/// <param name="Text">What the person reads, for example "Status: Pågår".</param>
public record ActiveFilter(string Key, string Text);

/// <summary>A choice in <see cref="QuickTabs"/>. With an address it is a link, otherwise a button that filters the list.</summary>
public record QuickTab(string Key, string Text, int? Count = null, string? Href = null);

public enum FeedbackKind
{
    Success,
    Error
}
