using CryptoSignalBot.Application.Abstractions;
using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Risk;

public sealed class RiskEngine : IRiskEngine
{
    public RiskPlan CreatePlan(decimal entryPrice, decimal atr, decimal accountBalance, decimal riskPercent)
    {
        if (entryPrice <= 0 || atr <= 0 || accountBalance <= 0 || riskPercent <= 0)
        {
            return new RiskPlan(RiskLevel.Blocked, entryPrice, null, null, null, null, null, null, "Risk plan blocked: invalid input.");
        }

        var stopDistance = Math.Max(atr * 1.5m, entryPrice * 0.015m);
        var stopLoss = entryPrice - stopDistance;

        if (stopLoss <= 0)
        {
            return new RiskPlan(RiskLevel.Blocked, entryPrice, null, null, null, null, null, null, "Risk plan blocked: stop loss is not valid.");
        }

        var takeProfit1 = entryPrice + (stopDistance * 1.5m);
        var takeProfit2 = entryPrice + (stopDistance * 2.5m);
        var riskAmount = accountBalance * riskPercent;
        var positionSize = riskAmount / stopDistance;
        var stopPercent = stopDistance / entryPrice;
        var riskLevel = stopPercent switch
        {
            > 0.08m => RiskLevel.High,
            > 0.035m => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return new RiskPlan(
            riskLevel,
            entryPrice,
            stopLoss,
            takeProfit1,
            takeProfit2,
            1.5m,
            2.5m,
            positionSize,
            $"Risk {riskLevel}; stop distance {stopPercent:P2}; theoretical size {positionSize:N4} units.");
    }
}
