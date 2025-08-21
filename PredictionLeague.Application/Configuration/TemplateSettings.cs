﻿using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TemplateSettings
{
    public long JoinLeagueRequest { get; set; }
    public long PredictionsMissing { get; set; }
}