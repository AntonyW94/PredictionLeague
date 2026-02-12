using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Common.Enumerations;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum MatchStatus
{
    [Description("Scheduled")]
    Scheduled,
    [Description("In Progress")]
    InProgress,
    [Description("Completed")]
    Completed
}