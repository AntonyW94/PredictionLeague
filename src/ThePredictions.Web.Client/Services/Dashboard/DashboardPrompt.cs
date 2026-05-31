namespace ThePredictions.Web.Client.Services.Dashboard;

/// <summary>
/// A single call-to-action shown in the dashboard prompt strip (above the tabs on mobile,
/// above the tile grid on desktop). Condition-driven: prompts are recomputed from state, so
/// they self-dismiss once the underlying task is done (e.g. the pass is acquired).
/// Add new prompts (e.g. "add your mobile", "upload a photo") by appending to the list.
/// </summary>
/// <param name="Highlight">Optional phrase within <paramref name="Message"/> to emphasise (e.g. the season name).</param>
public record DashboardPrompt(string Icon, string Message, string ActionLabel, string ActionHref, string? Highlight = null);
