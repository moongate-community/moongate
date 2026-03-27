## [1.2.0](https://github.com/moongate-community/moongate/compare/v1.1.0...v1.2.0) (2026-03-27)

### Features

* add 9 Lua AI brains for ported ModernUO NPCs ([8345eec](https://github.com/moongate-community/moongate/commit/8345eecfe0ab3f3fc4b303bf67157aed2a6212f4)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* add centralized hot reload for scripts and templates ([41ac8af](https://github.com/moongate-community/moongate/commit/41ac8afc8753a9093c3f3364eff4b460af527c9b))
* add generated NPC templates from ModernUO ([91ce4f5](https://github.com/moongate-community/moongate/commit/91ce4f5babac8551aaebf79494fe7ecdc6da170f)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* add gm command entry point ([4e49158](https://github.com/moongate-community/moongate/commit/4e49158ca73bc51351c9b7a914bac83ff5745f15))
* add gm menu lua hub ([3dd801a](https://github.com/moongate-community/moongate/commit/3dd801a472973b5811083740d001a128c4aae861))
* add gm menu spacing probe ([d08c6cd](https://github.com/moongate-community/moongate/commit/d08c6cdb3f37e97f7102628ea367b66293aee62e))
* add gm menu spawn and broadcast tabs ([e629aee](https://github.com/moongate-community/moongate/commit/e629aee3e295bcce4a21a3b7a6f76e6bba967dfc))
* add gm sidebar menu ([2d6eb90](https://github.com/moongate-community/moongate/commit/2d6eb90a6a71b981e76618b942f50f24f5014817))
* add guard scripting module ([5184132](https://github.com/moongate-community/moongate/commit/5184132757fbb1fa3a827ec51f8e9587346b1baa))
* add lua target cursor bridge ([d53a0f9](https://github.com/moongate-community/moongate/commit/d53a0f90f85b831746dd01f72676afe092bd509b))
* add lua template search bridges ([6e00477](https://github.com/moongate-community/moongate/commit/6e00477d7a1da76add789bc39f1ac8ef3a880f45))
* add lua training objects ([93b8c76](https://github.com/moongate-community/moongate/commit/93b8c76d367da8ca295d09171bf03fbc33867f90)), closes [#162](https://github.com/moongate-community/moongate/issues/162)
* add modernuo brain runtime module api ([d9d5ec9](https://github.com/moongate-community/moongate/commit/d9d5ec90ad5bd0d56f3a540fd5c3878e528f3707))
* add optional guard patrol roam ([b46fa53](https://github.com/moongate-community/moongate/commit/b46fa53e59370933587f0bf410b40524c45bfca7))
* add phase 1 modernuo ai brains ([2bfea3e](https://github.com/moongate-community/moongate/commit/2bfea3e9a9e3946d24b03f5f5b573344efeadb86))
* add Python converter tool for ModernUO NPC porting ([f9193d5](https://github.com/moongate-community/moongate/commit/f9193d55d848c88340f129cb2e2fc22a08dfa893))
* add Resistances and DamageTypes to MobileTemplateDefinition ([#171](https://github.com/moongate-community/moongate/issues/171)) ([b5021ec](https://github.com/moongate-community/moongate/commit/b5021ec9e73ae6c79d85d50f56a3640864910097))
* add reusable gump layout helpers ([1d353c2](https://github.com/moongate-community/moongate/commit/1d353c20714e46c36ceba16498498847409c9f75))
* add template validator dotnet tool ([076e0b2](https://github.com/moongate-community/moongate/commit/076e0b2d110e0d64a0c1510e73a07d6b3aac363c)), closes [#184](https://github.com/moongate-community/moongate/issues/184)
* add validator run header ([8ecc9c7](https://github.com/moongate-community/moongate/commit/8ecc9c75591657aab2901c6b570574cbaa83204b))
* **converter:** emit canonical mobile ai metadata ([c47d51f](https://github.com/moongate-community/moongate/commit/c47d51f97d3ff77132196072baecb8d4225d5448))
* enable default patrol on guards ([8ed497b](https://github.com/moongate-community/moongate/commit/8ed497bf8450d5569d7cd6848cee4f6732533f99))
* map resistances and damage types from template to mobile entity ([#171](https://github.com/moongate-community/moongate/issues/171)) ([473b7b3](https://github.com/moongate-community/moongate/commit/473b7b3fd3a6f4ad2bd921fdf8f84b71b3305825))
* migrate mobile templates to variants ([3382641](https://github.com/moongate-community/moongate/commit/33826411a420c2ca3cf14e421a18a68c3cc9dc3a)), closes [#179](https://github.com/moongate-community/moongate/issues/179)
* port guard brain parity into lua ([85ce86d](https://github.com/moongate-community/moongate/commit/85ce86d43e86df90ae3a01b80983cef8d50638a8))
* **uo:** add canonical mobile ai template schema ([d8bb1d1](https://github.com/moongate-community/moongate/commit/d8bb1d1688bf9d41501480d971ec7f40019cd22a))

### Bug Fixes

* address code review issues in NPC porting ([c88443c](https://github.com/moongate-community/moongate/commit/c88443ca456b10a2c9dc654a86181e66e4a4ae2a)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* align guard brain runtime naming ([d0830a1](https://github.com/moongate-community/moongate/commit/d0830a1bac7f4f035436c14f1ae3f70d4ae204b5))
* align template validator version metadata ([9c40dbb](https://github.com/moongate-community/moongate/commit/9c40dbb4b7ced180816393f23f26110c459a769c))
* anchor guard patrol roaming to home ([603fa2e](https://github.com/moongate-community/moongate/commit/603fa2edb9d063f4e1a0c57ed3286f2c1aed805c))
* **converter:** limit symbolic ai field scope ([e30a2b4](https://github.com/moongate-community/moongate/commit/e30a2b471420798547105cf7012a0e4db0baf3d7))
* **converter:** resolve inherited modernuo ai metadata ([479e221](https://github.com/moongate-community/moongate/commit/479e221046e0c10735cb83ea2f31a73de7821913))
* **converter:** resolve package-private ai ranges ([7ab224e](https://github.com/moongate-community/moongate/commit/7ab224ede1adadabedfd250ed5e5e740038a7c91))
* correct passive mobile ai metadata ([b4ef439](https://github.com/moongate-community/moongate/commit/b4ef439be17710ddcabe923516902b9885845ccc))
* enhance path traversal security checks in LuaScriptLoader ([2c11e4f](https://github.com/moongate-community/moongate/commit/2c11e4f36eabdfca4c496bf0a81f813280a74f92))
* enhance template path validation and security checks ([53c9352](https://github.com/moongate-community/moongate/commit/53c9352432b0f9e567ed89c43520fc57b535bf36))
* handle missed combat hooks in ai vendor ([84dad84](https://github.com/moongate-community/moongate/commit/84dad841cbd72b8930d14b8346c901bb5f42f77f))
* handle nullable numeric lua bindings ([5dbc2c4](https://github.com/moongate-community/moongate/commit/5dbc2c4e36d89c17b244e7ef0dd5a36843a0b9bb))
* harden release workflow git setup ([0fb1d56](https://github.com/moongate-community/moongate/commit/0fb1d567b6eea1bd77f16cd923c29468f6250358))
* harden semantic-release github success handling ([d5923c7](https://github.com/moongate-community/moongate/commit/d5923c7f579c5fbaf1e0b8b1024adaede4f7a5a6)), closes [#127](https://github.com/moongate-community/moongate/issues/127) [#126](https://github.com/moongate-community/moongate/issues/126)
* import ModernUO loot packs and support items ([6d47b56](https://github.com/moongate-community/moongate/commit/6d47b568def65b0223b58d3ce7076062cae5ba69))
* include recipient mobile id in lua combat hooks ([2d4f836](https://github.com/moongate-community/moongate/commit/2d4f83632cf58bdb0362b8226671d025c1150f2d))
* install python in release workflow ([515fcdb](https://github.com/moongate-community/moongate/commit/515fcdb9650b1ce113231ac1b3ec61eb11e080bb))
* migrate ai blackboard legacy keys in npc state ([21927e8](https://github.com/moongate-community/moongate/commit/21927e86ed312d26f6171e27781b34942945405f))
* migrate mobile brains to canonical ai blocks ([e6ee153](https://github.com/moongate-community/moongate/commit/e6ee15328c212a31d21b44b8ac4970c4aad7400d))
* normalize duplicate ai blackboard aliases ([0e0ffb3](https://github.com/moongate-community/moongate/commit/0e0ffb32ce632f20fdf4bfb54f517aed36e84f7b))
* normalize strongest ai target scoring ([17a60de](https://github.com/moongate-community/moongate/commit/17a60de0c943b7b5af06addf1051c1753576ee8b))
* polish gm menu spawning workflow ([f8c9b46](https://github.com/moongate-community/moongate/commit/f8c9b465205a2ff7a1918fef80c658c52abbbab0))
* recover guard patrol after boundary breach ([c364bc5](https://github.com/moongate-community/moongate/commit/c364bc57b08f1938615b541546ea9bacdf2a6905))
* remove gm menu probe and guard patrol crash ([32d75c3](https://github.com/moongate-community/moongate/commit/32d75c3d85d85391af9a7a19ba00369375f543a8))
* restore ModernUO NPC outfit extraction ([ec112b6](https://github.com/moongate-community/moongate/commit/ec112b63f5d589ff72132f87abb1ef5347975239)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* stop requiring guards lua module ([62c7971](https://github.com/moongate-community/moongate/commit/62c7971e154b011b5b664f58d01b9be799336023))
* tighten mobile ai parser inheritance ([94d6301](https://github.com/moongate-community/moongate/commit/94d6301150d40d1848999dd0f4e6c4e3abaa0a0e))
* trust workspace in release workflow ([6c049e9](https://github.com/moongate-community/moongate/commit/6c049e9b841bb2a0563f947e0cd21a54584ca5fc))
* **uo:** guard null mobile ai fight mode ([a9508c3](https://github.com/moongate-community/moongate/commit/a9508c36ac06f5a6c1b672d77afac25a3851eae3))
* **uo:** make mobile ai inheritance explicit ([ecb290a](https://github.com/moongate-community/moongate/commit/ecb290a5b2e3bb6502caa2183e0116b007392843))
* **uo:** migrate mobile ai loader and runtime ([58cbc52](https://github.com/moongate-community/moongate/commit/58cbc5264f1dad433b4326487449d6eade7035fe))
* **uo:** remove legacy mobile brain shim ([b3676ca](https://github.com/moongate-community/moongate/commit/b3676ca3e6800b3a846805b1ebd6ba51e44d9399))
* **uo:** update remaining mobile ai test fixtures ([58494c7](https://github.com/moongate-community/moongate/commit/58494c7b99e3048409a3489cf372a302e36825d2))

### Contributors

- tom
- Agent 57951

## [1.1.0](https://github.com/moongate-community/moongate/compare/v1.0.0...v1.1.0) (2026-03-24)

### Features

* add additive loot template support ([930355c](https://github.com/moongate-community/moongate/commit/930355c093c1f52cd93efb857875c771b23a2114))
* add city guard and vendor npc data ([2ff5025](https://github.com/moongate-community/moongate/commit/2ff5025ed78303d2666d5c9349d37a1a0029c33c))
* add combat-driven skill gain ([64f99c9](https://github.com/moongate-community/moongate/commit/64f99c920a5595ed588533e0ea7500c645f98afc))
* add contributor attribution to release notes ([cf21e6d](https://github.com/moongate-community/moongate/commit/cf21e6d3fa2be88143897129350141359acdf38d))
* add enhanced client session support ([f381262](https://github.com/moongate-community/moongate/commit/f3812627702e57304ff26bc0e5b035642f3b504f))
* add factions v1 for ai and notoriety ([b3e2efb](https://github.com/moongate-community/moongate/commit/b3e2efb7ac9913367e1bccac0d23da503f05bd99))
* add guard in-range greetings and threat reactions ([3a0f1a4](https://github.com/moongate-community/moongate/commit/3a0f1a40c936d15fef2fc3ae3db01e6294628835))
* add guard positioning behaviors ([c2cfe7f](https://github.com/moongate-community/moongate/commit/c2cfe7f5cf7818ed41d6096f813bdc4923309df9))
* add lua-configurable reputation titles ([6006916](https://github.com/moongate-community/moongate/commit/60069163aa7b251589dd571c5fa32b38e9ee19de))
* add ModernUO static template generator ([f37bc40](https://github.com/moongate-community/moongate/commit/f37bc4076e5f65b723389ba452539daa60e93e50))
* add moongate plugin template package ([3118db3](https://github.com/moongate-community/moongate/commit/3118db3a71aa3643f9cc1a0ba89d130db00d694b))
* add npc self-bandage behavior ([5ae3576](https://github.com/moongate-community/moongate/commit/5ae357639fa7fc99c472811e744f7da4e7351b06))
* add quiver support for ranged combat ([0b94f8e](https://github.com/moongate-community/moongate/commit/0b94f8e7f022a59e8eb24bd6e606b5f4e3aae4f8))
* add ranged combat v1 for bows and crossbows ([4e30f9f](https://github.com/moongate-community/moongate/commit/4e30f9f27aeacb7da7baf20ac26618ece5d0b213))
* add ranged spacing behavior for guard archers ([d976102](https://github.com/moongate-community/moongate/commit/d976102e2b3b69d5680af88fa39ef353333c0220))
* add reusable base loot packs ([70d8a16](https://github.com/moongate-community/moongate/commit/70d8a167bab46cfcdfd80ecaffd8caf9aebf873d))
* add skill gain anti-macro and stat progression ([cc9d794](https://github.com/moongate-community/moongate/commit/cc9d794eb7ecf0e572fb615ad0407051784fb2b4))
* add undead melee zombie brain ([dd6bd75](https://github.com/moongate-community/moongate/commit/dd6bd7522e7d2834cb8aec4a7a361faab18ad37b))
* add vendor consumables and clock item scripts ([e9c86e2](https://github.com/moongate-community/moongate/commit/e9c86e2a1cdf723d335ece7fb62ea6d26cacf40d))
* add viewer-relative ai relations for npc brains ([3f5ab53](https://github.com/moongate-community/moongate/commit/3f5ab53f2c6980615f57c11c2d460995e957703c))
* add viewer-relative ai relations for npc brains ([33f5f38](https://github.com/moongate-community/moongate/commit/33f5f38182e322bf368ca448ec0dc7cbff1f2ed3))
* add weapon combat sounds and blood hit effects ([3639d8d](https://github.com/moongate-community/moongate/commit/3639d8d092b7565634c8fee2deaf0eb3845ff5a3))
* append missing ModernUO static item templates ([33e5471](https://github.com/moongate-community/moongate/commit/33e54711a44bccc3f41395194d13e279713ab011))
* award fame and karma for npc kills ([e30bb1c](https://github.com/moongate-community/moongate/commit/e30bb1c0de3559af0ef5ac03d940fbece369f6c5))
* enhance command execution with user authorization checks ([d5fa5fc](https://github.com/moongate-community/moongate/commit/d5fa5fc104a81d61305ab15c6774764d42f04a03))
* expand ModernUO item template coverage ([a366f76](https://github.com/moongate-community/moongate/commit/a366f76fee0861491333cab2945f4de0b46e8a83))
* extend loot generation for tags and corpses ([4fa2760](https://github.com/moongate-community/moongate/commit/4fa27605aa8b2c5880286b92883befedca5e0a0f))
* implement player character profile packet flow ([3a57653](https://github.com/moongate-community/moongate/commit/3a5765364d8bc4594f4727c0d6f970214e53ba67))
* import creature and container loot templates ([352e0c3](https://github.com/moongate-community/moongate/commit/352e0c3d308a37240005227a0f21418ee3f9afa3))
* integrate pending gameplay and infrastructure updates ([275c24a](https://github.com/moongate-community/moongate/commit/275c24a70d00d8d4e114ead348fc1d219dd882e2))
* purge persisted corpses on startup ([c658fea](https://github.com/moongate-community/moongate/commit/c658fead0538eeec03e813baa65be21a300ea321))
* support encrypted UO client handshakes ([14e5112](https://github.com/moongate-community/moongate/commit/14e511277e1d2fddb95ad9b4cec7606478801279))

### Bug Fixes

* align enhanced client login handshake ([4453d32](https://github.com/moongate-community/moongate/commit/4453d327bd94b8af40c195e4c5760178a8582d99))
* align enhanced client shard redirect flow ([47e68a6](https://github.com/moongate-community/moongate/commit/47e68a6d501b79170221aad96407e1976de56bf7))
* align enhanced client shard selection handshake ([dcbaea1](https://github.com/moongate-community/moongate/commit/dcbaea12fc5f77c9b5bb6e4616d102a492da069d))
* allow command endpoint without jwt ([c635259](https://github.com/moongate-community/moongate/commit/c6352598ef144a779cb3500b5424df5435788b31))
* allow configured shard redirect address ([030c4ee](https://github.com/moongate-community/moongate/commit/030c4eeaffde9f5b61fe838dd5ec9ccd16dcbff0))
* allow configured shard redirect address ([d092583](https://github.com/moongate-community/moongate/commit/d09258317e948f9971dc9f9633d0f1d99313af4c))
* correct enhanced client server list ip order ([1230609](https://github.com/moongate-community/moongate/commit/1230609787bbe955aa5df4b5278c9574c4a178c0))
* correct enhanced client server list ip order ([8caa529](https://github.com/moongate-community/moongate/commit/8caa529dfe19ad5ffb2c73f8e2a9d9cf6d69b2c0))
* enhanced client funziona ([7b24bd5](https://github.com/moongate-community/moongate/commit/7b24bd583fe2ded972d790269b0a7ef0bdb18bfa))
* exclude template payload from docfx metadata ([41ff039](https://github.com/moongate-community/moongate/commit/41ff039f09e461900e145ad0fae45461169b739d))
* hydrate equipped container contents for status values ([da25373](https://github.com/moongate-community/moongate/commit/da25373bed18a389beda6886bc5740ebc04ceb79))
* parse enhanced client hardware info before shard select ([9a022f5](https://github.com/moongate-community/moongate/commit/9a022f589a07219949a2b15861977f35efe4356d))
* preserve enhanced client metadata across shard redirect ([9aaa731](https://github.com/moongate-community/moongate/commit/9aaa731a0a77eb110eb2f02af38ec78ddf1cb08c))
* resolve post-merge skill gain regressions ([5dc1eb4](https://github.com/moongate-community/moongate/commit/5dc1eb48e204510cd3c3e674bef7899cc9af8f43))
* restore item template startup integrity ([cfa5843](https://github.com/moongate-community/moongate/commit/cfa584399ff739b8ecaf0cf9abd01bca5144f2bf))
* restore modernuo server list address encoding ([15d12eb](https://github.com/moongate-community/moongate/commit/15d12eba932403906645d8cfd0c74ee348c5b0bc))
* restore ModernUO server list address encoding ([ee21f5a](https://github.com/moongate-community/moongate/commit/ee21f5af6910a7d802552fe4e06f59c539908603))
* send effective carried weight in player status ([5884671](https://github.com/moongate-community/moongate/commit/58846719bb3632e9c4b8c00358fd99b02ac055df))
* stabilize guard combat behaviors ([dd5f596](https://github.com/moongate-community/moongate/commit/dd5f5965abbcb5ae189c35ffbab562335640717a))
* stabilize guard hostility and corpse visuals ([16b0c95](https://github.com/moongate-community/moongate/commit/16b0c95b09a1e8e6214b8b43c483252dbdaca30b))
* tighten combat and inventory live sync ([64357b6](https://github.com/moongate-community/moongate/commit/64357b6b582460e8a49907f370dd145a872265a6))

### Contributors

- tom
- Agent 57951

## [1.0.0](https://github.com/moongate-community/moongate/compare/v0.36.0...v1.0.0) (2026-03-20)

### ⚠ BREAKING CHANGES

* the server is no longer published or deployed as a NativeAOT binary. Docker now runs the framework-dependent Moongate.Server.dll on the ASP.NET runtime image, and any AOT-specific publish or operational workflow has been removed.

### Features

* add admin help ticket dashboard ([c3cf530](https://github.com/moongate-community/moongate/commit/c3cf530c217690a48cb14707b9b8883693e35802))
* add first-open chest loot generation ([115325e](https://github.com/moongate-community/moongate/commit/115325e15750704aebdd00900ec6550ae705e75e))
* add help ticketing flow and endpoints ([08fa508](https://github.com/moongate-community/moongate/commit/08fa508ec8e7e7bb2cb14580f79382ff83a15aa4))
* add lua-first scheduled events runtime ([241c7c2](https://github.com/moongate-community/moongate/commit/241c7c28b03db3052ac3e454901c66a79010b834))
* add plugin sdk abstractions and nuget publish flow ([ce460da](https://github.com/moongate-community/moongate/commit/ce460da2dc066f3f66e7d5aaaabcaa726be0b627)), closes [#95](https://github.com/moongate-community/moongate/issues/95) [#96](https://github.com/moongate-community/moongate/issues/96)
* add refillable chest loot containers ([8678912](https://github.com/moongate-community/moongate/commit/8678912e9eca004374651ccd862e0a59eb58bf1b))
* add startup csharp plugin host ([c746ca0](https://github.com/moongate-community/moongate/commit/c746ca0dea7b39756de93c078b8c1617f1493af5))
* add udp ping server ([c098f15](https://github.com/moongate-community/moongate/commit/c098f155843319eea5ca617df252fb35339b53e2))
* extend in-game item admin commands ([952800e](https://github.com/moongate-community/moongate/commit/952800ef81485c7477962ce10dd817a20bae6a20))
* move starter loadout generation to lua ([fdaddc1](https://github.com/moongate-community/moongate/commit/fdaddc1aaafde24339cec1b2807cd067d5143c45))
* publish aggressive action combat events ([ef874d0](https://github.com/moongate-community/moongate/commit/ef874d0c603506f7133c9c715eb0aed6e6d8af50))
* support hue in lua starting loadout ([f77b620](https://github.com/moongate-community/moongate/commit/f77b62049419d1e7dada30458e236e9a5c680ae4))

### Bug Fixes

* align generator toolchain compatibility test ([c32b35c](https://github.com/moongate-community/moongate/commit/c32b35c2c36c65534c5df46fba1e7053dc844cbe))
* improve help gump and map viewer ([49964c2](https://github.com/moongate-community/moongate/commit/49964c2499cafe35188bcad9ea5ebd691f6ea50b))
* remove duplicate scheduled event registration ([89ab560](https://github.com/moongate-community/moongate/commit/89ab560d6b784ebba66b2fb25ed9c1f624615fc6))
* update docker image build toolchain ([1796b28](https://github.com/moongate-community/moongate/commit/1796b281eb6b725bd412f2faa08d24749d3e83ad))

### Build System

* remove nativeaot build and publish flow ([e055722](https://github.com/moongate-community/moongate/commit/e05572207f07e889cc1816c41e690e860170c519))

## [0.36.0](https://github.com/moongate-community/moongate/compare/v0.35.0...v0.36.0) (2026-03-19)

### Features

* add async intelligent npc dialogue scheduling ([53e56f9](https://github.com/moongate-community/moongate/commit/53e56f92ad0b40e9b541f572a5c00c6dae83ec08))
* add async lua job module ([cfb05a5](https://github.com/moongate-community/moongate/commit/cfb05a553a2e4108aa3ca53ace6f9a493f353e62))
* add authored npc dialogue runtime ([55eeb20](https://github.com/moongate-community/moongate/commit/55eeb20df83f81e5a39902986d4daf94c7c6ba90))
* add classic vendor buy and sell flow ([ba57f20](https://github.com/moongate-community/moongate/commit/ba57f2001b8f5a0651a6776ee1c709293c4234e3))
* add combat sound and lua combat hooks ([de7ff72](https://github.com/moongate-community/moongate/commit/de7ff7281f52f6ccd82838de97ea9490cf3aeebe))
* add combat system v1 ([47d2e6a](https://github.com/moongate-community/moongate/commit/47d2e6a3f0ff9fca255865d5b70daea12dd1b2ff))
* add Lua banker context menu ([256ab13](https://github.com/moongate-community/moongate/commit/256ab1335ccd81041fb718a997f8bad9b076b0a3))
* add Lua plugin loading support ([210846b](https://github.com/moongate-community/moongate/commit/210846b2390207a39991811c4856442b70d4cc27))
* add Lua-backed help button gump ([f9def47](https://github.com/moongate-community/moongate/commit/f9def47524bc1074eb366898cda50d8982b1b8f4))
* add npc corpse death flow ([0b959f6](https://github.com/moongate-community/moongate/commit/0b959f6f92f94c2fa2708f4c536a479e446a207d))
* add real mount token flow and guard templates ([eb6444a](https://github.com/moongate-community/moongate/commit/eb6444a476cde5eac5f9b0e2d269326f2863c12b))
* add seller npc templates ([62aa671](https://github.com/moongate-community/moongate/commit/62aa6712c25c9aee29fdc220ccc8944c5912a817))
* add speech shorthand for yell and whisper ([627687a](https://github.com/moongate-community/moongate/commit/627687a83efd029fdf3edc6f35bafaf582302c30))
* add world emote speech support ([38e0472](https://github.com/moongate-community/moongate/commit/38e0472c72c54691a5088946425c5a904b69d5f5))
* extend lua speech helpers and shorthand docs ([01e0bdb](https://github.com/moongate-community/moongate/commit/01e0bdbdfa8fe438c660dbb1cebec158c3d43a76))
* extend npc death and admin kill tooling ([6977a5b](https://github.com/moongate-community/moongate/commit/6977a5bc5d4570885fea7decea4250eb5af625a3))
* move npc memories to runtime storage ([7ecb755](https://github.com/moongate-community/moongate/commit/7ecb755daf23d8e4921d7e9066167406d92c7b45))
* scale melee damage with core combat stats ([47e6543](https://github.com/moongate-community/moongate/commit/47e65430bc975b68cd052385158ec7f2599a5905))

### Bug Fixes

* align player visibility sync with view range ([c8ee49f](https://github.com/moongate-community/moongate/commit/c8ee49fea3c5d01fea13f5ca4414888d04ab00df))
* anchor live player markers to map content ([6f90f3c](https://github.com/moongate-community/moongate/commit/6f90f3c65d3f7b6098af518cfe156dadd91890b5))
* correct Discord webhook workflow conditions ([ceedf8f](https://github.com/moongate-community/moongate/commit/ceedf8f332b75e4c70fb4d0a211286e463e38e09))
* harden docker image build and healthcheck ([0158d56](https://github.com/moongate-community/moongate/commit/0158d5639becd455b4c56fd488dd197f16e5f56f))
* queue npc dialogue listener requests ([a0cb301](https://github.com/moongate-community/moongate/commit/a0cb301b742d0d615b92b67059cb9293a119ec90))
* realign npc dialogue benchmark with service constructor ([f17b7a3](https://github.com/moongate-community/moongate/commit/f17b7a3e589c71cdedd406708aaa6d05ab20eba4))
* reduce login sector sync stalls ([ce13a1f](https://github.com/moongate-community/moongate/commit/ce13a1f25291ab52be159b8268ade3f11bb07693))
* refresh backpack after vendor transactions ([c42b90d](https://github.com/moongate-community/moongate/commit/c42b90df051caabcb6e9a8e7ab9e9656cfaa365d))
* resolve mobile notoriety per viewer ([ae0e374](https://github.com/moongate-community/moongate/commit/ae0e37439d96f690b3194a77b696514503278012))

## [0.35.0](https://github.com/moongate-community/moongate/compare/v0.34.0...v0.35.0) (2026-03-15)

### Features

* add Discord changelog webhook release posting ([b3e9193](https://github.com/moongate-community/moongate/commit/b3e91932c7c38eac336876477acde0b00b0fabdd))

### Bug Fixes

* improve teleport map sync and sector alignment ([1f58297](https://github.com/moongate-community/moongate/commit/1f582973472e283c1654e26e25da092bbd327974))
* tighten teleport visual sync and cold-sector player resolution ([3d7ca49](https://github.com/moongate-community/moongate/commit/3d7ca49c3e874ece7c6c6d853a3525595b9538ca))
* **tooltip:** remove invalid mobile vital clilocs ([987ccc0](https://github.com/moongate-community/moongate/commit/987ccc0cf83624f172232913a0c02694ef83a85b))

## [0.34.0](https://github.com/moongate-community/moongate/compare/v0.33.0...v0.34.0) (2026-03-14)

### Features

* **books:** finalize client 7 writable book flow ([869ece6](https://github.com/moongate-community/moongate/commit/869ece6e49a12e212307ed503b62e71b291feaed))
* **chat:** add runtime conference chat system ([8533b26](https://github.com/moongate-community/moongate/commit/8533b266b43de0bfead5e8268b922bbabed97116))
* **doors:** add shared lock ids and key generation ([1f0f7d9](https://github.com/moongate-community/moongate/commit/1f0f7d9e7e3fafd712e61a05021b5fa2741967df))
* **doors:** add targeted door placement command ([9b49d38](https://github.com/moongate-community/moongate/commit/9b49d38f3daea84f4ef5a881f98797156dea3d58))
* **doors:** persist and apply door facing metadata ([e1c86c4](https://github.com/moongate-community/moongate/commit/e1c86c400258dedc066bdc597a6e096f48c1ad2b))
* **gameplay:** add dye window flow and proximity spawners ([ca0e1c0](https://github.com/moongate-community/moongate/commit/ca0e1c0257fdf07acbf6e578eb2e29855c3d2641))
* improve persistence autosave and cross-map client sync ([27d26cf](https://github.com/moongate-community/moongate/commit/27d26cf2bbfe0c629b406fb972b1ffa55837c95e))
* **interaction:** add bulletin board flow and mod name command ([279e381](https://github.com/moongate-community/moongate/commit/279e38125f3540d92d63ebe826cfd51aef80adb5))
* **portal:** add character inventory and bank tables ([21ab196](https://github.com/moongate-community/moongate/commit/21ab196b2349be1ba7e494d89a3e63afd13b8604))
* **portal:** make portal dark-only and fantasy ([d5cd013](https://github.com/moongate-community/moongate/commit/d5cd0138a73337da22300d307a1a16202444cab6))
* **spawn:** add initial world spawn command ([2c02326](https://github.com/moongate-community/moongate/commit/2c0232658a668770befb520fb1aa146eec3291d8))

### Bug Fixes

* **doors:** sync clone location after MoveItemToWorldAsync ([77e51d9](https://github.com/moongate-community/moongate/commit/77e51d94dba11f2ff447c7d6712f859965edb99f))
* **doors:** use linked door native offset ([e16af32](https://github.com/moongate-community/moongate/commit/e16af3214fcf5eb7bc5caaaa37603357439c09f4))
* **images:** crop item art transparent borders ([9f70ee9](https://github.com/moongate-community/moongate/commit/9f70ee94246b6924787aff7e83eb789ef9e7a4bd))
* **network:** zero world item facing in object info ([a5b4215](https://github.com/moongate-community/moongate/commit/a5b42157af9289c5cf6bfd4ca761049f55dde6de))
* restore tests badge and resolve persistence bootstrap wiring ([cd1990e](https://github.com/moongate-community/moongate/commit/cd1990e90bede01f64bcd6d9e9670e52a29eecbe))
* **tooltip:** correct mobile vital cliloc ids ([2a2bc40](https://github.com/moongate-community/moongate/commit/2a2bc40af023634d96f1a72acdab3bc6738bdaf0))

## [0.33.0](https://github.com/moongate-community/moongatev2/compare/v0.32.0...v0.33.0) (2026-03-12)

### Features

* **accounts:** add password command ([c02f78a](https://github.com/moongate-community/moongatev2/commit/c02f78adc6e606ac37f2003f3a7f0924a61d28bf))
* add lua brain metrics and grafana throughput panel ([0a0364e](https://github.com/moongate-community/moongatev2/commit/0a0364ecf16d3b08da4b6a621b6dce0da95239c5))
* add random.element for Lua tables ([22e4904](https://github.com/moongate-community/moongatev2/commit/22e4904cc4c2712227fd885194878410fcea5589))
* **books:** add writable book flow and readonly tag ([ebfca1d](https://github.com/moongate-community/moongatev2/commit/ebfca1da0f82fec6e506343088205a3771ccf683))
* **di:** register MapImageService and wire into HTTP service ([5ac88f3](https://github.com/moongate-community/moongatev2/commit/5ac88f395c728e6c11fdbfa8fb23f90ae6244676))
* **gm-body:** make bag contents template-driven and add skullcap ([17d566c](https://github.com/moongate-community/moongatev2/commit/17d566cea38231773d5d4b689841a69a512b77b1))
* **http:** add GET /api/maps/{mapId}.png endpoint with file cache ([0da2f0b](https://github.com/moongate-community/moongatev2/commit/0da2f0b951369824e0f40992050776fe252d5e57))
* **http:** wire IMapImageService into HTTP route context ([7392779](https://github.com/moongate-community/moongatev2/commit/73927790fac0ada8e1af8b456b5a1764432d6374))
* **items:** add flippable templates and targeted flip command ([3b3a654](https://github.com/moongate-community/moongatev2/commit/3b3a654333c5559363c7c0daf96164f8547b4677))
* **items:** add readonly books and harden debug console startup ([36c1951](https://github.com/moongate-community/moongatev2/commit/36c19516dd536808dd61fe9bb3a573811e950aa5))
* **items:** add typed combat stats and modifiers ([7b58087](https://github.com/moongate-community/moongatev2/commit/7b580875e3351aa3ad218860d155746add993c3a))
* **maps:** add IMapImageService interface ([9ca3e27](https://github.com/moongate-community/moongatev2/commit/9ca3e273fbe1ac9c6a8f063c244d7eb5a816a5d8))
* **maps:** add MapImageService radar-color renderer ([1b22a6a](https://github.com/moongate-community/moongatev2/commit/1b22a6acc4404f43d26943979a8fde116a8d985b))
* **maps:** show online players as markers on the map viewer ([df98c07](https://github.com/moongate-community/moongatev2/commit/df98c07c992be164d39b9346798e1289c3a9611d))
* **mobile:** persist status fields and expose runtime gold total ([ff76477](https://github.com/moongate-community/moongatev2/commit/ff76477fdfbe4ac369476d2821115952b8d855d2))
* **mobiles:** add typed mobile state and effective modifiers ([10968fa](https://github.com/moongate-community/moongatev2/commit/10968faf8d958600aa4447f031f0696d0772e845))
* **mobiles:** persist skills and answer request skills ([cbec8aa](https://github.com/moongate-community/moongatev2/commit/cbec8aad2718ffed6f511e3d7ecf2ca3ce04922b))
* **network:** modernize player status packet ([c066414](https://github.com/moongate-community/moongatev2/commit/c0664147fc464b1fc3944404c7d1b1341459e9f9))
* **portal:** add authenticated password change ([a589b3f](https://github.com/moongate-community/moongatev2/commit/a589b3f2565bae2a79e1fc53173f8e5af671b6c6))
* **portal:** add branding, localization, and profile navigation ([3307cc1](https://github.com/moongate-community/moongatev2/commit/3307cc1cfc785ed7f2766b17e423880da2d8cda8))
* **portal:** add player account portal ([99d7613](https://github.com/moongate-community/moongatev2/commit/99d76134c774bcdda84d085bade94b5e212b37cb))
* **scripting:** add behavior-based NPC brains and Lua runtime modules ([c8e45d8](https://github.com/moongate-community/moongatev2/commit/c8e45d823a2b133e3eb8fbfc30facc20b09865f1))
* **scripting:** add item.spawn helper in ItemModule ([a11961b](https://github.com/moongate-community/moongatev2/commit/a11961beec320d1f436bfdb75776ba0374273412))
* **scripting:** add text template rendering for gumps ([c9db5b6](https://github.com/moongate-community/moongatev2/commit/c9db5b63efeb875bda0e6edc8e0e23aec5547a2f))
* **scripting:** support hash comments in text templates ([f77466f](https://github.com/moongate-community/moongatev2/commit/f77466f8e965b26e34fff5aa3c5ab009e56b17f6))
* **server:** add sell profiles and custom context menu pipeline ([f6773e7](https://github.com/moongate-community/moongatev2/commit/f6773e7505f5cf805cfde1890d3ab0b67dffa0c3))
* **tiles:** add RadarCol reader for radarcol.mul ([7feea44](https://github.com/moongate-community/moongatev2/commit/7feea44b571009f440bd2aee7c73c5798e98e495))
* **ui:** add crosshair with UO map coordinates on hover ([b1b3e8c](https://github.com/moongate-community/moongatev2/commit/b1b3e8cecb3caaec1154b940fe5d0257ec23a16a))
* **ui:** add Maps route and sidebar entry ([88cf56e](https://github.com/moongate-community/moongatev2/commit/88cf56e40d83bbace093cda0b73800852e3521c4))
* **ui:** add MapsPage with zoom/pan map viewer ([872d803](https://github.com/moongate-community/moongatev2/commit/872d80347a08b558afa9f89120a3ed8fe2f5f218))
* **ui:** install react-zoom-pan-pinch for map viewer ([b4cdd9f](https://github.com/moongate-community/moongatev2/commit/b4cdd9fafefd6505826cfa4b8bae7e29e30db4c9))
* **ui:** show resolved container contents in item details ([458e444](https://github.com/moongate-community/moongatev2/commit/458e4449d7e3e81d3c716a6ec722848fa3462b08))

### Bug Fixes

* **doors:** mirror linked double-door opening direction ([2a8b601](https://github.com/moongate-community/moongatev2/commit/2a8b6018d2dd969eb73d00268a6caaef574db2d7))
* **items:** align book tooltip cliloc and page requests ([dfa7f50](https://github.com/moongate-community/moongatev2/commit/dfa7f5032fb782758ca7e7b4a3f9834bf9f6dff3))
* **movement:** resync map change draw packets for teleport transitions ([48f8c14](https://github.com/moongate-community/moongatev2/commit/48f8c14ad1d5b8b3c92711f921d6a8cfd1f54a71))
* **server:** add macOS codesign entitlements for debugger attach ([9a582c0](https://github.com/moongate-community/moongatev2/commit/9a582c0ff5fcc41dcc668893fc19c6f0c87576cf)), closes [dotnet/runtime#125484](https://github.com/dotnet/runtime/issues/125484)
* **server:** preserve equipped items on position save and resolve container gump fallback ([3154ce0](https://github.com/moongate-community/moongatev2/commit/3154ce0396bcc2c56fd5e578421ef0adc3931240))
* **templates:** remove duplicate gm_robe ([75f7ee5](https://github.com/moongate-community/moongatev2/commit/75f7ee56033fd72fb7d57af52b9e19ccb5a8abf6))
* **tooltips:** correct item name cliloc and document cliloc audit ([0971550](https://github.com/moongate-community/moongatev2/commit/09715500cde1fba768a5107a41dd82485c3c36da))
* **world:** align internal map registration index ([deecae9](https://github.com/moongate-community/moongatev2/commit/deecae954c671117ba8a02b7c54cec17e1442a95))
* **worldgen:** preserve decoration item graphics and add gm robe ([19d5b7d](https://github.com/moongate-community/moongatev2/commit/19d5b7d751558495f849e9fa390cd44349948b4d))

## [0.32.0](https://github.com/moongate-community/moongatev2/compare/v0.31.0...v0.32.0) (2026-03-09)

### Features

* add generic_npc mobile template ([3c726dd](https://github.com/moongate-community/moongatev2/commit/3c726dd52dd2d61132237778e4adb664b68e8478))
* add mobile animation event dispatch and packet support ([9719ad3](https://github.com/moongate-community/moongatev2/commit/9719ad32acf41362eb124136fe22046b07714bac))
* add reload_template command and set lilly npc id ([4dfa023](https://github.com/moongate-community/moongatev2/commit/4dfa02376dad788b7988940be205a0e38fea2fd0))
* add runtime spawn service for active sectors ([eac7144](https://github.com/moongate-community/moongatev2/commit/eac7144f5dd8814b82120d238d5568b518d11914))
* add semantic animation intent mapping ([2010df1](https://github.com/moongate-community/moongatev2/commit/2010df17f169f6e5338511ad172faa14ffb32b73))
* add spawn Lua module and update docs ([f060b3d](https://github.com/moongate-community/moongatev2/commit/f060b3da505055a3de28372967c4013fbe6d1dfb))
* add spawner spawn events and harden spawn tools flow ([64d701e](https://github.com/moongate-community/moongatev2/commit/64d701ea40e6cb4940851bfd2a4daf9f54c97f55))
* add use_animation helpers to LuaMobileProxy ([1375f3e](https://github.com/moongate-community/moongatev2/commit/1375f3e8e7744304f28ef2d6031bd9b358fd1015))
* add Vega cat command and Lua brain ([26fdc97](https://github.com/moongate-community/moongatev2/commit/26fdc97719914896886d513bbd7c45a0f5561cb0))
* **data:** add spawn tools gump and teleporter seed dataset ([c12a7fd](https://github.com/moongate-community/moongatev2/commit/c12a7fdaed33a65a615925783cd22a93f909dc32))
* **items:** add account-based item visibility and spawner base template ([e705e88](https://github.com/moongate-community/moongatev2/commit/e705e88d2a83860ce1bd3b6fa26a24bdb4dd8072))
* **lua-brain:** add in_range and out_range callbacks ([4b6a7b9](https://github.com/moongate-community/moongatev2/commit/4b6a7b9343c050331b9252f7df5d08b2fb3521d4))
* pass spawner walking range to lua mobile brain ([a43f870](https://github.com/moongate-community/moongatev2/commit/a43f870c5fa47c51c0cf250710af690db6880443))
* **scripting:** add map/convert modules and refactor teleport item script ([9310c73](https://github.com/moongate-community/moongatev2/commit/9310c73e44cf2cabf17b48fa4d61313dc39c8a27))
* **templates:** add noble queen outfit and Lilly NPC ([9e1c5be](https://github.com/moongate-community/moongatev2/commit/9e1c5bebb2c38f77c4448a2deb7cc560f7881256))
* **world:** import spawns and teleporters seed data with loaders/services ([5b73322](https://github.com/moongate-community/moongatev2/commit/5b73322ea8f0064b19f30f2c6cca2f325226a664))

### Bug Fixes

* align linked door toggle semantics and simplify spawn counting ([b77f641](https://github.com/moongate-community/moongatev2/commit/b77f641e3ce505ecc05b6ce6c8a9a7343097a93b))
* **benchmarks:** update SpatialWorldService benchmark for teleporter dependency ([70b726c](https://github.com/moongate-community/moongatev2/commit/70b726c6a50e42e4ab023b82172f23ce9190a67b))
* handle opened door offset for static collision checks ([0d40c49](https://github.com/moongate-community/moongatev2/commit/0d40c492ace6743b954d78ea0870da599ac448d7))
* restore gump callback wiring and decouple lua brain runner ([f95fb89](https://github.com/moongate-community/moongatev2/commit/f95fb897b4f199dc20a9ee831807162337fc8b43))
* send map change even when destination sector is missing ([72e9358](https://github.com/moongate-community/moongatev2/commit/72e93583c302cd989ef98b73b5eacbe861392ea5))
* **teleport:** force position event on map-change and bypass throttle ([3f8a01d](https://github.com/moongate-community/moongatev2/commit/3f8a01ddb2a8e1a9675d5b343f470e0d01eba55e))
* update benchmark mobile service stub for try spawn api ([09d169f](https://github.com/moongate-community/moongatev2/commit/09d169f68637edb20687b4eb96913811fccaa3f6))

## [0.31.0](https://github.com/moongate-community/moongatev2/compare/v0.30.0...v0.31.0) (2026-03-06)

### Features

* **persistence:** add bulk upsert for items with batched journal writes ([ed2a2e1](https://github.com/moongate-community/moongatev2/commit/ed2a2e10b954c381367dd05b46c9f392bca7ab90))

### Bug Fixes

* align door toggle with ModernUO item+location behavior ([541b4ca](https://github.com/moongate-community/moongatev2/commit/541b4ca8a52af6d7c777f0ece992023e3fd7b2d5))
* delta sector sync, bulk persistence perf, rename emulator to server ([b68e4fc](https://github.com/moongate-community/moongatev2/commit/b68e4fc0b3b3464aad916bd406d848645155a140))

## [0.30.0](https://github.com/moongate-community/moongatev2/compare/v0.29.0...v0.30.0) (2026-03-06)

### Features

* expand sector updates and template params across server ([5fd65c0](https://github.com/moongate-community/moongatev2/commit/5fd65c03aaac77d66b0f37723fa755de2639b9dc))
* **lua:** add eclipse command with global light override ([05194b8](https://github.com/moongate-community/moongatev2/commit/05194b8217b0539ad4be0a395eca220fba18b896))
* **persistence:** migrate persistence serialization to MessagePack source-gen ([08f1d7a](https://github.com/moongate-community/moongatev2/commit/08f1d7a606d9381c64422490bb4b2018e9f716af))
* port ModernUO-style light cycle with spatial/region overrides ([e134f0f](https://github.com/moongate-community/moongatev2/commit/e134f0f6a4ac29ceba7e47ad34966b7d7acde577))
* **world:** add door data pipeline and runtime door toggling ([e7f2e79](https://github.com/moongate-community/moongatev2/commit/e7f2e79b7c36c4bdc01212e03e2b4eea9201b0d6))

### Bug Fixes

* **items:** correct ItemMovedEvent handler loading wrong entity ([b2539f8](https://github.com/moongate-community/moongatev2/commit/b2539f87a3cf81a150125c7d5d6f1bfa4e98389a))
* remove player self-resync on movement and add teleports gump ([91dd4e0](https://github.com/moongate-community/moongatev2/commit/91dd4e0f6deef7dd24646e451c19aaef34d5a965))

### Performance Improvements

* **characters:** batch load missing equipment items instead of N+1 queries ([7cfdb0e](https://github.com/moongate-community/moongatev2/commit/7cfdb0e2cbbdd3d85c247019909824aaf8fb59a5))
* **events:** reduce per-event allocations in GameEventBusService ([19c5aa9](https://github.com/moongate-community/moongatev2/commit/19c5aa95fae3c62efbd66ed11eeadb0d150aaa92))
* **items:** add early exit for ItemDeletedEvent when source owns container ([7e9dbec](https://github.com/moongate-community/moongatev2/commit/7e9dbece00760735ba83e70d87923aa88d414eac))
* **login:** eliminate redundant character loads during login ([998d5cd](https://github.com/moongate-community/moongatev2/commit/998d5cd9e04f3a63e53234224652664dbc953cb2))
* **login:** use Count property instead of GetAll().Count to avoid array allocation ([7726461](https://github.com/moongate-community/moongatev2/commit/772646136737148fa05a710af7f3e2ff464e6835))
* **mobile:** delta sector sync on movement + packet handler performance docs ([02a0d1c](https://github.com/moongate-community/moongatev2/commit/02a0d1cafc9badd8aa8ec36eceed6931df4c1370))
* **movement:** convert event publishing to fire-and-forget instead of blocking ([c65c986](https://github.com/moongate-community/moongatev2/commit/c65c986523c22915156d352beb0cfe0bc5716385))
* **packets:** remove sync-over-async and allocations from PacketDispatchService ([5723991](https://github.com/moongate-community/moongatev2/commit/57239919d4652c37e08b6c892cb3fc852c40a69a))
* **persistence:** eliminate LINQ allocations in SnapshotMapper.ToMobileSnapshot ([7546b65](https://github.com/moongate-community/moongatev2/commit/7546b655befde1f7edd30279bf0893814263b217))
* **spatial:** convert PublishEvent to fire-and-forget instead of blocking ([dc7d6c9](https://github.com/moongate-community/moongatev2/commit/dc7d6c9a1534c6aba0bcbef2099ee62f8394f4f7))
* **spatial:** deduplicate session-to-character mapping in broadcast ([0b23056](https://github.com/moongate-community/moongatev2/commit/0b230569ac5b9f13e1d60bd2075159a684796ffd))
* **spatial:** proactive sector warmup on player sector change ([e2236e2](https://github.com/moongate-community/moongatev2/commit/e2236e2c3a7c38311ce54d2e111f994b93975ef6))
* **spatial:** use O(1) reverse lookup for NPC resolution instead of sector scan ([0b0ff33](https://github.com/moongate-community/moongatev2/commit/0b0ff3390c4514d79b8f818f267b4a856c40f247))

## [0.29.0](https://github.com/moongate-community/moongatev2/compare/v0.28.0...v0.29.0) (2026-03-05)

### Features

* add Lua command.execute bridge with output capture ([ac0443e](https://github.com/moongate-community/moongatev2/commit/ac0443ed3f083f344d7b3aad0f81c514a2e0588e))
* **config,scripting:** support env overrides and snake_case userdata ([8c98a0e](https://github.com/moongate-community/moongatev2/commit/8c98a0e7a391bbf5412ec0f494f7bc9f6fe899e0))
* extend lua gump layout flow and scripting runtime modules ([bdd7237](https://github.com/moongate-community/moongatev2/commit/bdd7237e686820314eb8a9e40c5686506a690f8b))
* **network:** capture client hardware info and local bind endpoint in sessions ([625b33c](https://github.com/moongate-community/moongatev2/commit/625b33cd3a2746931cb6bca6fa1326f4671ffe24))

### Bug Fixes

* **commands:** harden lua command module registration and error handling ([482709f](https://github.com/moongate-community/moongatev2/commit/482709ff234312ae9f1971cd67640454c6dae697))

## [0.28.0](https://github.com/moongate-community/moongatev2/compare/v0.27.0...v0.28.0) (2026-03-04)

### Features

* **scripting:** add Lua gump callbacks and item script fallback conventions ([5576974](https://github.com/moongate-community/moongatev2/commit/5576974c69849bb404967fc8e8718b9a8f91ca60))

## [0.27.0](https://github.com/moongate-community/moongatev2/compare/v0.26.0...v0.27.0) (2026-03-04)

### Features

* evolve persistence locking and gameplay/world tooling ([deb76da](https://github.com/moongate-community/moongatev2/commit/deb76da7f349828ce0da7642b8880f8c90100898))
* **server,scripting,network:** add pathfinding and enrich Lua item/mobile scripting ([0c60e29](https://github.com/moongate-community/moongatev2/commit/0c60e293eb8fd5d39d20d373b1abb3d1850a780e))
* **server:** add background job service and seed-data facade ([5e43e28](https://github.com/moongate-community/moongatev2/commit/5e43e2874c7c4167ace97743e6c43f200a15e87b))
* **server:** add server-change teleport flow and lua visual effects ([d6fb4b8](https://github.com/moongate-community/moongatev2/commit/d6fb4b86934b7766ff8b486f076da41ff18883ab))
* **server:** improve npc runtime brain, movement and speech pipelines ([3630b82](https://github.com/moongate-community/moongatev2/commit/3630b820e2c6af11ca8f19cd4e5e5d6b7311f875))
* **world:** import ModernUO datasets and add world data loaders ([86b36c6](https://github.com/moongate-community/moongatev2/commit/86b36c6843c3574b0c13cf7187a3ee818ae0378c))

### Bug Fixes

* **worldgen:** restore door candidate generation flow ([49a874d](https://github.com/moongate-community/moongatev2/commit/49a874d0edd5a375071dc20e799334ba97f9df22))

## [0.26.0](https://github.com/moongate-community/moongatev2/compare/v0.25.0...v0.26.0) (2026-03-03)

### Features

* **benchmarks:** add gameplay hot-path benchmark suites ([bc4eff8](https://github.com/moongate-community/moongatev2/commit/bc4eff8d4d402b1e58a7cf7d7cf534854462eb0a))
* **commands:** refactor command system with source-generated executor registration ([bc47b04](https://github.com/moongate-community/moongatev2/commit/bc47b044e2c9d8c57438f38de484f392fb1536dc))
* **config:** add http website url for email templates ([2e8b842](https://github.com/moongate-community/moongatev2/commit/2e8b842e5c0133e97e2fadf6ecf6bd29d986c45a))
* **email:** add minimal smtp email pipeline ([7d24e08](https://github.com/moongate-community/moongatev2/commit/7d24e085cd0ef4e2f851ba941428e552722bbc73))
* **persistence:** add typed custom properties for item entities ([337e75f](https://github.com/moongate-community/moongatev2/commit/337e75f97cf5074be393066628c7973dab396913))
* **scripting:** add lua brain on_event speech callback ([6e42eac](https://github.com/moongate-community/moongatev2/commit/6e42eacfbfcef2f1eac66098f23891dc5843cc9e))
* **spatial:** add range broadcast API and war mode handling ([ed58614](https://github.com/moongate-community/moongatev2/commit/ed586147da16a7dc1d302ac532d2b48b4025294d))
* **ui,http:** add public server version endpoint and theme switcher ([748210f](https://github.com/moongate-community/moongatev2/commit/748210f43b3ad443c51a0fa4cdbb93d803b5c3ec))
* **world:** add player-local door spawn and item sector sync ([0059f84](https://github.com/moongate-community/moongatev2/commit/0059f84c980fd9ceafabe4c519c37b9d58c42563))

## [0.25.0](https://github.com/moongate-community/moongatev2/compare/v0.24.0...v0.25.0) (2026-03-02)

### Features

* **http,ui:** extend admin APIs and ship shell-like console UX ([4a518ec](https://github.com/moongate-community/moongatev2/commit/4a518ec7c09ecb6955ba14e15d31576c35e3a2a1))
* **http:** expose paged item templates and lazy item image endpoint ([da5a257](https://github.com/moongate-community/moongatev2/commit/da5a25735eae69c2234cb006ae95c26e43506014))
* **ui:** add item templates page with paginated preview gallery ([b0e1eb9](https://github.com/moongate-community/moongatev2/commit/b0e1eb940e3fff06da1314cf3480e3bf18e78c98))
* **world:** add items image generator pipeline and UO art export ([df9180b](https://github.com/moongate-community/moongatev2/commit/df9180b9d5240726e6f1aee8852518f450487840))

### Bug Fixes

* **commands:** prevent duplicate console command event handling ([df3c8f1](https://github.com/moongate-community/moongatev2/commit/df3c8f1c1cdbfec8bd917ebe7db65aa0ad2971ef))

## [0.24.0](https://github.com/moongate-community/moongatev2/compare/v0.23.0...v0.24.0) (2026-03-02)

### Features

* **spatial:** adopt lazy chunk-style sector streaming for entities ([4d48854](https://github.com/moongate-community/moongatev2/commit/4d488543ce07cff4b486eba7d19932ff004ce897))
* **world:** add generator pipeline and door scan workflow ([6d1151d](https://github.com/moongate-community/moongatev2/commit/6d1151d5961767f2efb75917f233c90888417780))

## [0.23.0](https://github.com/moongate-community/moongatev2/compare/v0.22.0...v0.23.0) (2026-03-01)

### Features

* **converters:** add ModernUO items to Moongate JSON exporter ([65534bc](https://github.com/moongate-community/moongatev2/commit/65534bc3e28e75dcd71f91925594def76ad322ff))
* **data:** add moongate_data root directory structure ([5811c0a](https://github.com/moongate-community/moongatev2/commit/5811c0a7f6defcf75f7a2ba9d3e90831432245b1))
* **server:** add client version handling and chat/view-range protocol flows ([ecbec45](https://github.com/moongate-community/moongatev2/commit/ecbec455367aa6df9d67b150cb006191bfbb85b7))
* **server:** handle general information subcommands 0x06 0x1A 0x2C 0x2D 0x2E ([d4375e0](https://github.com/moongate-community/moongatev2/commit/d4375e094ee0d741b2f9dd2a7344d9a53b15dc15))

## [0.22.0](https://github.com/moongate-community/moongatev2/compare/v0.21.0...v0.22.0) (2026-02-28)

### Features

* **items:** improve drag-drop flow and bank container handling ([2104edd](https://github.com/moongate-community/moongatev2/commit/2104edd4953a4990d5ea14906602e4260a035649))
* **network:** add effect packets and effects factory ([d0a5f3e](https://github.com/moongate-community/moongatev2/commit/d0a5f3e5c42fd3df519e22efaaac1547bbec6379))
* **scripting:** add lua brain runtime with npc speech dispatch ([4211266](https://github.com/moongate-community/moongatev2/commit/4211266d1573b27d70bbb14aee74f2ee236ff3a2))
* **spatial:** expose region lookup by id in spatial world service ([319ab7a](https://github.com/moongate-community/moongatev2/commit/319ab7a094e0b21e280dafa05c0e6dfe9f32f86b))

### Bug Fixes

* **movement:** validate server-side movement with z resolution and collision checks ([7590695](https://github.com/moongate-community/moongatev2/commit/7590695baafd461f2ee65d67c9ec91cd95331ff7))
* preserve existing container items when moving item into backpack ([4771ffe](https://github.com/moongate-community/moongatev2/commit/4771ffe4462c11c42862d3f96b6c75578ce56a3d))

## [0.21.0](https://github.com/moongate-community/moongatev2/compare/v0.20.0...v0.21.0) (2026-02-27)

### Features

* **spatial:** add polymorphic regions with typed map and music support ([1f20006](https://github.com/moongate-community/moongatev2/commit/1f20006402fef1df68ee40d730a9331a18da4edb))
* **spatial:** resolve regions by priority and parent hierarchy ([637f131](https://github.com/moongate-community/moongatev2/commit/637f131ce10ff58affe6036e4653f4a043e737fb))

## [0.20.0](https://github.com/moongate-community/moongatev2/compare/v0.19.1...v0.20.0) (2026-02-27)

### Features

* **items:** add template spawn and session-aware move events ([8232634](https://github.com/moongate-community/moongatev2/commit/82326343306c067f65004a57b908de3cba519458))
* **items:** add TryToGetItemAsync to IItemService ([ac5f220](https://github.com/moongate-community/moongatev2/commit/ac5f220aa439448625f36b5250d60a15776970d2))
* **server:** add item script dispatcher and item ScriptId flow ([a696262](https://github.com/moongate-community/moongatev2/commit/a6962628ce54cc575bc5c95214b4f14c9a63ad05))
* **server:** generate Lua user-data bootstrap registrations ([c2cd4cc](https://github.com/moongate-community/moongatev2/commit/c2cd4cc574d08df19267124ee1244b0750f386cf))

### Bug Fixes

* align mobile enter-range packets with modernuo flow ([2898c5f](https://github.com/moongate-community/moongatev2/commit/2898c5faa080ef144e0e9f4d5e4ebd24829fc660))

## [0.19.1](https://github.com/moongate-community/moongatev2/compare/v0.19.0...v0.19.1) (2026-02-26)

### Bug Fixes

* restore RegisterGameEventListener bootstrap subscription and listener startup wiring ([014bf6a](https://github.com/moongate-community/moongatev2/commit/014bf6aca3e78553a4345112fdf651a1b9982401))

## [0.19.0](https://github.com/moongate-community/moongatev2/compare/v0.18.0...v0.19.0) (2026-02-26)

### Features

* **converters:** add UOX3 DFN item and loot converters ([ee11847](https://github.com/moongate-community/moongatev2/commit/ee118470a15b8138a5d0278b84330066b00a5ed1))
* **converters:** unify DFN pipeline and improve creature mapping ([d4406c8](https://github.com/moongate-community/moongatev2/commit/d4406c82db139c68a2606376fa62706852d0dff9))
* **network:** implement target cursor commands packet (0x6C) with tests ([a505e9c](https://github.com/moongate-community/moongatev2/commit/a505e9ce331233ac9c24cecd3123029983131ac2))
* **scripting:** add lua command module and stabilize target cursor flow ([4ba7566](https://github.com/moongate-community/moongatev2/commit/4ba7566f12b5f34039e26f573e16ea42d81f9b17))
* **scripting:** add lua speech/mobile/item modules and tests ([e27a3f9](https://github.com/moongate-community/moongatev2/commit/e27a3f960c8e05ff2550207ce6948248a523e8cc))
* **server:** generate game-event listener wiring and document it ([9f5d6b9](https://github.com/moongate-community/moongatev2/commit/9f5d6b9e109dcb813dd48850850082b8587fd7bc))
* **targeting:** add player target event flow and player target service ([1556fdc](https://github.com/moongate-community/moongatev2/commit/1556fdc1587ae4b1063f6121d4761e9cc60afe55))
* **templates:** add loot schema, base inheritance, and mobile sounds ([1f65e31](https://github.com/moongate-community/moongatev2/commit/1f65e31232678815e279d699d3fa14a97b0efc6a))
* **uo-data:** add cross-platform item art service with ImageSharp ([c6be3f2](https://github.com/moongate-community/moongatev2/commit/c6be3f2d2a9ad92cda8db92e1f523def69aa187a))

### Bug Fixes

* **server:** register mobile handler and align docker http port exposure ([2879543](https://github.com/moongate-community/moongatev2/commit/2879543f2bc78fa97d1b351b8b9733357cd0f7dc))
* **server:** restore mobile spawn service and orion target command flow ([a2ed9bc](https://github.com/moongate-community/moongatev2/commit/a2ed9bc2ca5b5913a8b9bf7f5acb2cebc6fe1bde))

## [0.18.0](https://github.com/moongate-community/moongatev2/compare/v0.17.0...v0.18.0) (2026-02-25)

### Features

* **server:** add persistence events and play sound effect packet ([ffb1512](https://github.com/moongate-community/moongatev2/commit/ffb1512114d434d837a72abc2345684d60945bea))
* **ui:** add api fetch client with auth header injection ([0dbce72](https://github.com/moongate-community/moongatev2/commit/0dbce724865e48b491444628547301b1ea6c674b))
* **ui:** add dashboard page with health check ([581c5b5](https://github.com/moongate-community/moongatev2/commit/581c5b54f0fad167482487ca6cfec975b2731b55))
* **ui:** add login page with jwt auth ([cc3c2ad](https://github.com/moongate-community/moongatev2/commit/cc3c2adc1eb8f1ff8b9431f4fb80c5152c32aee9))
* **ui:** add react router with protected route ([f0a1be8](https://github.com/moongate-community/moongatev2/commit/f0a1be8d7a06dba463601d38a2571c4a6d67096b))
* **ui:** add sidebar and app layout ([7b3939c](https://github.com/moongate-community/moongatev2/commit/7b3939cc50154242a9465b154ea8f96aa9d2721d))
* **ui:** add vite proxy /api -> localhost:8088 ([4732a05](https://github.com/moongate-community/moongatev2/commit/4732a0535889f9694f93523bdc81c3752aff1e1b))
* **ui:** add zustand persisted auth store ([f576f31](https://github.com/moongate-community/moongatev2/commit/f576f3116a044c1b068d1abf0808c9723e292f20))
* **ui:** align theme to moongate docs palette (blue/purple [#6aa5da](https://github.com/moongate-community/moongatev2/issues/6aa5da), [#242130](https://github.com/moongate-community/moongatev2/issues/242130)) ([788904b](https://github.com/moongate-community/moongatev2/commit/788904b5d02058578cab59dd53a85f2cf456d6e0))
* **ui:** apply arcane terminal design with amber theme, cinzel font, heroui dark colors ([b97c241](https://github.com/moongate-community/moongatev2/commit/b97c24186236dd4aa38637079ca05496049599eb))
* **ui:** configure heroui with tailwind v4 and dark theme provider ([4fad917](https://github.com/moongate-community/moongatev2/commit/4fad9175a811fafac8cecb7612a796900644269a))
* **ui:** scaffold vite react-ts project with heroui and zustand ([23ab8af](https://github.com/moongate-community/moongatev2/commit/23ab8af53a150307567de8effd9f7208688aac2e))

### Bug Fixes

* **http:** resolve account service lazily in facades ([c14745d](https://github.com/moongate-community/moongatev2/commit/c14745d425522650564b1188568e6940022ca60a))
* **ui:** add missing ReactNode import in router ([cf8b249](https://github.com/moongate-community/moongatev2/commit/cf8b249924f5c3d591db1a36bde1be3dd78eb24b))
* **ui:** downgrade to tailwind v3 for proper heroui theme support ([c880282](https://github.com/moongate-community/moongatev2/commit/c88028299ce3fd517e0e23e64598d84f38e89ec3))
* **ui:** move dark class to html element as required by heroui v2 ([5256e56](https://github.com/moongate-community/moongatev2/commit/5256e567c48798c5f8cb214e8af42c48b082bdfe))
* **ui:** switch to tailwind v4 per heroui docs, fix source path and plugin setup ([7d35c4d](https://github.com/moongate-community/moongatev2/commit/7d35c4da075781665ec2eb98049e83a491f863c0))

## [0.17.0](https://github.com/moongate-community/moongatev2/compare/v0.16.2...v0.17.0) (2026-02-25)

### Features

* **server:** reorganize events and add spatial lazy sector warmup with item map support ([fc69539](https://github.com/moongate-community/moongatev2/commit/fc695392a73c02a21a09c51aad232743a0f11e46))

## [0.16.2](https://github.com/moongate-community/moongatev2/compare/v0.16.1...v0.16.2) (2026-02-24)

### Bug Fixes

* **scripting:** await Lua metadata generation during startup ([f0db82d](https://github.com/moongate-community/moongatev2/commit/f0db82d4aed92c5fb9304a64d49aa86a844b7753))

## [0.16.1](https://github.com/moongate-community/moongatev2/compare/v0.16.0...v0.16.1) (2026-02-24)

### Bug Fixes

* update benchmark imports for general information packet namespace ([23c2912](https://github.com/moongate-community/moongatev2/commit/23c291298213f95cef04d78cce33d512d4800a3e))

## [0.16.0](https://github.com/moongate-community/moongatev2/compare/v0.15.1...v0.16.0) (2026-02-24)

### Features

* add item interaction packets, spatial metrics, and container item location alignment ([e57c8f7](https://github.com/moongate-community/moongatev2/commit/e57c8f770f6c96eb99f794a77cbd0ad7bfd55083))
* add movement position events with anti-spam and spatial hooks ([bac826a](https://github.com/moongate-community/moongatev2/commit/bac826a117a48a7fda6e1514b536af0fe92c71e8))
* add player character logged in event and coverage tests ([ed97b57](https://github.com/moongate-community/moongatev2/commit/ed97b57ded93c93ec14c07534d4002ea27f21478))
* **scripting,packets:** add gump builder/module and fix lua docs generation ([9a6f219](https://github.com/moongate-community/moongatev2/commit/9a6f219f8bea75f59cf6b144fa634d3016aa5527))

### Bug Fixes

* align sector constants and update wrapped regions data ([450f520](https://github.com/moongate-community/moongatev2/commit/450f520145442737fed4f889df76ac800d122926))
* support drop item grid-byte parsing and align container item locations ([92744e3](https://github.com/moongate-community/moongatev2/commit/92744e3cde7e2fc36a742df0fb777cddc01dd13a))
* update item drop handler for container stacking flow ([3c67b6a](https://github.com/moongate-community/moongatev2/commit/3c67b6ab2597173cbdb6dc9e368d76cc0f82648c))

## [0.15.1](https://github.com/moongate-community/moongatev2/compare/v0.15.0...v0.15.1) (2026-02-23)

### Bug Fixes

* **docker:** add binutils for AOT symbol stripping and update generator paths ([b77baec](https://github.com/moongate-community/moongatev2/commit/b77baec40065afc66d6792832b6f32a848ee706d))

## [0.15.0](https://github.com/moongate-community/moongatev2/compare/v0.14.0...v0.15.0) (2026-02-23)

### Features

* **benchmarks:** add BenchmarkDotNet suite and publish baseline results ([5ce76a2](https://github.com/moongate-community/moongatev2/commit/5ce76a2d0ab690aef62be57b6b955adadc28fa7d))
* **benchmarks:** add jit vs nativeaot comparison runner and docs ([dc85ed1](https://github.com/moongate-community/moongatev2/commit/dc85ed1ce1b23072ce4297ec8a44f683e88e4628))
* **benchmarks:** add parser dispatch and compression benchmark suites ([732d599](https://github.com/moongate-community/moongatev2/commit/732d599bb11bd5a3cefe6b9dffa454009c38a645))
* **server:** add speech server events and shared event clock ([ef0eeab](https://github.com/moongate-community/moongatev2/commit/ef0eeab2595de3927a959ecf0ec7d74941687cce))
* **server:** centralize speech handling and add server broadcast APIs ([a48280b](https://github.com/moongate-community/moongatev2/commit/a48280b792c0746c6212cdb921ff55bc16a0fa90))
* **server:** generate script module registrations and unify game event base ([dced6f8](https://github.com/moongate-community/moongatev2/commit/dced6f8addfc3e2e95662816a339c07010dd8f11))

## [0.14.0](https://github.com/moongate-community/moongatev2/compare/v0.13.0...v0.14.0) (2026-02-23)

### Features

* **commands:** add tab autocomplete and contextual help ([21d3e23](https://github.com/moongate-community/moongatev2/commit/21d3e233d41832d6b684e0b9e0fe164b3917b014))

## [0.13.0](https://github.com/moongate-community/moongatev2/compare/v0.12.0...v0.13.0) (2026-02-22)

### Features

* **commands:** enforce account-type authorization and update docs ([07128f0](https://github.com/moongate-community/moongatev2/commit/07128f0b2957fc59ee3866908ccf1b1fd4588634))
* **item:** persist stackable and rarity metadata ([453a10f](https://github.com/moongate-community/moongatev2/commit/453a10f14fddda4c20f797d4952041befcfb0059))
* **persistence:** persist item name and weight across snapshots ([b0629bf](https://github.com/moongate-community/moongatev2/commit/b0629bf3d5b5c07ec2bdfbcb07d0f1256c2df073))

### Bug Fixes

* **item:** derive stackable flag from mul tile data ([e9dfc2c](https://github.com/moongate-community/moongatev2/commit/e9dfc2c2e643d010050ae3fb00fec971b849a338))

## [0.12.0](https://github.com/moongate-community/moongatev2/compare/v0.11.1...v0.12.0) (2026-02-22)

### Features

* **server:** generate packet listener registrations via attributes ([c2d238b](https://github.com/moongate-community/moongatev2/commit/c2d238bd21e6e3d422ab7c04e9429f3114370854))
* **server:** implement mega cliloc tooltip responses ([2d7dedc](https://github.com/moongate-community/moongatev2/commit/2d7dedc9b4a5b8b9cb067b32919d3cc83165f749))

### Bug Fixes

* send extended player status and correct movement throttle reset ([d30ead8](https://github.com/moongate-community/moongatev2/commit/d30ead86288044f7fb7374af362618b76c57490d))
* update packet log markers and align tests with directory types ([b3ae7da](https://github.com/moongate-community/moongatev2/commit/b3ae7da2125eeefff318f35d1a0eea62a0f522b4))

## [0.11.1](https://github.com/moongate-community/moongatev2/compare/v0.11.0...v0.11.1) (2026-02-21)

### Bug Fixes

* make http endpoints AOT-safe for JSON metadata resolution ([3d74f88](https://github.com/moongate-community/moongatev2/commit/3d74f88e7302e85ef5f78171945ed0a52fd40352))

## [0.11.0](https://github.com/moongate-community/moongatev2/compare/v0.10.0...v0.11.0) (2026-02-21)

### Features

* add gump packet support for 0xdd and 0xb1 ([89673df](https://github.com/moongate-community/moongatev2/commit/89673df0a30faf6cd43c664efb8142fd0bda66ab))
* route in-game command output to speech packets ([795cdff](https://github.com/moongate-community/moongatev2/commit/795cdff94b47eef875818f1c694e5e3f0044b31e))

## [0.10.0](https://github.com/moongate-community/moongatev2/compare/v0.9.1...v0.10.0) (2026-02-21)

### Features

* **http:** add JWT authentication login endpoint and config wiring ([9f3d8eb](https://github.com/moongate-community/moongatev2/commit/9f3d8ebcd94afdf9d36c033fa438efa64e4f47ce))

### Bug Fixes

* **ci:** harden coverage extraction without broken pipes ([4dcb89c](https://github.com/moongate-community/moongatev2/commit/4dcb89cc80d1928df95522321fa489bc5144c1fe))
* **ci:** make coverage and security workflows yaml-safe ([9c80fc9](https://github.com/moongate-community/moongatev2/commit/9c80fc9b0650636e2c43194ab1443937ccad78ee))
* harden http json metadata and stat enum parsing ([6b6ecca](https://github.com/moongate-community/moongatev2/commit/6b6ecca7134523fb66c74d278877ea6fc5133164))

## [0.9.1](https://github.com/moongate-community/moongatev2/compare/v0.9.0...v0.9.1) (2026-02-21)

### Bug Fixes

* **ci:** resolve security workflow yaml parsing ([497e9e0](https://github.com/moongate-community/moongatev2/commit/497e9e07492eff7f049ba61e6244cd61007f9ccc))
* **ci:** stabilize security workflow command parsing ([6b14e2c](https://github.com/moongate-community/moongatev2/commit/6b14e2c2f3761d5b72f27c24334c3456218dbdb8))

## [0.9.0](https://github.com/moongate-community/moongatev2/compare/v0.8.0...v0.9.0) (2026-02-21)

### Features

* **network:** add book packets, property list support, and dynamic test badge ([5aee7e8](https://github.com/moongate-community/moongatev2/commit/5aee7e8c779af2cd878bca60deb6efafbff89c0f))

## [0.8.0](https://github.com/moongate-community/moongatev2/compare/v0.7.10...v0.8.0) (2026-02-21)

### Features

* **network:** add unicode speech pipeline and message factory helpers ([5cd39c4](https://github.com/moongate-community/moongatev2/commit/5cd39c45fe1c0af7cf008422007167e112e9d288))

### Bug Fixes

* **server:** preserve speech type and hue in speech handler ([8e4fea3](https://github.com/moongate-community/moongatev2/commit/8e4fea3dd868beed08fea6c8bb882d6dbd0cfae4))

<p align="center">
  <img src="images/moongate_logo.png" alt="Moongate logo" width="240" />
</p>

All notable changes to this project will be documented in this file. Releases are generated from Conventional Commits with semantic-release.

<a name="0.7.10"></a>
## [0.7.10](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.10) (2026-02-21)

<a name="0.7.9"></a>
## [0.7.9](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.9) (2026-02-21)

<a name="0.7.8"></a>
## [0.7.8](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.8) (2026-02-21)

<a name="0.7.7"></a>
## [0.7.7](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.7) (2026-02-21)

<a name="0.7.6"></a>
## [0.7.6](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.6) (2026-02-21)

<a name="0.7.5"></a>
## [0.7.5](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.5) (2026-02-21)

<a name="0.7.4"></a>
## [0.7.4](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.4) (2026-02-21)

<a name="0.7.3"></a>
## [0.7.3](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.3) (2026-02-21)

<a name="0.7.2"></a>
## [0.7.2](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.2) (2026-02-21)

<a name="0.7.1"></a>
## [0.7.1](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.1) (2026-02-21)

### Features

* add PacketSenderService with dedicated sender thread ([24429c6](https://www.github.com/moongate-community/moongatev2/commit/24429c6dd143e9f2404094edaf4ee4d94b09ed02))
* add synchronous Send method to IOutboundPacketSender ([f943324](https://www.github.com/moongate-community/moongatev2/commit/f9433240bcaae74692a3437f47aaee53f167710c))
* change default tick duration from 250ms to 8ms ([1030f8f](https://www.github.com/moongate-community/moongatev2/commit/1030f8f434abb6a9f8e86b30ef0352d8c4d3b091))
* switch OutgoingPacketQueue to Channel<T> with WaitToReadAsync ([f69a93b](https://www.github.com/moongate-community/moongatev2/commit/f69a93bcedeb172b2f42a85e5a8a5f4690e3f015))
* **loop:** switch to timestamp-driven timer updates with idle cpu throttle ([6f24593](https://www.github.com/moongate-community/moongatev2/commit/6f24593cdbccfaa209f286814edc1a69bec25d3d))
* **metrics:** add mvp loop/network/timer/persistence runtime metrics ([c8b1a08](https://www.github.com/moongate-community/moongatev2/commit/c8b1a086617da1b8e1346b142db3e34b671b472e))
* **metrics:** generate metric samples from annotated snapshots ([d193295](https://www.github.com/moongate-community/moongatev2/commit/d1932951e45a0c259834d01abbfd9b906ad4a46d))
* **network:** parse get player status packet and add tests ([bc80cf2](https://www.github.com/moongate-community/moongatev2/commit/bc80cf2ef4a69eb0fcc552914db5e4db94fa8c00))
* **server:** add movement flow, paperdoll updates, and timer tick alignment ([e57e1ec](https://www.github.com/moongate-community/moongatev2/commit/e57e1ec08d1a1ba3ad83bbcf502dcc358e8f1729))
* **server:** add movement throttling and run/walk speed handling ([a0a1303](https://www.github.com/moongate-community/moongatev2/commit/a0a130381a718e79aa068cf46fa6722b3154f327))
* **status:** implement 0x11 basic player status response ([45abfbd](https://www.github.com/moongate-community/moongatev2/commit/45abfbd45b687fdae8bae4df0296ceb651633981))

### Bug Fixes

* count all executed callbacks in metrics including failures ([63ce044](https://www.github.com/moongate-community/moongatev2/commit/63ce044a692b65b0882c9f92e5920b7ae224966f))
* make BaseMoongateService StartAsync/StopAsync virtual and use override in services ([7ed4293](https://www.github.com/moongate-community/moongatev2/commit/7ed429306699fd120d12c25a033391464c56ab09))
* use Task.WhenAll in packet dispatch instead of fire-and-forget ([b3cba93](https://www.github.com/moongate-community/moongatev2/commit/b3cba93d2b6cb20b3c51e9bc5d49716b156023d4))
* **network:** align movement and mobile incoming hair serialization ([9fb5905](https://www.github.com/moongate-community/moongatev2/commit/9fb5905f9b511f90f8a35bfb2172e5f862775dab))

<a name="0.7.0"></a>
## [0.7.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.7.0) (2026-02-20)

### Features

* expand server/persistence templates, entities and tests ([14214fb](https://www.github.com/moongate-community/moongatev2/commit/14214fbf5c53ed564a98035ea4e1c7616c2feba8))

<a name="0.6.0"></a>

## [0.6.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.6.0) (2026-02-20)

### Features

- **events,uo:** add outbound listener base and runtime equipment references ([2f4c147](https://www.github.com/moongate-community/moongatev2/commit/2f4c14740c3c433f3e2989cf5791ae3a08733875))
- **network:** add ping packet handling and movement/general info packets ([ceb67e8](https://www.github.com/moongate-community/moongatev2/commit/ceb67e83846940c1fd730fc726c8da16c7258bfd))
- **network-packets:** add after-login outgoing packet serialization ([63f75f4](https://www.github.com/moongate-community/moongatev2/commit/63f75f4cabac81067be1612e98bbf9f5320cefc6))
- **server:** implement login character packet flow ([0407571](https://www.github.com/moongate-community/moongatev2/commit/04075715e92256f386c01c7cdeecd2c9a047cee6))

### Bug Fixes

- **logging:** avoid timestamp highlighting collisions in console sink ([c261c89](https://www.github.com/moongate-community/moongatev2/commit/c261c89a330928677b09739159d17d33cce50cb4))

<a name="0.5.0"></a>

## [0.5.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.5.0) (2026-02-19)

### Features

- add console ui logger with prompt and spectre colors ([8e7a9e6](https://www.github.com/moongate-community/moongatev2/commit/8e7a9e6d75898f6a8c4783c58ddb6bc9ee093ed9))
- **console:** add scrollable log history in interactive UI ([45f61f7](https://www.github.com/moongate-community/moongatev2/commit/45f61f728701fd8c295505efd1fb4cba8d1b2327))
- **console:** add startup input lock with unlock flow ([45e7e67](https://www.github.com/moongate-community/moongatev2/commit/45e7e6752e3ddd9ae5a6b835b6d1de9b0d70ced5))
- **network:** add CharactersStartingLocationsPacket (0xA9) ([c3cf68d](https://www.github.com/moongate-community/moongatev2/commit/c3cf68df681ba2d156aa77fbce0da60510414b90))
- **persistence:** add repository count APIs and bootstrap account check ([d46676e](https://www.github.com/moongate-community/moongatev2/commit/d46676ef54363acc85b09d3757caa7fb0cf50e65))
- **scripting:** cache compiled lua chunks ([67af7cb](https://www.github.com/moongate-community/moongatev2/commit/67af7cb18cec0a9fe6a665d705f4c6c8c5a47d35))
- **server:** add command system service and lifecycle shutdown flow ([8651e1a](https://www.github.com/moongate-community/moongatev2/commit/8651e1a1dc9fab3d03a87c6e8a8226884479b6d3))
- **server:** add timer metrics, item/mobile link refs, and docs status refresh ([3378403](https://www.github.com/moongate-community/moongatev2/commit/33784038b91f186d958dacdbbeb15d6903690e51))
- **server:** refactor metrics into snapshot sources ([bf39de0](https://www.github.com/moongate-community/moongatev2/commit/bf39de08782bd9eab2a8d7ad75906d5d74d16788))
- **server:** update persistence/timers, packet mapping and serial parsing fixes ([0974549](https://www.github.com/moongate-community/moongatev2/commit/0974549e25ec3e79921ebb22e23ed4720fd79dae))
- **uo:** add mobile stat recalculation and apply on character mapping ([3d93590](https://www.github.com/moongate-community/moongatev2/commit/3d935908fab8d169afbe2826b49f851c3fb62543))

### Bug Fixes

- align docker publish inputs and slim http builder for aot ([69a68f1](https://www.github.com/moongate-community/moongatev2/commit/69a68f18fe9881689e2874e3bb200803a4718e57))
- **network:** enforce ordered outbound send and always compress post-login ([c6b263b](https://www.github.com/moongate-community/moongatev2/commit/c6b263b72129c6e858b6f3fea30619648e4068f1))

<a name="0.4.0"></a>

## [0.4.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.4.0) (2026-02-19)

### Features

- **network:** add client middleware management and support features packet ([6c8f0fa](https://www.github.com/moongate-community/moongatev2/commit/6c8f0fa74a2545f09d4a62d9d4d3e735c6424d66))

<a name="0.3.0"></a>

## [0.3.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.3.0) (2026-02-19)

### Features

- **http:** add embedded HTTP host service with options and dedicated logging ([033cd6d](https://www.github.com/moongate-community/moongatev2/commit/033cd6df52360aec8d72ae08192baccc942fe507))
- **network:** add client middleware management and support features packet ([6c8f0fa](https://www.github.com/moongate-community/moongatev2/commit/6c8f0fa74a2545f09d4a62d9d4d3e735c6424d66))
- **network:** handle reconnect seed handshake and refine login packet flow ([529dc79](https://www.github.com/moongate-community/moongatev2/commit/529dc79a70d384f2d5bac02e8db82f994d6f0747))
- **packets,scripting:** generate packet definitions and fix Lua log module interop ([71f3658](https://www.github.com/moongate-community/moongatev2/commit/71f3658df446e641ca5139d2c66b5373a35503dc))
- **server:** add http config and json context plumbing ([a94ea67](https://www.github.com/moongate-community/moongatev2/commit/a94ea674dacdeb0398e6e4a16c151ce8bd5c946a))
- **server:** add timer wheel service and game-event script bridge with tests ([de14394](https://www.github.com/moongate-community/moongatev2/commit/de143947a0f5518b965215fe90b1bcf6c34de466))

<a name="0.2.0"></a>

## [0.2.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.2.0) (2026-02-17)

### Features

- **server:** bootstrap bundled data assets into data directory ([2929ce1](https://www.github.com/moongate-community/moongatev2/commit/2929ce1f1a62a64947733a8312f04b150d2a6efc))
- **server:** wire UO file loaders and typed json context ([07551a2](https://www.github.com/moongate-community/moongatev2/commit/07551a2f200a882169af7e588d558a34358da63b))

### Bug Fixes

- **server:** resolve default root directory from app base path ([7aaa217](https://www.github.com/moongate-community/moongatev2/commit/7aaa21719dc5b834476369d2ef827de71b9a1487))

<a name="0.1.0"></a>

## [0.1.0](https://www.github.com/moongate-community/moongatev2/releases/tag/v0.1.0) (2026-02-17)

### Features

- add abstractions, network project, and Obsidian tooling setup ([300d338](https://www.github.com/moongate-community/moongatev2/commit/300d33837531512a083888dc7d03636112b31acd))
- add core and server projects to solution ([d1e2f47](https://www.github.com/moongate-community/moongatev2/commit/d1e2f47efb795c6c1b6b2a19df58578065e02002))
- **abstractions:** add service project wiring and sprint kanban ([d428991](https://www.github.com/moongate-community/moongatev2/commit/d428991c4581cfbd450be2036d574570821b3264))
- **core:** add utility, json, and configuration foundations ([2868a72](https://www.github.com/moongate-community/moongatev2/commit/2868a72d5fa52cc071563383e42177cd775a6a53))
- **network:** add packet registry with generated packet table ([9ba6051](https://www.github.com/moongate-community/moongatev2/commit/9ba60510191e7df730d8758e39a0d5c82481f0b9))
- **network:** add span io and base packet parsing infrastructure ([89be21b](https://www.github.com/moongate-community/moongatev2/commit/89be21bea67b65b57b9649a1aa00ad3803cc185c))
- **network:** add tcp client pipeline, events, and buffer exceptions ([97ff80f](https://www.github.com/moongate-community/moongatev2/commit/97ff80f89d7991828dee4573a558889fec365018))
- **network:** add tcp server foundation with buffers and compression ([d3bed53](https://www.github.com/moongate-community/moongatev2/commit/d3bed53c02d88b83bc1e321a7b99fefbc2a7b91b))
- **packets:** add packet descriptions from attributes and fix aot publish script ([03b6b52](https://www.github.com/moongate-community/moongatev2/commit/03b6b528eb4153b4f1285e94e9c846ed9def5b3a))
- **packets:** organize incoming packets by domain ([aaad0bc](https://www.github.com/moongate-community/moongatev2/commit/aaad0bcc68503b217f78b71a812706430095e582))
- **server:** add lifecycle run loop and align packet metadata ([c24abca](https://www.github.com/moongate-community/moongatev2/commit/c24abca925384b360e4e455f2d9025dae344fdea))
- **server:** add message bus and domain event bus infrastructure ([03f605c](https://www.github.com/moongate-community/moongatev2/commit/03f605cbd1bfd4c2008d601ca24b3d0f9ec870d9))
- **server:** add moongate bootstrap registration ([39ec37e](https://www.github.com/moongate-community/moongatev2/commit/39ec37e718045a4955e8f9177d112a5bf0c72989))
- **server:** add packet data dump logging with dedicated sink ([12c9c81](https://www.github.com/moongate-community/moongatev2/commit/12c9c81b1e0115590bb96bd38f2d1f909cdc259b))
- **server:** add startup header resource ([8f68586](https://www.github.com/moongate-community/moongatev2/commit/8f685865c37d09972dfd399f0e1decee030887fc))
- **server:** implement game loop lifecycle and tests ([853c4fd](https://www.github.com/moongate-community/moongatev2/commit/853c4fd86a3bd0b0a52daa00ff6f8e3c7c3c359c))
- **server:** scaffold game loop service contracts and models ([33f1cbe](https://www.github.com/moongate-community/moongatev2/commit/33f1cbe849a3eee8ad945153377d7defc01bf649))
- **server:** scaffold network packet listener and service contracts ([f1cea7b](https://www.github.com/moongate-community/moongatev2/commit/f1cea7b69e8ab473815438c9ad08fbcd7fb6d9af))
- **server:** wire startup banner and platform handling updates ([6e9af34](https://www.github.com/moongate-community/moongatev2/commit/6e9af3447d8878ac1239ef6277e580f4a4ea77ec))
- **uo-data:** add Serial type and coverage tests ([69f5836](https://www.github.com/moongate-community/moongatev2/commit/69f5836095b265d0d843fbe4d7ba723b98cc8731))
- **uo-data:** import core legacy UO data and add minimal entity model ([4359964](https://www.github.com/moongate-community/moongatev2/commit/43599640fb4b9a84fd0185c78580f876d10d003f))
