# Guida trading e CryptoSignalBot

Questa guida spiega i concetti base usati da CryptoSignalBot e come leggere dashboard, segnali e portfolio test.

CryptoSignalBot V1 e' un assistente decisionale: analizza dati di mercato, genera segnali, invia alert e simula un portfolio. Non compra, non vende e non esegue ordini reali.

## Regola di sicurezza

La V1 deve restare in modalita':

```text
DryRunOnly=true
```

Questo significa che il bot puo' osservare, notificare e simulare, ma non puo' fare trading reale.

I risultati del paper trading sono simulazioni. Non garantiscono profitti futuri.

## Concetti base

### Asset e coppie

Una coppia come `BTCUSDT` significa:

- `BTC`: asset osservato.
- `USDT`: valuta di riferimento.

Se il prezzo di `BTCUSDT` sale, BTC vale di piu' rispetto a USDT.

### Timeframe

Il timeframe e' la durata di ogni candela analizzata.

Esempi:

- `1h`: ogni candela rappresenta 1 ora.
- `4h`: ogni candela rappresenta 4 ore.
- `1d`: ogni candela rappresenta 1 giorno.

Timeframe piu' bassi producono piu' segnali ma anche piu' rumore. Timeframe piu' alti sono piu' lenti ma spesso piu' puliti.

### Candela

Una candela riassume il movimento del prezzo in un periodo:

- apertura: prezzo iniziale.
- massimo: prezzo piu' alto.
- minimo: prezzo piu' basso.
- chiusura: prezzo finale.
- volume: quantita' scambiata.

### Trend

Il trend indica la direzione generale.

- Trend positivo: prezzo e medie mobili indicano forza.
- Trend negativo: prezzo e medie mobili indicano debolezza.
- Trend misto: non c'e' una direzione chiara.

CryptoSignalBot usa soprattutto EMA50 ed EMA200 per capire se il trend e' sano.

### EMA50 ed EMA200

Le EMA sono medie mobili:

- EMA50: media piu' veloce, guarda il movimento recente.
- EMA200: media piu' lenta, indica il trend di fondo.

In generale:

- EMA50 sopra EMA200 e prezzo sopra EMA200: contesto migliore.
- Prezzo sotto EMA200 e EMA50 sotto EMA200: contesto debole.

### RSI

RSI misura quanto un asset e' tirato verso l'alto o verso il basso.

- RSI molto alto, ad esempio sopra 70: asset potenzialmente surriscaldato.
- RSI basso in trend positivo: possibile pullback interessante.

Il bot evita di trattare un RSI alto come acquisto automatico.

### MACD

MACD misura momentum, cioe' spinta del movimento.

Un MACD positivo puo' confermare che il prezzo non sta solo salendo per caso, ma ha spinta.

### ADX

ADX misura la forza del trend.

- ADX basso: mercato laterale o incerto.
- ADX alto: trend piu' forte.

CryptoSignalBot penalizza setup con trend debole.

### Volume

Il volume dice quanta attivita' c'e' sul mercato.

Un movimento con volume forte e' piu' credibile di un movimento con volume basso.

CryptoSignalBot usa `VolumeRatio`:

- sopra `1`: volume sopra la media recente.
- sopra `1.5`: volume forte.
- sotto `0.7`: volume debole.

### Supporto e resistenza

Supporto: zona in cui il prezzo potrebbe trovare compratori.

Resistenza: zona in cui il prezzo potrebbe trovare venditori.

Per un principiante:

- comprare vicino a una resistenza e' piu' rischioso.
- un rimbalzo vicino a supporto puo' essere piu' interessante.
- una resistenza puo' essere superata, ma serve volume forte.

### Stop loss

Lo stop loss e' il livello in cui l'idea viene considerata sbagliata.

Esempio:

```text
Entrata 100
Stop loss 95
```

Se il prezzo scende a 95, la simulazione chiude in perdita controllata.

Lo stop loss non serve a prevedere il futuro. Serve a limitare il danno quando il mercato va contro l'idea.

### Take profit

Il take profit e' un obiettivo di uscita in guadagno.

CryptoSignalBot usa due livelli:

- TP1: primo obiettivo, vende una parte della posizione simulata.
- TP2: secondo obiettivo, prova a chiudere il resto se il movimento continua.

### Break-even

Break-even significa proteggere il trade dopo un primo guadagno.

Nella simulazione:

1. Il prezzo raggiunge TP1.
2. Il bot vende una parte della posizione.
3. Il resto viene protetto vicino al prezzo di ingresso.

