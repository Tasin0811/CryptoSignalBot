using CryptoSignalBot.Domain.Enums;

namespace CryptoSignalBot.Domain.Signals;

public sealed record RuleResult(
    string RuleName,
    decimal ScoreImpact,
    RuleResultType Result,
    string? Details = null);
