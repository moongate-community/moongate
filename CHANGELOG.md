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
