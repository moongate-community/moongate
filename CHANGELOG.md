# Changelog

## [0.8.0](https://github.com/moongate-community/moongate/compare/v0.7.0...v0.8.0) (2026-09-26)


### Features

* **boot:** ship the shard data files and copy them into the root ([f7a7948](https://github.com/moongate-community/moongate/commit/f7a7948c468744101677eaaf2904bbb05f2b207a)), closes [#111](https://github.com/moongate-community/moongate/issues/111)
* **config:** add the line_of_sight section with max_distance ([0e22e83](https://github.com/moongate-community/moongate/commit/0e22e83caf1faedc9af6902f654713e80db4d740))
* **core:** write and read every enum in TOML by name ([b69527b](https://github.com/moongate-community/moongate/commit/b69527bbd0f7b5dde8f816f3059b0aa96b3b2701))
* **data:** add offensive words to banned_names.toml ([4251e44](https://github.com/moongate-community/moongate/commit/4251e44bdd7edc5747e357a6b3ba611c9ad0d3ec))
* **data:** add teleport rules to regions ([09a2094](https://github.com/moongate-community/moongate/commit/09a20946238e77ae9e8ba266038b212e4758f5ff))
* **data:** add travel zones as regions ([17fd752](https://github.com/moongate-community/moongate/commit/17fd752f41641a06bdae96741007f7de3297ce5e))
* **data:** add weather profiles and give every region a weather ([a6c6e5f](https://github.com/moongate-community/moongate/commit/a6c6e5f36fdaadc271a6dce3997270b00a7611c2))
* **data:** name the entries of containers.toml ([8572437](https://github.com/moongate-community/moongate/commit/8572437d9a253e2f68209797ab39f6591c18c8c5))
* **localization:** add server language with UOX3 message files ([3e3a452](https://github.com/moongate-community/moongate/commit/3e3a452a6efb88bd684332891c788d1a317d4bbc))
* **network:** add ClientVersion and carry it from login to the game session ([6469217](https://github.com/moongate-community/moongate/commit/6469217351db2da0b5967ae28a53565c84d6cb79))
* **persistence:** support custom values and collections in JSONB ([d5a033a](https://github.com/moongate-community/moongate/commit/d5a033a6599b9fc5f0fe5e9794cd24ca8de57b89))
* **persistence:** support custom values and collections in JSONB ([7ccdccf](https://github.com/moongate-community/moongate/commit/7ccdccf30ee0396396f8aa58eb9801c5b732bf5f)), closes [#108](https://github.com/moongate-community/moongate/issues/108)
* **scripting:** add the localization Lua module ([4bd6609](https://github.com/moongate-community/moongate/commit/4bd6609b3a90451037155d2cf1eb939f798581ca))
* **server:** let plugins register incoming packets and move Ultima packets to the plugin ([dcbfeef](https://github.com/moongate-community/moongate/commit/dcbfeef7a62991995b6db421b1e235654b88395a))
* **server:** refuse to start when the root is the directory holding the binary ([fa8f767](https://github.com/moongate-community/moongate/commit/fa8f76712cc7154d6b872525edce34c674c696c7)), closes [#52](https://github.com/moongate-community/moongate/issues/52)
* **server:** send starting cities and character list after game login ([e12a5b8](https://github.com/moongate-community/moongate/commit/e12a5b8f76dc335121ee5449fd80ab8f61072a12))
* **templates:** add visibility to item templates ([fc38295](https://github.com/moongate-community/moongate/commit/fc38295e43d1d272c451921e1e2508513007866c))
* **toml:** add Point2D and Point3D converters ([e39f8ff](https://github.com/moongate-community/moongate/commit/e39f8ff63f399ce742f67508cc12dc1eacad0b70))
* **toml:** use corner ranges for rectangle bounds ([b057fcd](https://github.com/moongate-community/moongate/commit/b057fcda25b27a4ef9d44d45284a2feeb62fc62d))
* **ultima:** add character creation rules and client flags ([e911325](https://github.com/moongate-community/moongate/commit/e911325d1d2d6f479d6017595dc5e90f0633bdd1))
* **ultima:** add HueSpec and load races with their allowed hair and beard styles ([22e515f](https://github.com/moongate-community/moongate/commit/22e515f1d014357203f2b424377d695bc207c3cc))
* **ultima:** add ILineOfSightService with POL's line walk and ModernUO's rules ([78cc30f](https://github.com/moongate-community/moongate/commit/78cc30f88014cfe4d6e9ce626d72f913cec9ead6))
* **ultima:** add IMapService to read map terrain and statics ([f020250](https://github.com/moongate-community/moongate/commit/f020250913fdd76e4ce3758ff805855c972eba19))
* **ultima:** add IMovementService with the average terrain height ([0fa15f0](https://github.com/moongate-community/moongate/commit/0fa15f025eea959088916c182c00fa4c5739c5d9))
* **ultima:** add IMultiService to read multi layouts ([4313e65](https://github.com/moongate-community/moongate/commit/4313e653f644731af5a361f3c60a9a81bd391710))
* **ultima:** add ITileDataService to read land and item tiles ([798bf5e](https://github.com/moongate-community/moongate/commit/798bf5ed1297b20d005e47f153bfad10e56c84f8))
* **ultima:** add the classic client create character packet (0xF8) ([9adf2d5](https://github.com/moongate-community/moongate/commit/9adf2d5a442b8bfa8a2b7ec44f6f6b19b1311398))
* **ultima:** check a straight step against terrain and statics ([c0f861e](https://github.com/moongate-community/moongate/commit/c0f861ec68d492c800271187acf8e4513ed627be))
* **ultima:** check diagonal steps and register IMovementService ([59dab92](https://github.com/moongate-community/moongate/commit/59dab92de247394af9a70e85e57632fb818b2a9e))
* **ultima:** load banned name words from banned_names.toml ([4366754](https://github.com/moongate-community/moongate/commit/436675463a938c520e306b30e6065aded2db80fd))
* **ultima:** load body kinds from bodies.toml ([2594fdd](https://github.com/moongate-community/moongate/commit/2594fdd175250c3dac2cc4f62dc990b9d6c0e75a))
* **ultima:** load container gumps and add the music track ids ([c783e08](https://github.com/moongate-community/moongate/commit/c783e08639e9a55a1fbf65952e12c0076631b155))
* **ultima:** load regions from one file per map with explicit rules ([4efdd93](https://github.com/moongate-community/moongate/commit/4efdd93c4aea817a9cf47637e501c41ecea5e4ec))
* **ultima:** load skills and professions from data files ([56dca80](https://github.com/moongate-community/moongate/commit/56dca8091fba6b8aa440a32b9427019c8259a184))
* **ultima:** load tiledata.mul when the server starts ([3fa699c](https://github.com/moongate-community/moongate/commit/3fa699c36fa8081a1ebd15687c9dec813bf863c8))
* **ultima:** store character skills as a JSONB list ([c7753cb](https://github.com/moongate-community/moongate/commit/c7753cbce66cc5c49a78d991040959503de1c69e))
* **ultima:** store position, appearance and stats on CharacterEntity ([f3ab926](https://github.com/moongate-community/moongate/commit/f3ab926eef4696dd14501bf744ebca85ed156384))


### Bug Fixes

* **admin:** ensure the certificates directory exists ([7cb69d8](https://github.com/moongate-community/moongate/commit/7cb69d83926bf8bad934c655eb77072f6886260a))
* **core:** make the TOML converter registry safe under concurrent use ([2bb430a](https://github.com/moongate-community/moongate/commit/2bb430ac146a440f8b2e859dbcd3c65a2991bdab))
* **core:** read EnumValueSpec member names as it writes them, never numbers ([638dd3a](https://github.com/moongate-community/moongate/commit/638dd3ac031f90b38ed63c7f714093a1b11a7de9))
* **data:** list each body once in bodies.toml ([d50aba0](https://github.com/moongate-community/moongate/commit/d50aba0ab5c01c14625fbd1908c8dcb568c7e221))
* **data:** validate starting cities when they load ([13d038e](https://github.com/moongate-community/moongate/commit/13d038e5c44224980d2334920647f6499cc7b3e7))
* **persistence:** keep DateTime columns in UTC ([e5668ca](https://github.com/moongate-community/moongate/commit/e5668cab2c85006d5c70e758904734acebc5c408))
* **scripting:** refuse [ScriptFunction] on static, non-public or generic methods ([478bcdc](https://github.com/moongate-community/moongate/commit/478bcdc51e2ff91870783255adaa62221fc54e8e)), closes [#36](https://github.com/moongate-community/moongate/issues/36)
* **ultima:** check the map bounds first and walk statics without allocating ([48705c5](https://github.com/moongate-community/moongate/commit/48705c5dd822f7f05b894d41181f01d9b790ba2e))
* **ultima:** read only the low three bits of a movement direction ([51b7b8e](https://github.com/moongate-community/moongate/commit/51b7b8e54555efdf1b77233edd3445d1b211bc1c))


### Performance Improvements

* **docker:** streamline publication and reuse build cache ([330b449](https://github.com/moongate-community/moongate/commit/330b449c9cdbee49f3c9813f27f98129fc67935c))
* **docker:** streamline publication and reuse build cache ([979e1c4](https://github.com/moongate-community/moongate/commit/979e1c45aee4a38c5ab61e2e1026ac24a899b1cf))
* **tests:** add fast suite and consolidate database scenarios ([b791048](https://github.com/moongate-community/moongate/commit/b7910481ea37e092c42a1a0c6a74bd7ebf5b3a83))

## [0.7.0](https://github.com/moongate-community/moongate/compare/v0.6.0...v0.7.0) (2026-09-24)

### Features

* **login and realms:** discover game servers through expiring Redis leases, filter the realm list by account level, and transfer authenticated clients with one-use `0x8C`/`0x91` handoff tickets.
* **server:** run Login, Game or Standalone roles with separate login and game TCP listeners and role-local PostgreSQL databases.
* **administration:** embed an optional gRPC plugin on port 2590 with server TLS, shared Redis sessions, bounded login throttling, account listing/creation, session revocation and server information. Ship portable protobuf contracts and C#/Python client examples.
* **accounts and commands:** add account persistence, account listing, console account creation and API-access provisioning, plus command discovery through `help`. Administrative API access is disabled for new accounts unless explicitly granted.
* **boot:** ship `mgboot` to prepare a root directory, default configuration and bundled migrations offline. Use `--generate-admin-certificate` to create or reuse a server TLS identity and enable the administration API without starting the server.
* **packets:** register asynchronous packet handlers, perform asynchronous work outside the GameLoop and return game-state changes to its owning thread.
* **persistence:** assign persistent serials automatically, map convention-based columns to snake_case, and generate reviewable development migrations at startup. Publish persistence readiness and shutdown events.
* **events:** subscribe to all published events through `SubscribeAll`.
* **templates and tools:** add item/loot template contracts, typed value specifications, data-loader registration and UOX3 conversion tooling with validated references and snake_case identifiers.
* **deployment:** document one Login and two independent Game processes with private Redis, PostgreSQL, Compose secrets and administration TLS.

### Fixes

* Preserve delivered redirect tickets when closing login connections; handle cancellation and terminal packet replies safely.
* Fence realm lease recovery, make heartbeat updates atomic and skip malformed directory entries.
* Coordinate administrative account changes with session revocation, classify dependency failures and complete operation audits.
* Preserve renamed persistence columns, detect default changes and allow indexes on newly created tables.
* Serialize PostgreSQL integration test projects to avoid contention in CI, and verify generated administration clients across languages.
* Normalize nested directory paths and improve item/loot conversion output and validation.

### Architecture and operation

* Redis provides shared realm coordination and temporary login/session state in all runtime roles, including Standalone. PostgreSQL remains the durable store, scoped to Accounts on Login and Realm on Game.
* The internal TCP API and `Moongate.Api` package are retired. Runtime peers coordinate through Redis; the optional administration endpoint uses standard gRPC with server TLS.
* Character selection, live-world editing and a playable world remain outside the implemented feature set. See the implementation-status guide for current limits.

## [0.6.0](https://github.com/moongate-community/moongate/compare/v0.5.0...v0.6.0) (2026-09-21)


### Features

* add versioned SQL migration catalog and history validation ([db87b4a](https://github.com/moongate-community/moongate/commit/db87b4a29bbe850fcbf0c7b136bccf16f8ea50ad))
* apply PostgreSQL migrations with isolated DbUp runner ([6162c53](https://github.com/moongate-community/moongate/commit/6162c532aad8078da4f695f19ece490e846ad709))
* **persistence:** simplify database config and entity registration ([a7e9971](https://github.com/moongate-community/moongate/commit/a7e99712455b3c612c7842ce0eaabb0d8b47de3a))
* ship and document versioned database migration workflow ([bd72582](https://github.com/moongate-community/moongate/commit/bd725827924be360486a7cde0d7ae33e13244aa0))
* validate migrations at startup and generate SQL drafts ([c22019e](https://github.com/moongate-community/moongate/commit/c22019e5cfd326216cd97d4230af24eb731fe178))


### Bug Fixes

* generate third-party notices for migration runner ([74530a2](https://github.com/moongate-community/moongate/commit/74530a2b9949496566fb6f44116d47712db4090a))
* preserve migration atomicity and align CLI defaults ([c30636f](https://github.com/moongate-community/moongate/commit/c30636f3935c6f1d2950ba2ca58805da6954a7fd))


### Miscellaneous Chores

* release 0.6.0 ([70dbb4d](https://github.com/moongate-community/moongate/commit/70dbb4d1754b33a8b92700b6111d0efdeaa003de))

## [0.5.0](https://github.com/moongate-community/moongate/compare/v0.4.1...v0.5.0) (2026-09-20)


### ⚠ BREAKING CHANGES

* **persistence:** replace binary storage with async PostgreSQL facades

### Features

* **api:** provision certificates and simplify peer permissions ([45ebc45](https://github.com/moongate-community/moongate/commit/45ebc452cdeb0f35b1a81276ce619cad8308a0e1))
* **api:** provision missing certificates on explicit opt-in ([f913862](https://github.com/moongate-community/moongate/commit/f9138621c57f68b49684f34bea815552c98cfc59))
* **api:** support wildcard operation permissions for trusted peers ([9366017](https://github.com/moongate-community/moongate/commit/936601771f93a9ed097e36873a46484f1001285d))
* **docker:** add login and two-realm Compose example ([e2708cb](https://github.com/moongate-community/moongate/commit/e2708cb99c07e3534f61226ede806e623b782e32))
* **docker:** add login and two-realm Compose example ([4011906](https://github.com/moongate-community/moongate/commit/4011906913644e08cddea00bc123d23097e71673))
* **persistence:** add PostgreSQL schema foundation ([cfc37a2](https://github.com/moongate-community/moongate/commit/cfc37a2124440212fef85ab3c6c7722f74596f4b))
* **persistence:** replace binary storage with async PostgreSQL facades ([412e3b5](https://github.com/moongate-community/moongate/commit/412e3b5a1b9477be828a5b608976e15bc87eb0ef))
* **server:** integrate PostgreSQL persistence lifecycle and schema commands ([ac12088](https://github.com/moongate-community/moongate/commit/ac12088eb18258cfb4c26be19af6c15a3bf141de))


### Bug Fixes

* **api:** allow concurrent public certificate replacement on Windows ([a2c91b5](https://github.com/moongate-community/moongate/commit/a2c91b53d12c78a0fd53199b5edefd1d7216369e))
* **docker:** preserve PostgreSQL secret values ([3aa5493](https://github.com/moongate-community/moongate/commit/3aa5493b6295708b1c41b2f179f0e434e7ed7787))
* **persistence:** drain admitted captures before releasing save coordination ([a17ceb8](https://github.com/moongate-community/moongate/commit/a17ceb8abd2259aaf7e3bdbe3a8aa1052f3d96e2))
* **persistence:** isolate FreeSql entity mappings ([9b8a04d](https://github.com/moongate-community/moongate/commit/9b8a04d7cdb4038cd789fdb6a7296f5ca951856f))
* **persistence:** reject silently unmapped writable properties ([b6c47c7](https://github.com/moongate-community/moongate/commit/b6c47c722f09373dee0d0804908e27e751f89790))
* **server:** retain all terminal capture failures during shutdown ([5300502](https://github.com/moongate-community/moongate/commit/5300502afcf7941a00e9eebaf553421a1b99374d))


### Miscellaneous Chores

* release 0.5.0 ([edc42a6](https://github.com/moongate-community/moongate/commit/edc42a606f57e5b0b414e2dd83e5c01aa58d1802))

## [0.4.1](https://github.com/moongate-community/moongate/compare/v0.4.0...v0.4.1) (2026-09-20)


### Features

* **api:** add opt-in server API configuration ([ce4423c](https://github.com/moongate-community/moongate/commit/ce4423c6856a39ee0c6f64901a4cf0c603cf3190))
* **api:** host the configured internal API server ([1bab556](https://github.com/moongate-community/moongate/commit/1bab5569769b27bb7faeb17b4e1f3adf4cc40a01))
* **api:** start configured API listener with the server lifecycle ([6672fa6](https://github.com/moongate-community/moongate/commit/6672fa6abcbc75da4c7aa0c5705ac9c2321e149c))
* **docs:** add Starlight documentation and release publication ([14987bd](https://github.com/moongate-community/moongate/commit/14987bdbda59eba9e570f44c8fa50719d4d32d0d))
* **docs:** add the Starlight documentation website ([3b903d5](https://github.com/moongate-community/moongate/commit/3b903d5352e069bde336030cf355219193aec5ee))
* **docs:** import existing guides and library readmes ([b44dceb](https://github.com/moongate-community/moongate/commit/b44dceb0195505d19604f58771cabbc473a03ed9))
* **network:** restore standalone span serialization utilities ([86bc49f](https://github.com/moongate-community/moongate/commit/86bc49f5939b34e2617edc95d771ddcc1d28acac))
* **network:** restore standalone span serialization utilities ([14221a7](https://github.com/moongate-community/moongate/commit/14221a734e19a6b1e7fd62c42474748db876e89d))
* **samples:** add a plugin that registers a Lua module, a command and a metric provider ([34b3e24](https://github.com/moongate-community/moongate/commit/34b3e248a5347174c00f806c624ab6cce7c4873e))
* **scripts:** install the released server with one command on Linux ([a271384](https://github.com/moongate-community/moongate/commit/a27138496b3ee2bd81c53855774fad8ca3be82f0))


### Bug Fixes

* **api:** reject unusable server certificates before startup ([dd9b648](https://github.com/moongate-community/moongate/commit/dd9b6489afc098956bae136f7b6f406582d882fd))
* **docs:** preserve imported anchors and responsive images ([9f4f7cd](https://github.com/moongate-community/moongate/commit/9f4f7cdbad9fac922965f4b485bdd9f3a8259b53))
* **docs:** serve documentation from the configured custom domain ([a519cb5](https://github.com/moongate-community/moongate/commit/a519cb5b5fa32fe2bc7cf02280fd55ef9f4c640d))
* **samples:** reject undefined tones and keep host assemblies out of the bundle ([20952f9](https://github.com/moongate-community/moongate/commit/20952f9504c7a4e9a830a5cacfa4fc8b342d3c21))
* **samples:** report the metric under its local name so the diagnostics service accepts it ([b248c25](https://github.com/moongate-community/moongate/commit/b248c2530fbba142e2ee6709bab0910b762b35e9))
* **scripts:** keep the previous installation when the swap fails ([45491fd](https://github.com/moongate-community/moongate/commit/45491fdaa3c5b24076a941e03b24739518895c05))
* **scripts:** tell the truth when an install fails after the files are in place ([ee6cded](https://github.com/moongate-community/moongate/commit/ee6cdedf426331aa43cf607e423f8d15e4a6335c))


### Miscellaneous Chores

* release 0.4.1 ([0341165](https://github.com/moongate-community/moongate/commit/03411653aab9a971b2b2aecd9471f84c61d59ce7))

## [0.4.0](https://github.com/moongate-community/moongate/compare/v0.3.0...v0.4.0) (2026-09-19)


### ⚠ BREAKING CHANGES

* **packets:** NetworkSession accepts and exposes INetworkConnection; ISessionService.GetOrCreate now accepts INetworkConnection. Recompile Server.Core consumers and plugins.

### Features

* **api:** add a script that generates the private CA and leaf certificates ([6ac4ccf](https://github.com/moongate-community/moongate/commit/6ac4ccfb9bcc442df31bea18741fd13e78920a55))
* **api:** add authenticated internal request/reply channels ([f5de84f](https://github.com/moongate-community/moongate/commit/f5de84fb01d96ce98a17ca9634d67966a5dded66))
* **api:** add versioned MessagePack framing ([85b51ab](https://github.com/moongate-community/moongate/commit/85b51ab35fc6d47385d02ec9d749b55d64630755))
* **api:** authenticate peers with mutual TLS and local policies ([155caed](https://github.com/moongate-community/moongate/commit/155caeda59d9a2f662f4653bb5832749fc824d22))
* **api:** bound pending calls and outgoing work ([0079e14](https://github.com/moongate-community/moongate/commit/0079e145c45c46100c9c927df63e322c212c531b))
* **api:** dispatch authorized requests with bounded execution ([54b4e40](https://github.com/moongate-community/moongate/commit/54b4e405b6fc4ce1c425b34040bb00cc015ece3b))
* **api:** expose authenticated clients and servers with owned lifecycle ([79ec6a2](https://github.com/moongate-community/moongate/commit/79ec6a2c2acc99e51babe0b174f8659ee34b05e5))
* **api:** merge internal request/reply channel into develop ([f5de84f](https://github.com/moongate-community/moongate/commit/f5de84fb01d96ce98a17ca9634d67966a5dded66))
* **api:** register typed operations and freeze handler metadata ([d3d2512](https://github.com/moongate-community/moongate/commit/d3d2512b6ff3fac3bf529c7f9228e889cafdf042))
* **diagnostics:** add AddMetricProvider container extension ([97a9498](https://github.com/moongate-community/moongate/commit/97a949850a11be2e0ceb3861c80439ab2501874f))
* **network:** own bounded asynchronous connection setup ([96583da](https://github.com/moongate-community/moongate/commit/96583da50568b53b2a6a53074b37c949ec1bcb06))
* **network:** support configured outbound stream preparation ([ddca33f](https://github.com/moongate-community/moongate/commit/ddca33fad957604ba804d31e60b6d84bd4fc86ea))
* **network:** track role-local connections and owned cleanup ([95e9824](https://github.com/moongate-community/moongate/commit/95e98243a1e8ebc69afe7db4cbaa5aae152b87b7))
* **persistence:** add the AddPersistenceEntity registration ([f5a87e2](https://github.com/moongate-community/moongate/commit/f5a87e2d1f0412c0197d87e9b0433784a0d6d7ae))
* **persistence:** let an entity declare its own collection ([62fe55d](https://github.com/moongate-community/moongate/commit/62fe55d7ba3602e355e3be6c8b802392bd0c2185))
* **scripting:** add module attributes, engine contracts and registration ([3c220c0](https://github.com/moongate-community/moongate/commit/3c220c0d3c80658915732c0e5e9574943b46af51))
* **scripting:** add the engine, log and timer modules ([1254614](https://github.com/moongate-community/moongate/commit/1254614aad63ced9d28ef1bf8a66ec114102d725))
* **scripting:** add the Lua script engine service ([00b07e8](https://github.com/moongate-community/moongate/commit/00b07e871a780e0841d645df7874bd24b3c57d28))
* **scripting:** add the Moongate.Scripting project on LuaCSharp 0.5.6 ([1e5a126](https://github.com/moongate-community/moongate/commit/1e5a126f8233eee7cfd8a6d0618f245c6e1b4695))
* **scripting:** bind module functions to Lua by attribute ([1c10d51](https://github.com/moongate-community/moongate/commit/1c10d514b4c3ed651661d996e79c6427cd0fb00a))
* **scripting:** cap the strings string.rep may build ([ec34afc](https://github.com/moongate-community/moongate/commit/ec34afcce9a556089a84a8427162be081ff474aa))
* **scripting:** expose module constants and enums to Lua ([f58328f](https://github.com/moongate-community/moongate/commit/f58328ffa9fd2dfeaa7cc8f6b7f9382e52d383f1))
* **scripting:** generate editor definitions from the bound modules ([6c47fa4](https://github.com/moongate-community/moongate/commit/6c47fa49e58780638f4db8f5515c946d5394c266))
* **scripting:** guard the loop thread and drive LuaCSharp synchronously ([97f4c88](https://github.com/moongate-community/moongate/commit/97f4c88f765f8fdc5b3d2d0cb852d007f649b4c5))
* **scripting:** load script files and serve require from the scripts directory ([79ff657](https://github.com/moongate-community/moongate/commit/79ff6574a24a7043c36b5ec84fe01477ccb44dfd))
* **scripting:** make the module binder public and document the package ([84fa4b8](https://github.com/moongate-community/moongate/commit/84fa4b85e02613a06e3f5bfc8ab2a1132711c070))
* **scripting:** schedule coroutines on the timer wheel under an instruction budget ([d43ab38](https://github.com/moongate-community/moongate/commit/d43ab3870aecf20d21333942d1b203cc2697550d))
* **server:** add a script metrics console command ([b05035d](https://github.com/moongate-community/moongate/commit/b05035da5323ec219ff94bb9cde6d9c98654805a))
* **server:** add flags-based server mode configuration ([28e9af2](https://github.com/moongate-community/moongate/commit/28e9af2d47648c34727042d450524ba78373e818))
* **server:** add flags-based server mode configuration ([#4](https://github.com/moongate-community/moongate/issues/4)) ([2302fb8](https://github.com/moongate-community/moongate/commit/2302fb856ae0d9025491d22a648251d58d3d9b72))
* **server:** host the Lua script engine ([56c4180](https://github.com/moongate-community/moongate/commit/56c4180e2e6a2a6a52f3e5ea4e0ec49aed20e457))
* **server:** register typed API handlers through the container ([6d72508](https://github.com/moongate-community/moongate/commit/6d72508236e40736ee05aea169da4c8a0b629dbe))


### Bug Fixes

* **api:** report certificate script failures and clean up partial output ([9e3eb84](https://github.com/moongate-community/moongate/commit/9e3eb8468ee2b6bf7e71d8096cf3626e701a4ff6))
* **api:** retain admission until connection work completes ([d700e4c](https://github.com/moongate-community/moongate/commit/d700e4ca0af731ef54342571ee639c2d3b1e1dfe))
* **network:** preserve the actual bound endpoint during drain ([ee503f1](https://github.com/moongate-community/moongate/commit/ee503f15995f4041be7c9db0b3ce379e8cac973b))
* **packets:** distinguish requested closure from send failures ([0fd415b](https://github.com/moongate-community/moongate/commit/0fd415b1c794f9be3373b50689d141d34b828711))
* **scripting:** annotate enum parameters as enum or string in definitions.lua ([789e6a4](https://github.com/moongate-community/moongate/commit/789e6a48a9123741f7b9409dce0ff770f638cc0e))
* **scripting:** annotate varargs as ... and make definitions.lua robust to any constant ([271d6a4](https://github.com/moongate-community/moongate/commit/271d6a4d9a986859b7d8fa7e22fdc6c753c27c9e))
* **scripting:** cancel script timers on shutdown and publish errors from the loop ([80b3ad5](https://github.com/moongate-community/moongate/commit/80b3ad50c7c5661ac4d3eade944bf0c6847d905c))
* **scripting:** close the arithmetic bypass in the string cap and match Lua's argument errors ([960be94](https://github.com/moongate-community/moongate/commit/960be942381239f79dc8192eab7ec13dd94978ea))
* **scripting:** close the sandbox ([379a2d4](https://github.com/moongate-community/moongate/commit/379a2d4de0f44ced1d0f9d6d2f6c2c7584f89e6b))
* **scripting:** document the built-in modules and let a running coroutine own what it schedules ([817d28e](https://github.com/moongate-community/moongate/commit/817d28e157be7aa360a04169650fdd3030256e71))
* **scripting:** follow links when containing script paths ([7c283fd](https://github.com/moongate-community/moongate/commit/7c283fd137658a46eeb4514cea98d76750dd6703))
* **scripting:** give the logged print standard tostring semantics ([860785a](https://github.com/moongate-community/moongate/commit/860785a8a12a983ab15a8c728f81bdc602a25916))
* **scripting:** import Interfaces.Internal in the engine service ([a8a65d9](https://github.com/moongate-community/moongate/commit/a8a65d98165e1727dee0b8cdb93d67f00e42e45e))
* **scripting:** import Interfaces.Internal in the engine service ([22614af](https://github.com/moongate-community/moongate/commit/22614af1f3843c29069a0b313353165a333ee768))
* **scripting:** keep a failing reload or wait off the game loop's fault path ([c0b5b25](https://github.com/moongate-community/moongate/commit/c0b5b25df925bbb7e58f2f5f2826a1165a390be2))
* **scripting:** keep every coroutine failure inside the scheduler and scope the budget per unit ([51b2fe1](https://github.com/moongate-community/moongate/commit/51b2fe1a74d77d6fc39b5e535941e1925f1613ff))
* **scripting:** keep one cancellation source per coroutine so the budget survives wait ([e3700f0](https://github.com/moongate-community/moongate/commit/e3700f06ea76740b381cc039e894f5f7536b4363))
* **scripting:** let ValueTask.Result rethrow the original exception itself ([1532d94](https://github.com/moongate-community/moongate/commit/1532d945fb56709ffaa762ead08b09d83d041502))
* **scripting:** make the instruction budget survive an abort and cover pcall ([281b162](https://github.com/moongate-community/moongate/commit/281b1621af9002355148e74c44bfb3e390ec4e0c))
* **scripting:** name the member when a script constant getter throws ([fe83b67](https://github.com/moongate-community/moongate/commit/fe83b674599bb6585853044bcdfb475a525404c1))
* **scripting:** report out-of-range integers as Lua errors and document the binder ([e94cc54](https://github.com/moongate-community/moongate/commit/e94cc54a15568ff55d124ae2b678dffff2eb0224))
* **scripting:** route Lua print through the server log ([77a91ed](https://github.com/moongate-community/moongate/commit/77a91ed60887deb004991e71b5be6f7f822ab30c))
* **scripting:** run the engine's start and stop on the game loop thread ([30bb64e](https://github.com/moongate-community/moongate/commit/30bb64eaf0ad2b43a345cb55d5b6aaad2e0b6f53))
* **scripting:** zero-pad control-character escapes in definitions.lua ([fba7b33](https://github.com/moongate-community/moongate/commit/fba7b3304ffec3bffe26f087d13aebc2d5af8762))
* **server:** validate the scripting section with the server config ([0607e30](https://github.com/moongate-community/moongate/commit/0607e305270582dba6892cb960f5925384515ffb))


### Code Refactoring

* **packets:** separate connection sends from game sessions ([a07e1dc](https://github.com/moongate-community/moongate/commit/a07e1dc7f340e4db5c770f660177e8c2b5c8d93e))

## [0.3.0](https://github.com/moongate-community/moongate/compare/v0.2.0...v0.3.0) (2026-09-18)


### Features

* **core:** add a packet hex formatter ([d02d73d](https://github.com/moongate-community/moongate/commit/d02d73d4ce6f2f13d773f40211438e39f237e6e1))
* **core:** read the assembly codename from metadata ([abde51e](https://github.com/moongate-community/moongate/commit/abde51e3fb87d50d28014f892729de8f0e195738))
* **diagnostics:** add typed snapshots and process metrics ([80ae398](https://github.com/moongate-community/moongate/commit/80ae398e8d9fe06d90821d4f2431f38e4bb5d49c))
* **diagnostics:** collect and publish immutable snapshots ([54b8636](https://github.com/moongate-community/moongate/commit/54b86362676a8b78b8fa66bbc3212c9288df44b8))
* **diagnostics:** expose loop timer and session metrics ([981504a](https://github.com/moongate-community/moongate/commit/981504aa1c18765ddfcf2974a7809b45870c3e92))
* **diagnostics:** integrate configurable diagnostic service with bootstrap ([c4f6b25](https://github.com/moongate-community/moongate/commit/c4f6b2522148d6e5bdc56c74f963e8a0d8a46c96))
* print the codename in the startup header, sourced from the Codename ([a9da36e](https://github.com/moongate-community/moongate/commit/a9da36eb7ebade3926b2bff411f72e81584739a0))
* register IUltimaDataService with UltimaDataService at priority -10 and ([a9da36e](https://github.com/moongate-community/moongate/commit/a9da36eb7ebade3926b2bff411f72e81584739a0))
* **release:** attach a SHA-256 checksum next to each release tarball. ([948c746](https://github.com/moongate-community/moongate/commit/948c7462dc8550eb14acad57f0fcd2d0f749a4d1))
* **server:** add single-instance startup and the Ultima data service ([a9da36e](https://github.com/moongate-community/moongate/commit/a9da36eb7ebade3926b2bff411f72e81584739a0))
* **server:** write the log file as compact JSON ([06395e3](https://github.com/moongate-community/moongate/commit/06395e38369d5e2dcb543d3a815f18cbaecb30d1))


### Bug Fixes

* **docker:** copy Moongate.Ultima.csproj in the restore layer. The server has ([948c746](https://github.com/moongate-community/moongate/commit/948c7462dc8550eb14acad57f0fcd2d0f749a4d1))
* **docker:** hold the server root at /data, created and owned by the runtime ([6f682e2](https://github.com/moongate-community/moongate/commit/6f682e295c9d2ce1009db926b3257090abc30342))
* **docker:** let the licence into the build context ([01780ff](https://github.com/moongate-community/moongate/commit/01780ffc5bed1c9c96a9bb9eda8039658a775d78))
* give UltimaConfig.UltimaPath a default instead of leaving it null. ([a9da36e](https://github.com/moongate-community/moongate/commit/a9da36eb7ebade3926b2bff411f72e81584739a0))
* **tests:** build the plugin fixtures in the requested configuration ([abc6cca](https://github.com/moongate-community/moongate/commit/abc6ccafd585e5e5d6ee75aeaddbcf53412fefc7))

## [0.2.0](https://github.com/moongate-community/moongate/compare/v0.1.0...v0.2.0) (2026-09-18)


### Features

* **config:** load TOML server settings and create defaults ([939246b](https://github.com/moongate-community/moongate/commit/939246bd48e703c3cf9f70775effbdd6bbffd036))
* **core:** add entity primitives and expand utility coverage ([0bb77bd](https://github.com/moongate-community/moongate/commit/0bb77bd223706a4b77b83299a71340478dea47e1))
* **core:** add runtime utilities and shared buffer infrastructure ([e3434c2](https://github.com/moongate-community/moongate/commit/e3434c230c0ea9488c7ba76f456fd08a4b47830d))
* **core:** add TOML serialization utilities ([63731c3](https://github.com/moongate-community/moongate/commit/63731c30041101ac7b31ca7a4e7fe65df34a7f34))
* **core:** default TOML property names to snake_case ([e5ced0c](https://github.com/moongate-community/moongate/commit/e5ced0c93b0f0861495a55faefc6000525025c09))
* **core:** port geometry primitives from legacy Moongate ([4256a4d](https://github.com/moongate-community/moongate/commit/4256a4d0d53243f26a604864b4cfc0b20f4df1ce))
* **logging:** log service startup and improve console layout ([eac6b87](https://github.com/moongate-community/moongate/commit/eac6b879dd5a786ef1799f36718bed3f1147c72d))
* **network:** add bounded pending frame buffer ([544faac](https://github.com/moongate-community/moongate/commit/544faacf2907898f8c6c1182d972b624241db91f))
* **network:** add opcode diagnostics and wire session events ([035dc28](https://github.com/moongate-community/moongate/commit/035dc28f8d6d448c2ebebf32e209307884ec2ee6))
* **network:** add standalone ping and login packets ([0fe330a](https://github.com/moongate-community/moongate/commit/0fe330a2d6bb0e81c5abbc15d3bf9057331303a9))
* **network:** add supported client features packet ([a846be3](https://github.com/moongate-community/moongate/commit/a846be3929537f5300e01a13935aa0a563696b5a))
* **network:** expose listener endpoints and scaffold network service ([7faecf8](https://github.com/moongate-community/moongate/commit/7faecf8e092cd4497c4c5298f258d3f8865754d6))
* **network:** frame incoming Ultima Online packets ([3e0e0fa](https://github.com/moongate-community/moongate/commit/3e0e0fa444c974a043104a1c7bfb21bce2d0921b))
* **network:** infer packet direction during registration ([fdbb70e](https://github.com/moongate-community/moongate/commit/fdbb70ead0a98c54c51c96003655bbefc1f19463))
* **network:** port standalone TCP transport from Moonwell ([d9e1f9b](https://github.com/moongate-community/moongate/commit/d9e1f9bd521c1109ee9131454e73a97b6ec79be5))
* **network:** request listener shutdown when network service stops ([f863145](https://github.com/moongate-community/moongate/commit/f863145fdde9d43f75634d2e02489b2bd0c33e89))
* **packets:** add queued replies and initial packet handlers ([349d0da](https://github.com/moongate-community/moongate/commit/349d0da4779aa76b686124ff0342a202defaa23e))
* **packets:** log registered packet and handler counts at startup ([0be9311](https://github.com/moongate-community/moongate/commit/0be931182049d36ccd2c1a5f493fda34f7bf0974))
* **packets:** register typed handlers and dispatch on the game loop ([d51d704](https://github.com/moongate-community/moongate/commit/d51d7040037f31d29bd978a1e4197f31cff04fa2))
* **persistence:** add durable binary collection storage ([f048f77](https://github.com/moongate-community/moongate/commit/f048f77d1380f3122c56d3cfdfb7576a4e2024a0))
* **persistence:** add saving all registered live entities ([929dac3](https://github.com/moongate-community/moongate/commit/929dac36d2cd79e39084a3821b30a0ae78a59102))
* **persistence:** add typed data access and ZLinq queries ([103d407](https://github.com/moongate-community/moongate/commit/103d40735b7982549f751f1b1e536cb325007ac4))
* **persistence:** add verified backup and restore ([5823827](https://github.com/moongate-community/moongate/commit/58238279367f7ed57761d6d6440401cba156e076))
* **persistence:** merge binary persistence into develop ([13b88b4](https://github.com/moongate-community/moongate/commit/13b88b4034ce9b9eb14a73484f35c2c2a541ea67))
* **persistence:** separate world capture from durable writes ([6ed72f6](https://github.com/moongate-community/moongate/commit/6ed72f6a08408a42a1def92636c9503901edd1cc))
* **plugins:** add lifecycle event subscriptions ([5cc00c3](https://github.com/moongate-community/moongate/commit/5cc00c3d91bd11da3722fd9de46cd556c313867f))
* **server-core:** add account and command enums with session account type ([93990da](https://github.com/moongate-community/moongate/commit/93990dad7a35f37a58b1557075ffe1d20b18bbfb))
* **server-core:** add command context and definition metadata ([d70ed2d](https://github.com/moongate-community/moongate/commit/d70ed2d140c0310436c1e476d297f28647c1a262))
* **server-core:** add command executor contract and deferred registry ([0c2063f](https://github.com/moongate-community/moongate/commit/0c2063f0ffd45f58af442e87279976e935870387))
* **server:** add bootstrap lifecycle and circular buffer coverage ([d39f1c0](https://github.com/moongate-community/moongate/commit/d39f1c0675d182c69eb78556b4ebdfe7fd5d285a))
* **server:** add command system service with source and account gates ([837e604](https://github.com/moongate-community/moongate/commit/837e604fca68cfe2f6da2987d45feee6dc1dc842))
* **server:** add console host and Docker setup ([0ffc583](https://github.com/moongate-community/moongate/commit/0ffc583a78f776ca86ef0eec4bee9921de2b5a55))
* **server:** add console input loop dispatching operator commands ([f8a41ec](https://github.com/moongate-community/moongate/commit/f8a41ecf5a06f04eea9311ff9393b4d267158140))
* **server:** add dedicated game loop with bounded work queue ([2ef60c5](https://github.com/moongate-community/moongate/commit/2ef60c529667cfa7fbe9f4db8cb74cc99d499f1d))
* **server:** add echo command and register the command system at startup ([45b5715](https://github.com/moongate-community/moongate/commit/45b57159eafcc47cfcf2e76ce7d645ca133e87ee))
* **server:** add fluent bootstrap service registration ([88faa8c](https://github.com/moongate-community/moongate/commit/88faa8cfed7ed40f81a315bcf01cff2d8065796e))
* **server:** add pinned console prompt service with terminal seam ([7c9996d](https://github.com/moongate-community/moongate/commit/7c9996ddf008da4ff0346597db599d24e69b3b00))
* **server:** add plugins with versioned dependencies ([d5fb2df](https://github.com/moongate-community/moongate/commit/d5fb2df81e7ee02d538ec710f9308c05416012ac))
* **server:** add service registration and single-file publishing ([6313ba0](https://github.com/moongate-community/moongate/commit/6313ba0300f2fee2f12110248e8878daaa9395c3))
* **server:** coordinate periodic and final world saves ([7431d31](https://github.com/moongate-community/moongate/commit/7431d3197d660f237844714f8251a7dff85d352f))
* **server:** enable the interactive console prompt at startup ([ef392ba](https://github.com/moongate-community/moongate/commit/ef392bab311d70891a9a34afa40870f2b6aee7ec))
* **server:** expose event bus service wrapper ([54c2d73](https://github.com/moongate-community/moongate/commit/54c2d738c076a2bdd0c2e33416896812f3ec4db9))
* **server:** integrate packet handling with TCP lifecycle ([0b8179b](https://github.com/moongate-community/moongate/commit/0b8179b096faac7e33f41724919228d78aa9baf5))
* **server:** integrate persistence lifecycle ([c9fe6b4](https://github.com/moongate-community/moongate/commit/c9fe6b4cbe17237e1f43e7a8e661b5be4dc969b6))
* **server:** load plugin bundles before service startup ([8203099](https://github.com/moongate-community/moongate/commit/820309993d7d5a98cc66473989347a1b229f1c35))
* **server:** port timer wheel into the game loop ([142221c](https://github.com/moongate-community/moongate/commit/142221c150866eb24660fca4c6cb8c74880532af))
* **server:** register directory configuration and refresh startup banner ([d9b2f97](https://github.com/moongate-community/moongate/commit/d9b2f9716c9a344c2d89a57ec2ad16c482020967))
* **server:** route serilog console output around the prompt row ([4b54821](https://github.com/moongate-community/moongate/commit/4b54821cfbd3628d69d7fba25a85b3d91beb3090))
* **sessions:** add separate network and game session models ([29b1673](https://github.com/moongate-community/moongate/commit/29b1673ea29d1870f43d33b3249c739292a90685))
* **sessions:** port session registry and register singleton service ([3afd670](https://github.com/moongate-community/moongate/commit/3afd67001dafaa42b4f3ecbbca089804c02f216f))
* **ultima:** initialize asset library with SkiaSharp ([00c185a](https://github.com/moongate-community/moongate/commit/00c185a2bf62b160a350ea9272626acd64feb830))


### Bug Fixes

* **ci:** let each release asset build fail independently ([c05fae0](https://github.com/moongate-community/moongate/commit/c05fae0a15bdf9b058f9e830250f8c7fca323b55))
* **ci:** pin release-please to main and build assets from the tagged commit ([06de8f8](https://github.com/moongate-community/moongate/commit/06de8f8b8cbe39082ce570c71e288a9b6d9d75cc))
* **ci:** use release-please's include-v-in-tag instead of tag-format ([e2015a4](https://github.com/moongate-community/moongate/commit/e2015a4d8f5715650156100738c6b0f9eaca1e40))
* **docker:** include persistence in cached restore ([55ff990](https://github.com/moongate-community/moongate/commit/55ff99098ec8b2d2a95892943ba8e5e609001063))
* **game-loop:** describe the batch limit accurately in startup logs ([7a84ea3](https://github.com/moongate-community/moongate/commit/7a84ea3d287611d47b990e2d045b250e25cde0c8))
* **network:** coordinate TCP listener lifecycle and cleanup ([4d5d816](https://github.com/moongate-community/moongate/commit/4d5d816b83cdc1a7c2357b9643386abef4bbb080))
* **network:** preserve send failures and drain connection resources ([d68e8f3](https://github.com/moongate-community/moongate/commit/d68e8f35de59f6196e235418eda393a94f4fa21d))
* **network:** reject admitted sends after logical closure ([6d96037](https://github.com/moongate-community/moongate/commit/6d96037c74dc7033d31eb011b3a9477d0a118f61))
* **network:** reject empty client version frames ([7cd11b7](https://github.com/moongate-community/moongate/commit/7cd11b749dcea64b2555b9fc401279ee3e3ef980))
* **network:** reject whitespace versions before game-loop dispatch ([876bbd9](https://github.com/moongate-community/moongate/commit/876bbd9c20dd749bd3ffb913711a5a34d8826066))
* **persistence:** close capture and disposal races ([ac90bfa](https://github.com/moongate-community/moongate/commit/ac90bfa2226e72a67f44eccd9ad430dc769b99bc))
* **persistence:** preserve collection cleanup after owner shutdown ([7dc2cfd](https://github.com/moongate-community/moongate/commit/7dc2cfda4ae289b84879ead1440f9681199ed9e1))
* **plugins:** preserve metadata startup cleanup failures ([6adcedd](https://github.com/moongate-community/moongate/commit/6adcedd0c7daac59b50f1b9d01a9dc805e64f899))
* **plugins:** publish lifecycle tasks before callback reentry ([0fe5847](https://github.com/moongate-community/moongate/commit/0fe58478cf92128f62228f232641577e0d2b03c4))
* **server:** align persistence lifecycle namespaces ([05afb42](https://github.com/moongate-community/moongate/commit/05afb42a967361ca1081ec4ca36a8c5fc59bcba9))
* **server:** force process termination when shutdown leaves it running ([7f811cc](https://github.com/moongate-community/moongate/commit/7f811cc06678abb0aaaf45ed669e919c061bfc76))
* **server:** harden console prompt logging, input faults and row width ([2ec598b](https://github.com/moongate-community/moongate/commit/2ec598bf2e48921d37c73af1e0d9bfc58052732e))
* **server:** reject network startup after shutdown begins ([c3e324b](https://github.com/moongate-community/moongate/commit/c3e324b13a986a35c125c22bb2ec2f7527abd56a))
* **server:** reject zero-valued command source and document console authority ([7e56727](https://github.com/moongate-community/moongate/commit/7e567277e362d6bcc7be73dcb9874dd4e092c525))
* **server:** stop the prompt writer retrying a failed console write ([fb693c0](https://github.com/moongate-community/moongate/commit/fb693c0da5ea82e749c5a00fce11989c09eeb976))
* **sessions:** enforce terminal mutation precedence ([3251161](https://github.com/moongate-community/moongate/commit/32511619c3a8d95e95da6f558aca78b1f6b027c7))


### Performance Improvements

* **network:** bound frame buffers and remove redundant receive copies ([d839eec](https://github.com/moongate-community/moongate/commit/d839eecb02ad8831c30231a481ed83f72feb5854))


### Reverts

* **server:** drop the forced-termination guard ([186f55f](https://github.com/moongate-community/moongate/commit/186f55f758fb6f34785cf8bc1cd8cf164ca66d7a))
