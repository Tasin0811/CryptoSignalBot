using CryptoSignalBot.Domain.Enums;
using CryptoSignalBot.Domain.Signals;

namespace CryptoSignalBot.Infrastructure.Notifications;

public static class SignalMessageFormatter
{
    public static string FormatBeginnerEmail(Signal signal)
    {
        var lines = new List<string>
        {
            $"{signal.Symbol} - {FormatSignalType(signal.SignalType)}",
            $"Timeframe: {signal.Timeframe}",
            $"Score: {signal.Score:0.##}/10",
            $"Prezzo attuale: {FormatPrice(signal.Price)}",
            "",
            "DECISIONE RAPIDA",
            GetQuickDecision(signal),
            "",
            "COSA SIGNIFICA",
            GetMeaning(signal),
            "",
            "COSA FARE ADESSO",
            GetAction(signal),
            "",
            "LIVELLI DA GUARDARE",
            $"Ingresso di riferimento: {FormatPrice(signal.Price)}",
            $"Stop loss: {FormatNullable(signal.StopLoss)}",
            $"Take profit 1: {FormatNullable(signal.TakeProfit1)}",
            $"Take profit 2: {FormatNullable(signal.TakeProfit2)}",
            $"Rapporto rischio/rendimento: {FormatNullable(signal.RiskReward)}",
            $"Rischio stimato: {FormatRisk(signal.RiskLevel)}",
            "",
            "QUANDO IGNORARLO",
            GetInvalidation(signal),
            "",
            "RIASSUNTO TECNICO",
            signal.Summary,
            "",
            "REGOLE CHE HANNO PESATO NELLO SCORE"
        };

        lines.AddRange(signal.RuleResults.Select(rule =>
            $"- {rule.RuleName}: {rule.ScoreImpact:+0.##;-0.##;0} ({FormatRuleResult(rule.Result)}) {rule.Details}"));

        lines.Add("");
        lines.Add("Nota: e' un alert analitico, non un consiglio finanziario. Nessun ordine e' stato eseguito.");

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatBeginnerTelegram(Signal signal)
    {
        return $"*{signal.Symbol}* - {EscapeMarkdown(FormatSignalType(signal.SignalType))}\n" +
               $"Score: *{signal.Score:0.##}/10* | TF: {EscapeMarkdown(signal.Timeframe)}\n" +
               $"Prezzo: {EscapeMarkdown(FormatPrice(signal.Price))}\n\n" +
               $"*Decisione rapida*\n{EscapeMarkdown(GetQuickDecision(signal))}\n\n" +
               $"*Cosa significa*\n{EscapeMarkdown(GetMeaning(signal))}\n\n" +
               $"*Cosa fare adesso*\n{EscapeMarkdown(GetAction(signal))}\n\n" +
               $"*Livelli*\n" +
               $"Ingresso: {EscapeMarkdown(FormatPrice(signal.Price))}\n" +
               $"SL: {EscapeMarkdown(FormatNullable(signal.StopLoss))}\n" +
               $"TP1: {EscapeMarkdown(FormatNullable(signal.TakeProfit1))}\n" +
               $"R/R: {EscapeMarkdown(FormatNullable(signal.RiskReward))}\n\n" +
               "_Alert analitico, non consiglio finanziario. Nessun ordine eseguito._";
    }

    public static string FormatReportActionLine(Signal signal)
    {
        return signal.SignalType switch
        {
            SignalType.HighQualitySetup => "Setup forte: da valutare con attenzione, aspettando conferma del prezzo.",
            SignalType.BuyWatch => "Setup interessante: osservare, senza rincorrere il prezzo.",
            SignalType.Watch => "Da monitorare: non abbastanza forte per agire.",
            SignalType.Wait => "Attendere: quadro ancora debole o poco chiaro.",
            _ => "Evitare: il setup non e' favorevole."
        };
    }

    public static string FormatSignalType(SignalType signalType)
    {
        return signalType switch
        {
            SignalType.HighQualitySetup => "Setup forte",
            SignalType.BuyWatch => "Da osservare per possibile acquisto",
            SignalType.Watch => "Solo monitoraggio",
            SignalType.Wait => "Attendere",
            SignalType.Avoid => "Evitare",
            _ => signalType.ToString()
        };
    }

    public static string FormatNullable(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.########") : "non disponibile";
    }

    private static string GetMeaning(Signal signal)
    {
        return signal.SignalType switch
        {
            SignalType.HighQualitySetup =>
                "Il bot vede piu' conferme positive insieme. Non significa comprare automaticamente, ma e' uno dei setup migliori tra quelli analizzati.",
            SignalType.BuyWatch =>
                "Il quadro e' interessante ma non perfetto. Ha senso tenerlo in osservazione e aspettare che il prezzo confermi.",
            SignalType.Watch =>
                "Ci sono alcuni segnali buoni, ma non abbastanza per considerarlo un setup forte.",
            SignalType.Wait =>
                "Il mercato non sta dando un segnale chiaro. Meglio aspettare condizioni migliori.",
            _ =>
                "Il bot vede troppi elementi contrari o rischio non interessante. Meglio non forzare operazioni."
        };
    }

    private static string GetQuickDecision(Signal signal)
    {
        var compressed = IsCompressedNearResistance(signal);
        return signal.SignalType switch
        {
            SignalType.HighQualitySetup when compressed =>
                "Setup buono, ma il prezzo e' molto vicino a una resistenza: meglio aspettare breakout o pullback.",
            SignalType.HighQualitySetup =>
                "Setup forte: valutabile solo se accetti lo stop loss e non compri in rincorsa.",
            SignalType.BuyWatch when compressed =>
                "Non entrare di fretta: prezzo compresso vicino a resistenza/supporto. Aspetta conferma.",
            SignalType.BuyWatch =>
                "Osserva: possibile occasione, ma serve conferma del prezzo prima di agire.",
            SignalType.Watch =>
                "Solo osservazione: aspetta un segnale piu' forte.",
            SignalType.Wait =>
                "Attendi: nessuna azione consigliata dal bot.",
            _ =>
                "Evita: il setup non e' abbastanza favorevole."
        };
    }

    private static string GetAction(Signal signal)
    {
        if (IsCompressedNearResistance(signal))
        {
            return "Non rincorrere il prezzo. Aspetta che rompa chiaramente la resistenza oppure che torni piu' vicino al supporto con score ancora buono.";
        }

        return signal.SignalType switch
        {
            SignalType.HighQualitySetup =>
                "Se vuoi valutarlo, fallo solo con size piccola e piano gia' deciso: ingresso vicino al prezzo indicato, stop loss rispettato, primo target su TP1.",
            SignalType.BuyWatch =>
                "Mettilo in watchlist. Valuta solo se il prezzo resta sopra l'area di ingresso e non corre gia' troppo verso TP1.",
            SignalType.Watch =>
                "Osserva e aspetta un nuovo alert piu' forte. Non c'e' urgenza.",
            SignalType.Wait =>
                "Non fare nulla per ora. Aspetta che score e contesto migliorino.",
            _ =>
                "Non entrare solo per questo segnale. Meglio cercare setup piu' puliti."
        };
    }

    private static bool IsCompressedNearResistance(Signal signal)
    {
        return signal.RuleResults.Any(rule =>
            rule.RuleName.Contains("Support/resistance", StringComparison.OrdinalIgnoreCase) &&
            rule.Details?.Contains("compressed", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string GetInvalidation(Signal signal)
    {
        if (signal.StopLoss.HasValue)
        {
            return $"Se il prezzo scende sotto lo stop loss ({FormatNullable(signal.StopLoss)}), l'idea e' invalidata.";
        }

        return "Se il prezzo perde forza o arriva un nuovo alert peggiore, l'idea va scartata.";
    }

    private static string FormatRisk(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.Low => "basso",
            RiskLevel.Medium => "medio",
            RiskLevel.High => "alto",
            _ => riskLevel.ToString()
        };
    }

    private static string FormatRuleResult(RuleResultType result)
    {
        return result switch
        {
            RuleResultType.Pass => "positivo",
            RuleResultType.Warning => "attenzione",
            RuleResultType.Fail => "negativo",
            RuleResultType.Neutral => "neutro",
            _ => result.ToString()
        };
    }

    private static string FormatPrice(decimal value)
    {
        return value.ToString("0.########");
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }
}
