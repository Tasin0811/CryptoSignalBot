using CryptoSignalBot.Application.Risk;
using CryptoSignalBot.Domain.Enums;

namespace CryptoSignalBot.Application.Tests;

public sealed class RiskEngineRulesTests
{
    [Fact]
    public void CreatePlan_UsesRiskPercentAndStopDistanceToSizePosition()
    {
        var engine = new RiskEngine();

        var plan = engine.CreatePlan(
            entryPrice: 100m,
            atr: 2m,
            accountBalance: 10_000m,
            riskPercent: 0.01m);

        Assert.NotNull(plan.StopLoss);
        Assert.True(plan.PositionSize > 0);
        Assert.Equal(1.5m, plan.RiskReward1);
        Assert.Equal(2.5m, plan.RiskReward2);
    }

    [Theory]
    [InlineData(0, 2, 10_000, 0.01)]
    [InlineData(100, 0, 10_000, 0.01)]
    [InlineData(100, 2, 0, 0.01)]
    [InlineData(100, 2, 10_000, 0)]
    public void CreatePlan_InvalidInputsBlockThePlan(decimal entryPrice, decimal atr, decimal accountBalance, decimal riskPercent)
    {
        var engine = new RiskEngine();

        var plan = engine.CreatePlan(entryPrice, atr, accountBalance, riskPercent);

        Assert.Equal(RiskLevel.Blocked, plan.RiskLevel);
        Assert.Null(plan.PositionSize);
    }

    [Fact]
    public void CreatePlan_ExposesNoLeverageParameterForV1()
    {
        var leverageParameter = typeof(RiskEngine)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .FirstOrDefault(parameter => parameter.Name?.Contains("leverage", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Null(leverageParameter);
    }
}
