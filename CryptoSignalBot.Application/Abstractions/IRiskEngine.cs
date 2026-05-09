using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Application.Abstractions;

public interface IRiskEngine
{
    RiskPlan CreatePlan(decimal entryPrice, decimal atr, decimal accountBalance, decimal riskPercent);
}