In questo modo, se il prezzo torna indietro, il trade cerca di non trasformare un buon movimento in una perdita pesante.

### Fee

Le fee sono commissioni simulate.

Esempio:

```text
Fee paper per lato = 0.001
```

Significa 0,1% in ingresso e 0,1% in uscita.

### Slippage

Lo slippage e' un piccolo peggioramento simulato del prezzo.

Serve per evitare simulazioni troppo perfette. Nel mercato reale spesso non si compra o vende esattamente al prezzo teorico.

### Risk percent

`Rischio per trade` indica quanta parte del capitale teorico si accetta di rischiare se il trade va a stop loss.

Esempi:

- `0.005`: 0,5% per trade.
- `0.01`: 1% per trade.

Per V1 e test principianti, 0,5%-1% e' gia' abbastanza.

## Tipi di segnale

CryptoSignalBot non genera un semplice `BUY`. La V1 resta prudente.

| Segnale | Significato |
| --- | --- |
| `AVOID` | Evita. Contesto o rischio sfavorevole. |
| `WAIT` | Aspetta. Mancano conferme. |
| `WATCH` | Osserva. Setup interessante ma non abbastanza forte. |
| `BUY WATCH` | Possibile acquisto da valutare manualmente. |
| `HIGH QUALITY SETUP` | Setup forte, comunque da controllare manualmente. |

Nessun segnale e' un ordine o una garanzia.

## Score

Lo score va da 0 a 10.

Il bot somma regole positive e negative:

- trend
- momentum
- volume
- supporto/resistenza
- contesto BTC/mercato
- rischio/rendimento
- qualita' ingresso

La soglia alert default e':

```text
MinScoreToNotify = 7.5
```

Quindi il bot notifica solo setup sopra una certa qualita'.

## Filtro BTC e mercato

Molte altcoin dipendono dal contesto di BTC.

CryptoSignalBot penalizza o blocca setup se:

- BTC e' in calo forte.
- BTC e' sotto medie importanti.
- il mercato crypto globale e' in risk-off.
- BTC dominance e' alta e sfavorevole alle altcoin.

Questo riduce il numero di segnali, ma evita molti falsi positivi.

## Portfolio test

Il portfolio test simula un wallet teorico.

Esempio:

```text
Budget test portfolio = 500
```

Significa: il replay parte da 500 finti e prova a seguire i segnali salvati nel database.

### Non riparte da capo a ogni trade

Il wallet usa un budget unico nel replay.

Esempio:

1. parte da 500.
2. apre un trade.
3. chiude in profitto.
4. il cash sale.
5. il trade successivo usa il cash aggiornato.

La colonna `Cash wallet` mostra proprio il saldo prima e dopo ogni trade.

### Attenzione: e' un replay

Il portfolio test e' un replay storico ricalcolato dai segnali salvati.

Significa:

- non si resetta quando riavvii Docker.
- non si resetta quando riavvii la VPS.
- cambia se entrano nuovi segnali.
- cambia se cancelli segnali vecchi con cleanup o retention.

I dati sono salvati nel volume Docker. Non usare:

```bash
docker compose down -v
```

Quel comando cancella anche i volumi, quindi puo' eliminare database e storico.

## Metriche portfolio

### Budget iniziale

Capitale finto da cui parte il replay.

### Cash disponibile

Soldi finti liberi, non investiti.

### Valore posizioni

Valore delle posizioni ancora aperte.

### Equity simulata

Valore totale del wallet:

```text
cash disponibile + valore posizioni aperte
```

### Guadagno/perdita

Differenza tra equity simulata e budget iniziale.

### P/L realizzato

Profitto o perdita dei trade gia' chiusi.

### P/L aperto

Profitto o perdita delle posizioni ancora aperte.

Non e' definitivo finche' il trade non chiude.

### Win rate

Percentuale dei trade chiusi in profitto.

Attenzione: con pochi trade e' poco utile. Un win rate del 100% su 1 trade non significa nulla.

### Profit factor

Rapporto tra profitti lordi e perdite lorde.

In generale:

- sotto 1: strategia negativa sui trade chiusi.
- intorno a 1: quasi pari.
- sopra 1: profitti maggiori delle perdite.

Va letto solo dopo abbastanza trade.

### Expectancy

Profitto o perdita media attesa per trade chiuso.

Esempio:

```text
Expectancy = +1.20
```

