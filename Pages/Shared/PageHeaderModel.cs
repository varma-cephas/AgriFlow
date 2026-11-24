namespace AgriFlow.Pages;

/// <summary>Display data for the shared _PageHeader partial.</summary>
public class PageHeaderModel
{
    public string Icon { get; init; } = "bi-house-door";
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }

    /// <summary>Optional action buttons, supplied as a Razor template (@&lt;a&gt;...&lt;/a&gt;).</summary>
    public Func<dynamic, object>? Actions { get; init; }
}
