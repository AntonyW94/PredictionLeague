namespace ThePredictions.SchemaCheck;

public enum CheckStatus
{
    /// <summary>The result set and the result type line up.</summary>
    Ok,

    /// <summary>A positional constructor exists but does not match the result set - this throws at runtime.</summary>
    Mismatch,

    /// <summary>No constructor can ever be matched and there is no parameterless one - this throws at runtime.</summary>
    Broken,

    /// <summary>Name-mapped: columns with nowhere to go, or members no column fills. Fails silently.</summary>
    SilentDrop,

    /// <summary>A mismatch that only appears under a guessed parameter typing, so it needs a human look.</summary>
    Review,

    /// <summary>Outside what the tool can check. Always listed with a reason, never silently dropped.</summary>
    Skipped
}
