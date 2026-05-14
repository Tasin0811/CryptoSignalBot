namespace CryptoSignalBot.Domain.PaperTrading;

public enum PaperTradeOutcome
{
    Open = 0,
    TakeProfit1 = 1,
    StopLoss = 2,
    Expired = 3,
    Invalid = 4,
    TakeProfit2 = 5
}
