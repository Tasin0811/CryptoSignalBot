# Signal Rules

Nessun indicatore genera un `BUY` da solo. Ogni regola contribuisce allo score finale e deve essere salvata come `SignalRuleResult`.

## Formula

```text
FinalScore = TrendScore
           + MomentumScore
           + VolumeScore
           + SupportResistanceScore
           + MarketContextScore
           + RiskRewardScore
           - RiskPenalty
```

## Output

| Score | Tipo | Azione |
| --- | --- | --- |
| 0 - 3.9 | AVOID | Non comprare. Contesto o rischio sfavorevole. |
| 4.0 - 5.9 | WAIT | Osservare. Mancano conferme. |
| 6.0 - 7.4 | WATCH | Setup interessante ma non forte. |
| 7.5 - 8.4 | BUY WATCH | Alert con stop e size controllata. |
| 8.5 - 10 | HIGH QUALITY SETUP | Setup forte, comunque con controllo manuale in V1. |

## Regole principali

- Trend positivo: `Close > EMA200` e `EMA50 > EMA200`.
- Trend negativo: `Close < EMA200` e `EMA50 < EMA200`.
- RSI interessante: `RSI` tra 28 e 40 in trend positivo.
- RSI ipercomprato: `RSI > 70`.
- MACD: sopra signal e istogramma crescente.
- ADX: trend piu' affidabile sopra 20/25, laterale sotto 15.
- Volume: candela sopra media 20, bonus forte sopra 150%.
- Supporto/resistenza: favorire rimbalzi su supporti, penalizzare resistenze vicine.
- ATR: penalizzare volatilita' eccessiva.
- BTC filter: bloccare o penalizzare altcoin se BTC e' sotto EMA200 o in dump intraday.
- Risk/reward: favorire setup con `TP1/SL >= 1.5` e `TP2/SL >= 2`.

## Vincoli V1

- Solo alert, nessun ordine reale.
- Nessuna leva.
- Nessun all-in.
- Rischio teorico massimo 0.5%-1% del capitale per segnale.
- Segreti sempre fuori dal codice.
