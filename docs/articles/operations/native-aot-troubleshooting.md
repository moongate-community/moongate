# NativeAOT Troubleshooting

Runbook operativo per problemi comuni NativeAOT in Moongate v2.

## Build and Run Commands

Build AOT (script di progetto):

```bash
./scripts/run_aot.sh --root-directory ~/moongate --uo-directory ~/uo
```

Run stress smoke:

```bash
dotnet run --project tools/Moongate.Stress -- --clients 10 --duration 15
```

## Known Failure Patterns

## 1) Reflection/DI incompatibile con AOT

Sintomo tipico:

- `MethodInfo.MakeGenericMethod() is not compatible with AOT`
- stacktrace DryIoc su conversione/risoluzione dinamica

Cause frequenti:

- risoluzione tramite reflection generica runtime
- registrazioni DI ambigue o non esplicite
- codice che usa dynamic factory selection non source-generated

Checklist:

1. evitare reflection runtime in path di bootstrap
2. preferire source generation già presente nel progetto
3. usare registrazioni DI esplicite e costruttori non ambigui
4. verificare che non ci siano service collections costruite in modo dinamico

## 2) Serializer incompatibile con AOT

Sintomo tipico:

- crash o deserialize failure durante load snapshot
- regressioni in publish AOT dopo modifiche a entity/snapshot

Decisione progetto:

- snapshot/journal su `MessagePack-CSharp` con source generation
- evitare serializer che richiedono metadati runtime non preservati

Checklist:

1. dopo cambio su entity snapshot, rigenerare/buildare
2. testare startup con snapshot esistente
3. validare roundtrip serialize/deserialize in test

## 3) Crash subito dopo login in binario AOT

Sintomo tipico:

- crash durante handshake/login o subito dopo `AccountLoginPacket`

Checklist rapida:

1. lanciare con log `Debug`
2. verificare parser/network session state
3. verificare serializer persistence init (`LoadAsync`)
4. provare con snapshot pulito per isolare corruzione dati

## 4) Errori linker/trimmer

Se usi librerie esterne con reflection:

1. aggiungere descriptor in `ILLink.Descriptors.xml`
2. preservare i tipi necessari
3. rieseguire publish AOT completo

## Pre-flight AOT Checklist

Prima di dichiarare “AOT stabile”:

1. `dotnet test`
2. publish AOT locale
3. avvio server AOT con dati reali
4. login client reale
5. smoke dei path critici:
   - movement
   - item double click script
   - gump callbacks
   - command execution
   - persistence save/load

## Practical Debug Tips

- usa stack trace completo, non solo il primo frame
- confronta sempre run JIT vs AOT sullo stesso dataset
- quando il bug è intermittente, riduci il workload (1 client, no stress)
- quando il bug è deterministic, crea test minimo prima del fix

## References

- [Installation](../getting-started/installation.md)
- [Stress Test](stress-test.md)
- [.NET NativeAOT compatibility guidance](https://aka.ms/nativeaot-compatibility)
