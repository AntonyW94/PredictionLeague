namespace PredictionLeague.Domain.Services.Boosts;

public static class BoostEligibilityEvaluator
{
    public static BoostEligibilityResult Evaluate(
        bool isEnabled,
        int totalUsesPerSeason,
        int seasonUses,
        int windowUses,
        bool hasUsedThisRound,
        int roundNumber,
        IReadOnlyList<BoostWindowSnapshot>? windows,
        bool isUserMemberOfLeague,
        bool isRoundInLeagueSeason)
    {
        if (!isRoundInLeagueSeason)
            return BoostEligibilityResult.NotAllowed("Round does not belong to this league's season.");
        
        if (!isUserMemberOfLeague)
            return BoostEligibilityResult.NotAllowed("User is not a member of this league.");

        if (!isEnabled)
            return BoostEligibilityResult.NotAllowed("Boost is not enabled for this league.");
        
        if (totalUsesPerSeason <= 0)
            return BoostEligibilityResult.NotAllowed("Boost cannot be used in this league.");

        if (hasUsedThisRound)
            return BoostEligibilityResult.AlreadyUsedThisRoundResult();

        if (seasonUses >= totalUsesPerSeason)
            return BoostEligibilityResult.NotAllowed("Season limit reached for this boost in this league.");
       
        BoostWindowSnapshot? activeWindow = null;

        if (windows != null && windows.Count > 0)
        {
            activeWindow = windows.FirstOrDefault(w => roundNumber >= w.StartRoundNumber && roundNumber <= w.EndRoundNumber);
            
            if (activeWindow == null)
                return BoostEligibilityResult.NotAllowed("Boost is not available for this round.");
            
            if (activeWindow.MaxUsesInWindow <= 0)
                return BoostEligibilityResult.NotAllowed("Boost cannot be used in this window.");
           
            if (windowUses >= activeWindow.MaxUsesInWindow)
                return BoostEligibilityResult.NotAllowed("Window limit reached for this boost in this league.");
        }
        
        var seasonRemaining = Math.Max(0, totalUsesPerSeason - seasonUses);
        var windowRemaining = activeWindow != null
            ? Math.Max(0, activeWindow.MaxUsesInWindow - windowUses)
            : seasonRemaining;

        return BoostEligibilityResult.Allowed(seasonRemaining, windowRemaining);
    }
}