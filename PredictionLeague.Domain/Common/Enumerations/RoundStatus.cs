using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Common.Enumerations;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum RoundStatus
{
    Draft,      
    Published,  
    Completed 
}