Vuol dire che, in media, ogni trade chiuso ha prodotto +1.20 nel replay.

### Drawdown massimo

La peggior discesa dal massimo precedente del wallet.

E' una metrica fondamentale per capire quanto il sistema soffre prima di recuperare.

### Miglior trade / peggior trade

Mostra il trade simulato migliore e quello peggiore.

Serve a capire se il risultato e' stabile o dipende da un solo trade fortunato.

## Grafico equity

Il grafico equity mostra l'andamento del wallet simulato.

Se la linea sale in modo graduale, il replay e' piu' sano.

Se la linea sale ma poi scende molto, controllare il drawdown.

Se la linea resta piatta, probabilmente ci sono pochi segnali o pochi trade chiusi.

## Ogni quanto entra

Su Hetzner lo scheduler gira circa ogni 15 minuti.

Questo non significa che il bot entra ogni 15 minuti.

Il bot simula un ingresso solo quando:

- trova un segnale sopra soglia.
- il setup ha stop loss e take profit validi.
- il contesto non e' bloccato.
- il wallet replay ha cash disponibile.
- non c'e' gia' una posizione aperta che blocca il replay prudente.

Quindi puo' passare molto tempo senza nessun trade. Questo e' normale.

## Ogni quanto esce

Un trade simulato esce quando succede uno di questi casi:

1. Stop loss: prezzo va contro il setup.
2. TP1: prezzo raggiunge primo obiettivo e vende una parte.
3. TP2: prezzo raggiunge secondo obiettivo e chiude il resto.
4. Break-even: dopo TP1, il prezzo torna vicino all'entrata e chiude il resto protetto.
5. Scadenza: non prende ne' stop ne' target entro la finestra testata.

La dashboard usa una finestra di future candles. Con `maxFutureCandles=24`:

- su `1h`: guarda circa 24 ore.
- su `4h`: guarda circa 4 giorni.
- su `1d`: guarda circa 24 giorni.

## Impostazioni consigliate per V1 Hetzner

Configurazione prudente:

```text
DryRunOnly=true
PaperPortfolioInitialBudget=500
RiskPercent=0.005
PaperTradingFeePercent=0.001
PaperTradingSlippagePercent=0.001
PaperTradingTakeProfit1ExitPercent=0.5
MinScoreToNotify=7.5
```

Configurazione un po' piu' attiva:

```text
RiskPercent=0.01
```

Non usare leva. Non fare all-in. Non disattivare `DryRunOnly` in V1.

## Come valutare i risultati

Non giudicare il bot da 1-2 trade.

Meglio aspettare:

- almeno 3-7 giorni per verificare stabilita' tecnica.
- almeno 2-4 settimane per giudicare la qualita' dei segnali.
- idealmente qualche decina di trade simulati.

Metriche da guardare:

1. Profit factor.
2. Expectancy.
3. Drawdown massimo.
4. Numero trade chiusi.
5. Win rate.
6. P/L dopo fee e slippage.

Il win rate da solo non basta.

Un sistema puo' vincere spesso ma perdere troppo quando sbaglia.

## Cosa fa l'app

CryptoSignalBot:

- scarica dati Binance pubblici.
- legge contesto globale CoinGecko.
- calcola indicatori tecnici.
- genera score e segnali.
- invia alert Telegram/email se configurati.
- salva segnali e candele nel database.
- simula un portfolio paper.
- mostra dashboard locale/remota.
- esporta segnali CSV.
- fa cleanup dati secondo retention.

## Cosa non fa l'app

CryptoSignalBot V1 non:

- esegue ordini reali.
- usa leva.
- garantisce profitti.
- sostituisce una decisione umana.
- salva segreti nel repository.
- protegge da eventi improvvisi di mercato.

## Comandi Hetzner utili

Entrare sul server:

```bash
ssh root@IP_SERVER
```

Aggiornare dopo un nuovo push:

```bash
cd /opt/cryptosignalbot
git pull
docker compose down
docker compose build --no-cache
docker compose up -d
```

Controllare stato:

```bash
docker compose ps
curl http://localhost:5055/health
```

Log scheduler:

```bash
docker compose logs -f scheduler
```

Forzare report:

```bash
docker compose exec dashboard dotnet /app/worker/CryptoSignalBot.Worker.dll --report-watchlist --force-report --send-empty-report
```

Non cancellare database/log:

```bash
docker compose down -v
```

Usarlo solo se vuoi azzerare tutto.

## Titolo commit consigliato

```text
Add beginner trading and app guide
```
