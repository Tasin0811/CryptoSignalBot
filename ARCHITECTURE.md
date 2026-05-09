# Architecture

## Obiettivo

Analizzare una watchlist crypto, calcolare indicatori e contesto di mercato, generare uno score tracciabile e inviare notifiche leggibili.

## Flusso

1. `MarketDataService` scarica candele OHLCV, prezzi e dati globali.
2. `IndicatorEngine` calcola EMA, RSI, MACD, ATR, Bollinger, ADX e volume ratio.
3. `MarketContextEngine` valuta BTC, ETH/SOL, dominance e market cap globale.
4. `SignalEngine` combina regole tecniche, contesto e rischio.
5. `RiskEngine` calcola stop loss, take profit, position size teorica e risk/reward.
6. `NotificationService` invia Telegram/email.
7. `PersistenceService` salva candele, segnali, risultati regole, log e configurazioni.

## Boundary dei progetti

- `Domain`: solo modelli, enum e value object. Nessun accesso a rete, file system o database.
- `Application`: logica applicativa pura e interfacce verso infrastruttura.
- `Infrastructure`: implementazioni concrete di HTTP, SQL Server, Telegram, SMTP e logging.
- `Worker`: hosting, scheduling, configurazione e dependency injection.

## Scheduling

La V1 usa `BackgroundService` e `PeriodicTimer`. Quartz.NET puo' essere introdotto quando serviranno job multipli, calendari o retry piu' sofisticati.

## Regola prodotto

Ogni segnale deve spiegare il perche': prezzo, score, tipo segnale, rischio, stop loss, take profit, risk/reward e risultati delle regole principali.
