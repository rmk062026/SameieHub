using AngleSharp.Dom;
using Bunit;

namespace SameieHub.Tests.Web;

/// <summary>
/// Small lookups for UI tests, so a test says what it is looking for (a row with some text, a field with a label, an element with a test
/// id) instead of how the markup happens to be built. When a page is redesigned, only these helpers need to know about the new markup.
/// </summary>
public static class UiTestHelpers
{
    /// <summary>The element marked with <c>data-testid</c>. Fails with a clear message if there is none.</summary>
    public static IElement FindByTestId<T>(this IRenderedComponent<T> cut, string testId)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        return cut.Find($"[data-testid='{testId}']");
    }

    public static bool HasTestId<T>(this IRenderedComponent<T> cut, string testId)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        return cut.FindAll($"[data-testid='{testId}']").Count > 0;
    }

    /// <summary>The first table row, list item or card row whose text contains <paramref name="text"/>.</summary>
    public static IElement FindRowContaining<T>(this IRenderedComponent<T> cut, string text)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        var row = cut.FindAll("tbody tr, [role=row], li, .list-row")
            .FirstOrDefault(element => element.TextContent.Contains(text, StringComparison.Ordinal));

        return row ?? throw new InvalidOperationException($"No row contains the text \"{text}\".");
    }

    /// <summary>The form control that belongs to the label that starts with <paramref name="labelText"/> (the star for required fields is ignored).</summary>
    public static IElement FindFieldByLabel<T>(this IRenderedComponent<T> cut, string labelText)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        var label = cut.FindAll("label")
            .FirstOrDefault(element => element.TextContent.Trim().StartsWith(labelText, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No label starts with \"{labelText}\".");
        var id = label.GetAttribute("for") ?? throw new InvalidOperationException($"The label \"{labelText}\" has no \"for\".");

        return cut.Find($"#{id}");
    }

    /// <summary>The button or link button with exactly this visible text.</summary>
    public static IElement FindButton<T>(this IRenderedComponent<T> cut, string text)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        var button = cut.FindAll("button, a.btn")
            .FirstOrDefault(element => element.TextContent.Trim() == text)
            ?? throw new InvalidOperationException($"No button has the text \"{text}\".");

        return button;
    }

    public static void ClickButton<T>(this IRenderedComponent<T> cut, string text)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        cut.FindButton(text).Click();
    }
}
