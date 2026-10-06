<!-- translation: {"sourceHash":"6c89064729bd5855256909537907f6a19207130fb287c3ed0dd18b329354cb90","title":"account"} -->

# account

Crea un account o concede a un amministratore l'accesso all'API di amministrazione.

| Sintassi | Console | In gioco | Livello minimo | Ruolo |
| --- | --- | --- | --- | --- |
| `account create <username> <password> [level]` / `account api-access <username> <on\|off>` | Sì | Sì | Administrator | Login |

```text
account create <username> <password> [Regular|GameMaster|Administrator]
```

Il livello predefinito dell'account è `Regular`. Il comando attende
`IAccountService.CreateAccountAsync` e segnala successo, nome utente esistente o
un errore senza stampare la password. La console interattiva maschera il token della
password durante la digitazione e non include la riga di comando grezza nel log
degli errori. L'account viene memorizzato nel database PostgreSQL Accounts condiviso.
I processi solo game non registrano questo comando né ricevono le credenziali Accounts.

Gli amministratori in gioco possono digitare `.account create ...`. Il server non
ripete né trasmette l'input e non lo scrive nei log. Il client UO potrebbe conservare
il comando digitato nella propria cronologia locale.

I plugin possono aggiungere comandi tramite `RegisterCommand<TExecutor>`; vedi
[Scrivere un plugin](../plugins.md#console-commands).

Ogni testo mostrato da un comando ai giocatori, la sua descrizione in `help` e le
risposte del dispatcher (comando sconosciuto, non disponibile qui, non consentito,
fallito) provengono dai file dei messaggi nella lingua del server (`ILocalizationService`,
id 30008–30049; vedi [Localizzazione](../localization.md#moongates-own-messages)).
Sintassi dei comandi, tipi di account, sorgenti e nomi delle mappe restano nomi tecnici,
come accettati dai comandi. In un processo solo login, privo di file dei messaggi,
i testi sono inglesi. L'output console riservato agli operatori (`account api-access`,
`script`) rimane inglese.

### Abilitazione locale dell'accesso API

```text
account api-access <username> <on|off>
```

Disponibile solo nella console locale Login/Standalone, anche quando il chiamante è
un Administrator in gioco. Gli account creati con `account create` partono con
accesso API disabilitato. Abilita un Administrator esistente per configurare il primo
utente del pannello; disabilitare l'accesso revoca le sue sessioni amministrative su
tutti gli host. Il login di gioco non è influenzato. Vedi [API di amministrazione](../admin-api.md).

## Vedi anche

- [Tutti i comandi](../commands.md)
