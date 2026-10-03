# Changelog

## [0.13.0](https://github.com/moongate-community/moongate/compare/v0.12.0...v0.13.0) (2026-10-03)


### Features

* A* pathfinding service ([9a0f7d2](https://github.com/moongate-community/moongate/commit/9a0f7d2adc2e6049068ba5fceb0fc94541122bf8))
* **ctl:** convert the named places of ModernUO into locations.toml ([438b317](https://github.com/moongate-community/moongate/commit/438b31799111546da95bc435461ff1a94651a740))
* **ctl:** convert the treasure chests of ModernUO's spawners ([c32184e](https://github.com/moongate-community/moongate/commit/c32184e17bb5b0e864b3791406874a7fcdd18bbc))
* **data:** the content tables of the town containers ([626cbec](https://github.com/moongate-community/moongate/commit/626cbec3f580dd03a0a4a1771097846cc62ac3f2))
* **data:** treasure chests hold piles as ModernUO's ([b7b65f0](https://github.com/moongate-community/moongate/commit/b7b65f05996cd8cfa64fe64d8e0ac19d60ed029e))
* go gump of the named places and travel by name ([3774a04](https://github.com/moongate-community/moongate/commit/3774a0460b9e064e5f6300be7b9f8aa821959b6d))
* ground items block movement, closed doors among them ([730018e](https://github.com/moongate-community/moongate/commit/730018e4a16326288284cb2ccd784e41fcb41913))
* item scripts can refuse a lift, a drop, an equip and an insert ([41a2693](https://github.com/moongate-community/moongate/commit/41a2693088913d5645c93dec7df03219412211f6))
* mobile flags, war mode and shard props from Lua ([3ca3e33](https://github.com/moongate-community/moongate/commit/3ca3e331c30fd19e0abfd2193227f2f6ca28865d))
* **moongates:** make the moongates glow ([f3455e6](https://github.com/moongate-community/moongate/commit/f3455e6db9de03374b7ab0a913ed21e9e9d94745))
* more Lua modules and functions over the existing services ([b68ffbe](https://github.com/moongate-community/moongate/commit/b68ffbe94f4507061b0e81c153c050c3f160a1ca))
* NPCs walk a path from Lua (npc.walk_to, npc.find_path) ([b29ec84](https://github.com/moongate-community/moongate/commit/b29ec84a26fd7ae2256a28cae56035f7cddc212c))
* read and write a mobile's stats and skills from Lua (roadmap 0.1, slice 1) ([d27ec4c](https://github.com/moongate-community/moongate/commit/d27ec4cb7f471e9803b054ba8914e6ffa1656642))
* respawning treasure chests in dungeons (slice 1) ([45ceb95](https://github.com/moongate-community/moongate/commit/45ceb957e91d33955601d31e90c93c747a96741a))
* saved item timers, item.equip, item.find and the text prompt from Lua ([669614b](https://github.com/moongate-community/moongate/commit/669614b249c3df4e4427b16ab9b73eb74fd19c9e))
* **scripts:** an item script can refuse to be lifted, dropped, worn or filled ([e57cf83](https://github.com/moongate-community/moongate/commit/e57cf831c1f164712cfcb724be3bee25a69ce37d))
* **scripts:** ask a player for a line of text from Lua ([b08008a](https://github.com/moongate-community/moongate/commit/b08008a9a57d98b692b57ffc4ecc7527f5e177da))
* **scripts:** keep shard-wide props across restarts ([fd9a61f](https://github.com/moongate-community/moongate/commit/fd9a61fd00949b4f77cd9aacb6215bf07fb734b5))
* **scripts:** list the mobiles around an NPC ([0f17b8d](https://github.com/moongate-community/moongate/commit/0f17b8d22f720c015d0bf2b382916a9b157f5209))
* **scripts:** make, change and move items from Lua ([e21c2df](https://github.com/moongate-community/moongate/commit/e21c2dfbdd9184be755d089ad50b1598b89020d3))
* **scripts:** put an item on a mobile and find items by template from Lua ([fd2d45f](https://github.com/moongate-community/moongate/commit/fd2d45f2fdd1eb9ec60058a4e5e9eab15e27d893))
* **scripts:** read and write a mobile's stats and skills from Lua ([b759d98](https://github.com/moongate-community/moongate/commit/b759d98e2d3fed887be4dc366ad22b2fd189fcb4))
* **scripts:** read mobiles and the world from Lua ([f85c02e](https://github.com/moongate-community/moongate/commit/f85c02e9c3b1571a028404f056d40c5067bac65d))
* **scripts:** region change event and NPCs stepping on items ([e685e9f](https://github.com/moongate-community/moongate/commit/e685e9f07b8e6a42ad6f6f2686c4d4f247892110))
* **scripts:** spawn, delete and turn NPCs from Lua ([679e02c](https://github.com/moongate-community/moongate/commit/679e02cd86375056709acc225843d3d3c8e5a0d5))
* **scripts:** target cursor from Lua ([486d5a3](https://github.com/moongate-community/moongate/commit/486d5a389a3210c5fcbba16de7d8ddb9eceeb85e))
* **scripts:** the real time, an NPC's template and loot rolled into a container ([94a3dc5](https://github.com/moongate-community/moongate/commit/94a3dc5185289e17759d4a60c48e6d47c5a0294e))
* **scripts:** timers an item keeps across restarts ([382072a](https://github.com/moongate-community/moongate/commit/382072abb719a02dce84accb2f771b0d242485ce))
* **scripts:** town containers fill up when they are opened ([21fc0b5](https://github.com/moongate-community/moongate/commit/21fc0b5df6f3dbb40ed8310d35326f4be47b9495))
* **scripts:** walk an NPC along a path from Lua ([74cd8e9](https://github.com/moongate-community/moongate/commit/74cd8e93644d8616a175aad6c3f71b0b63c05777))
* **server:** a mobile can be hidden, frozen and in war mode ([4ae1d9e](https://github.com/moongate-community/moongate/commit/4ae1d9ec177cdeb9277be622883b34c74f01599b))
* **server:** A* pathfinding service ([ea10749](https://github.com/moongate-community/moongate/commit/ea107495ff6189394faa3ab1a771b8955a4f95d3))
* **server:** change a mobile's numbers, skills, name and looks and tell the clients ([892748e](https://github.com/moongate-community/moongate/commit/892748efc18252c0338255cfb541cda5ae647a9d))
* **server:** clocks tell the time and the staff sees the blockers ([a710927](https://github.com/moongate-community/moongate/commit/a7109275b7fe9f2b1fecfa93989ca14dd1e785b3))
* **server:** decorate places the town containers as fillable ([0c27ab2](https://github.com/moongate-community/moongate/commit/0c27ab267e39f8b402e491d492b198ac554577ef))
* **server:** find the ground items of a cell without searching its sector ([03852c0](https://github.com/moongate-community/moongate/commit/03852c00c1d5811b8b7c1fb73569af902c78dc0c))
* **server:** go gump of the named places and travel by name ([b5c8d99](https://github.com/moongate-community/moongate/commit/b5c8d99848dbd5439dafe236cc0eb465c065c91f))
* **server:** go reads a place written with slashes ([5f13f8b](https://github.com/moongate-community/moongate/commit/5f13f8ba7f4ba32f6f2a65db6870f8f017a97bfc))
* **server:** ground items block movement, closed doors among them ([a257af4](https://github.com/moongate-community/moongate/commit/a257af497d495999699178b26fad12f8a82adfc6))
* **server:** hidden mobiles leave the players' screens, war mode is answered, flags from Lua ([2b70e4e](https://github.com/moongate-community/moongate/commit/2b70e4e7a3635fb970e173360c668459933feac7))
* **server:** item templates can hold loot and gold ([ccf5ede](https://github.com/moongate-community/moongate/commit/ccf5ede78904b4590d0d67a6ee79dd92a147f435))
* **server:** items go into a container on the ground and those around see it ([a11f5f7](https://github.com/moongate-community/moongate/commit/a11f5f753c5bd50465f9116c327abe5c2097f6b4))
* **server:** keep the path each NPC walks ([cbf1e56](https://github.com/moongate-community/moongate/commit/cbf1e56eec25964985b3b8bf3af3e91e622759e2))
* **server:** load the named places and keep them as a tree ([5a08d13](https://github.com/moongate-community/moongate/commit/5a08d13e3988733db790fc811f470c340e618c7e))
* **server:** piles merge inside a container on the ground; chest scrolls of ModernUO's circles ([0673448](https://github.com/moongate-community/moongate/commit/06734482227f77bf7711a769ef6057f53d5f7a48))
* **server:** register and document the pathfinding service ([c1e86e5](https://github.com/moongate-community/moongate/commit/c1e86e5a48859f310429e0b256a9c1063ca4431e))
* **server:** spawn an item on the ground with its gold and loot ([c2ae8f3](https://github.com/moongate-community/moongate/commit/c2ae8f391ac0bc52ab94dd293d5c4a38f42cb14c))
* **server:** spawn regions can spawn items on the ground ([f67b58c](https://github.com/moongate-community/moongate/commit/f67b58c3673373f8a52d8009040697476cf920f9))
* **server:** treasure chest templates that decay though fixed ([36aeb09](https://github.com/moongate-community/moongate/commit/36aeb09fc1f7b8b02ba6f5718a47d380c5194c7a))
* **server:** wait ten seconds before searching again a path that was not found ([0e0c28b](https://github.com/moongate-community/moongate/commit/0e0c28ba20d0811f501ef54f2c41c81f4ca516bc))
* town containers that fill up (slice 2) ([47bd992](https://github.com/moongate-community/moongate/commit/47bd992ae5462ad3b9dd3413b54ed35bae3aa53a))


### Bug Fixes

* **ci:** take the workspace back before the image jobs check out ([236dcf5](https://github.com/moongate-community/moongate/commit/236dcf57f554ec86ae3a63a3a6ec9387770569b1))
* **scripts:** settle the findings of the fillable containers review ([843a999](https://github.com/moongate-community/moongate/commit/843a999c8ce589843e33abc4a865ae905ebcd5e4))
* **scripts:** settle the findings of the Lua modules review ([a2b35ae](https://github.com/moongate-community/moongate/commit/a2b35ae0b0d0ce228931bd297e117bdc4cc9b79e))
* **scripts:** settle the findings of the refusing events review ([9c660a7](https://github.com/moongate-community/moongate/commit/9c660a75d762db2f468bc4dd0a42ea985a8b2086))
* **scripts:** settle the findings of the timers and prompt review ([7d290dd](https://github.com/moongate-community/moongate/commit/7d290ddd60d400407b86ab1fb6deabe5af4ed6f5))
* **scripts:** settle the minor findings of the timers and prompt review ([0dc4a33](https://github.com/moongate-community/moongate/commit/0dc4a33ab05324b575f0d25b4c5fe2c36df161a7))
* **scripts:** settle the minors of the fillable containers ([a0272e5](https://github.com/moongate-community/moongate/commit/a0272e5bbcff6144deac3292e35b8f39a8d565c2))
* **server:** a character does not bring back an item the world moved on from ([58d0e6f](https://github.com/moongate-community/moongate/commit/58d0e6f5d0a7a9ccfb72298e4fe1970b8de45b12))
* **server:** do not leave an empty chest row and say when a region finds no spot ([bd4a715](https://github.com/moongate-community/moongate/commit/bd4a715bea31b982d39653290b2c37f12d41c583))
* **server:** go gump says when a place is refused and cuts long texts ([45d24a6](https://github.com/moongate-community/moongate/commit/45d24a6fd329d0824f4b8e0ba316e48461e38de3))
* **server:** settle the findings of the chest minors review ([0597819](https://github.com/moongate-community/moongate/commit/0597819e95f5cdd6539292bc10620a465ebda9f8))
* **server:** settle the findings of the go gump review ([5b5d50f](https://github.com/moongate-community/moongate/commit/5b5d50fc29e3dc2296bcaada222ef224bd9b5580))
* **server:** settle the findings of the items-block-movement review ([44415a1](https://github.com/moongate-community/moongate/commit/44415a14056ec591421325a418d4ebf95b8dd3ac))
* **server:** settle the findings of the mobile flags review ([98a0dbd](https://github.com/moongate-community/moongate/commit/98a0dbd953601e305507b9d1fdd590152b015909))
* **server:** settle the findings of the mobile state review ([5d7f31e](https://github.com/moongate-community/moongate/commit/5d7f31ed67f6888ab381683413fd6275db4ba6ce))
* **server:** settle the findings of the pathfinding review ([4a7fa49](https://github.com/moongate-community/moongate/commit/4a7fa4940cba789903f21ba9faaa4cdfed9ef48f))
* **server:** settle the findings of the treasure chests review ([6be6457](https://github.com/moongate-community/moongate/commit/6be64572c2e44529e2758eb662b7772347b70e0e))
* **server:** settle the findings of the walk_to review ([98c64ce](https://github.com/moongate-community/moongate/commit/98c64cef14c7767b7465b1b574a78176dc25e2e0))
* **server:** settle the minor findings of the mobile flags review ([eaf16c1](https://github.com/moongate-community/moongate/commit/eaf16c1133d82a3b2d958b894f5bae2a6b47991b))
* **server:** settle the minor findings of the refusing events review ([4f1baf9](https://github.com/moongate-community/moongate/commit/4f1baf982d504328d8c247f391e88479d3ff933d))
* **server:** spawned items keep to their own cells and are counted without a scan ([fe24cfc](https://github.com/moongate-community/moongate/commit/fe24cfc2bb49ef82d4f6598a7632a8713c4f1648))
* **server:** the spawn texts no longer say NPCs for a region of items ([c71a577](https://github.com/moongate-community/moongate/commit/c71a57728451d3bbd7c2430d951eb7e11645da80))
* settle the minors of the treasure chests ([27a1602](https://github.com/moongate-community/moongate/commit/27a16021d1d26f87f334e0f807d10dbb82479e7c))


### Performance Improvements

* index the contents of containers ([d4f37ef](https://github.com/moongate-community/moongate/commit/d4f37efb80dd41cd30e1ce356a1d406faba40fbb))
* **server:** find what a mobile owns through the indexes ([5d2570f](https://github.com/moongate-community/moongate/commit/5d2570f7eca065c7b3860018aada113804bc2d1b))
* **server:** index the contents of containers ([d0cb264](https://github.com/moongate-community/moongate/commit/d0cb264df917d13650c9aa40cddb1c863bb9a86f))

## [0.12.0](https://github.com/moongate-community/moongate/compare/v0.11.0...v0.12.0) (2026-10-02)


### Upgrade notes

- One new world migration, `0011_item_grid_index.sql`: run `mgctl migrate apply --root-directory <root> --target world` before starting the server.
- The tools are now one: `mgboot <root>` is `mgctl init <root>`, the migration runner is `mgctl migrate` and `mg-uoxconv` is `mgctl convert`. The server executable is named `mgserver`; the Linux installer keeps the `moongate` command.
- New optional settings start with their defaults: `[sql_backup]` (off), `network.enable_ping_server` and `network.ping_port` (a UDP ping server on port 12000, on by default).
- Run `mgctl init` again on an existing root to add the new data (`moongates.toml`, the messages of each language as a directory), templates (teleporters, signs, gumps, the bank box, the moongate) and scripts; existing files are preserved. Then run `.decorate` once as an administrator to place the teleporters, the signs, the town doors and the public moongates.
- New in the world: gumps from XML and Lua, teleporters and moongates, the bank, seasons, moon phases, region music and graphic effects. Combat, pathfinding AI, death and skill gain are not yet available.

### Features

* automatic rotating SQL backups and the sql_backup command ([23608e2](https://github.com/moongate-community/moongate/commit/23608e2e3278e2847f9ba870464a7f2de319a858))
* **bank:** add the bank box and the service that opens it ([e08ef2e](https://github.com/moongate-community/moongate/commit/e08ef2e88d58784c76fa7f9e11a92d7d74e2897c))
* **bank:** bank box opened by the bankers' bank keyword ([43b22df](https://github.com/moongate-community/moongate/commit/43b22dfe3f392cc61fd95f4a12425c787d637a37))
* **bank:** keep bank contents out of reach while the bank is closed ([2bb7652](https://github.com/moongate-community/moongate/commit/2bb7652883e02dfdc84500b56dc5e37f82dd8087))
* **bank:** open the bank box from Lua and from the bankers ([eb3f1eb](https://github.com/moongate-community/moongate/commit/eb3f1eba7b072d6e4a828c4d000661fd178c982c))
* **boot:** migrations and converters inside mgboot ([0a8dd2e](https://github.com/moongate-community/moongate/commit/0a8dd2e53b0077a6b39d0bde259d97ba4bd3f3dc))
* **clock:** add the moon phases and show them in .time ([5c63cd1](https://github.com/moongate-community/moongate/commit/5c63cd19a83c8a18a33390ad9dd7a8bd82211d21))
* **commands:** add .go to take a game master to a place ([56fdd53](https://github.com/moongate-community/moongate/commit/56fdd539e9006986b7a6a97300dd4bd5b4f16abe))
* **commands:** add .gump to open a gump of templates/gumps on yourself ([5a53a67](https://github.com/moongate-community/moongate/commit/5a53a673ec409ed11a989215529c6c7684882d7c))
* **commands:** add .music to show or try the region music ([03d9127](https://github.com/moongate-community/moongate/commit/03d9127f243cc3934e11b844f643c5bb609f86a8))
* **commands:** add .time to show the game time where you stand ([436836a](https://github.com/moongate-community/moongate/commit/436836ac5687cfcb6aa3d1bcd9cf947716cfce95))
* **console:** add console lock to lock the console input again ([c580c58](https://github.com/moongate-community/moongate/commit/c580c58c01a7ee0bea53369a6a8bc2dee1838779))
* **console:** console lock and GitHub-ready exception reports ([f04e22f](https://github.com/moongate-community/moongate/commit/f04e22fd5c5f9229710b0bf7fedde8ebafa17451))
* **converter:** add modernuo-spawns to convert ModernUO spawners ([07bd014](https://github.com/moongate-community/moongate/commit/07bd014d9b2e8095a85f7b2c5dfd241174b47754))
* **converter:** convert ModernUO's teleporters.json into decoration files ([1552887](https://github.com/moongate-community/moongate/commit/1552887048733879eba55e830b0cef441a87050e))
* **data:** log the total time of the data loaders ([20161d8](https://github.com/moongate-community/moongate/commit/20161d8948b16e1f51791e64df2bba9d39fae9c8))
* **data:** ship ModernUO's world and dungeon teleporters ([5f1cf99](https://github.com/moongate-community/moongate/commit/5f1cf99e30c22167ca05bd811649a10d2c3eec6c))
* **decorations:** generate the town doors from the map's door frames ([60aa7ca](https://github.com/moongate-community/moongate/commit/60aa7caa3c4c4349d7a8b0c4e502c9fc3bbf2601)), closes [#238](https://github.com/moongate-community/moongate/issues/238)
* **decorations:** place the shop and world signs of ModernUO ([e6e55b1](https://github.com/moongate-community/moongate/commit/e6e55b1b501fd0c9b7f8ad1d43456dd185dc9681)), closes [#238](https://github.com/moongate-community/moongate/issues/238)
* **decorations:** town doors and shop signs with .decorate ([8dea7fb](https://github.com/moongate-community/moongate/commit/8dea7fb1e3106904bd13f1174878521d34045c69))
* **effects:** graphic effects with packets, service, Lua module and named enums ([bcda5a3](https://github.com/moongate-community/moongate/commit/bcda5a3a93a13f43276748afdd78841e4a7576da))
* **effects:** graphic effects with packets, service, Lua module and named enums ([e309cc0](https://github.com/moongate-community/moongate/commit/e309cc0c532d7c446e10af3db79c96ed240e22de)), closes [#247](https://github.com/moongate-community/moongate/issues/247)
* **gumps:** add the gump packets ([9f082ff](https://github.com/moongate-community/moongate/commit/9f082ff5b612fbb97d420fdc0183c45fa7460438))
* **gumps:** ask before decorating with a gump of templates/gumps ([bbdf893](https://github.com/moongate-community/moongate/commit/bbdf89390d47acfec348b7185c7edef9c7337f71))
* **gumps:** build gump layouts with their string table ([95ad693](https://github.com/moongate-community/moongate/commit/95ad69317e63fff06c91682fac8174a78a4a4811))
* **gumps:** build gumps from Lua and fill XML slots ([fcea41a](https://github.com/moongate-community/moongate/commit/fcea41acd2f14d1fd685ed771f322a7d59fbf333))
* **gumps:** chain gumps with bind and open ([a41ea23](https://github.com/moongate-community/moongate/commit/a41ea239e91c4e5da7103156a13efc0587ebfd2b))
* **gumps:** gump core with checked answers ([902cee0](https://github.com/moongate-community/moongate/commit/902cee0f6d0c1c555833bccad80f3cc5744e8941))
* **gumps:** gumps built in Lua, slots, chained gumps and a tutorial ([59e535e](https://github.com/moongate-community/moongate/commit/59e535ec14eec11e257a10bc91f3e16b252e89ec))
* **gumps:** hand the gump answers to the gump service ([7213c95](https://github.com/moongate-community/moongate/commit/7213c959eb304ec19e371733f674eefb192e75a6))
* **gumps:** load XML gumps checked against gump.xsd ([53c8dc6](https://github.com/moongate-community/moongate/commit/53c8dc6ec1a66389d0d652f2bcddda85fc769f21))
* **gumps:** open gumps on players and check their answers ([ff34dd2](https://github.com/moongate-community/moongate/commit/ff34dd24407f198c0513c46098ca8f6a5a72dfd4))
* **gumps:** open XML gumps from Lua and call their scripts ([45a9e71](https://github.com/moongate-community/moongate/commit/45a9e71b12e75cdb601297f3188a129e4e3b0ca4))
* **gumps:** ship the tutorial gumps ([a1c5bd5](https://github.com/moongate-community/moongate/commit/a1c5bd51f25df726b3ba14667c5e3960530b3348))
* **gumps:** tell a gump when the server closes it, and harden the layout ([dd9af68](https://github.com/moongate-community/moongate/commit/dd9af68e5d64eb4ee998a0baa994470135b3cc78))
* **gumps:** turn an XML gump into a layout for one opening ([e7db8d0](https://github.com/moongate-community/moongate/commit/e7db8d02094c81c6187edbb753ef50cc57f29c07))
* **gumps:** XML gumps checked by an XSD, with Lua scripts ([f95ef34](https://github.com/moongate-community/moongate/commit/f95ef34103c5980b15c62f488d88a88d22217b79))
* import ModernUO's world and dungeon teleporters ([d2b000f](https://github.com/moongate-community/moongate/commit/d2b000f5df8b65c1d5fc63585d7fd9c993c829ab))
* **items:** container grid slots for the Enhanced Client ([b9df717](https://github.com/moongate-community/moongate/commit/b9df717dfa9cee3c98719c3c1d8e332ef782f2ab))
* **items:** find an item's worn root and keep the bank out of sight ([c62396f](https://github.com/moongate-community/moongate/commit/c62396fd387d68e108a21b1f81b00f4d314dc9e1))
* **items:** give every item in a container a grid slot for the Enhanced Client ([e7bb03a](https://github.com/moongate-community/moongate/commit/e7bb03a7295f5ae9f45884ec03c31ce81d437f29)), closes [#239](https://github.com/moongate-community/moongate/issues/239)
* **localization:** load a language's messages from a directory of toml files ([d06c08a](https://github.com/moongate-community/moongate/commit/d06c08ad4b6dbf0e63a8dfab26c2c34e7b6bac0f))
* **localization:** load a language's messages from a directory of toml files ([ed6425c](https://github.com/moongate-community/moongate/commit/ed6425c085ed26dbbc557f4486d13350ece9fb31)), closes [#221](https://github.com/moongate-community/moongate/issues/221)
* **logging:** show only the message and the report of an exception on the console ([9aaf34d](https://github.com/moongate-community/moongate/commit/9aaf34ddb090f833c0f62a133bda1743e2ea9266))
* **logging:** write GitHub-ready reports of logged exceptions ([6f14e39](https://github.com/moongate-community/moongate/commit/6f14e392428791d5398cc2ef2f838707733c66d4))
* **moongates:** add the plain moongate item with one destination ([b0d6076](https://github.com/moongate-community/moongate/commit/b0d60761d7a41efd364bbda033e40dc7586b762c)), closes [#262](https://github.com/moongate-community/moongate/issues/262)
* **moongates:** plain moongate item with one destination ([882a6d4](https://github.com/moongate-community/moongate/commit/882a6d4f8bf83f76aec3e0c23c09acbbc8e36a93))
* **moongates:** public moongates with a destination gump ([2f6d939](https://github.com/moongate-community/moongate/commit/2f6d939049b4f278badeec6c3d529bb9400aa52d))
* **moongates:** public moongates with a destination gump ([2a98a5a](https://github.com/moongate-community/moongate/commit/2a98a5a62dba5729ac97f18e7765b1cc544d6837)), closes [#255](https://github.com/moongate-community/moongate/issues/255)
* name the server executable mgserver ([b791277](https://github.com/moongate-community/moongate/commit/b7912774d0ac1f468d199a19208fd05e3721f0f5))
* **network:** answer UDP pings on port 12000 ([bc1dd2b](https://github.com/moongate-community/moongate/commit/bc1dd2b519bb842bf3af0627d8f4242c851d91be))
* **network:** answer UDP pings on port 12000 ([a86734a](https://github.com/moongate-community/moongate/commit/a86734ac05ab2208e79d040fdc30453b59b6a50a)), closes [#232](https://github.com/moongate-community/moongate/issues/232)
* one tool, mgctl, for root setup, migrations and converters ([b27aa9b](https://github.com/moongate-community/moongate/commit/b27aa9b1fb2ae8b7a8256b62e98aa862dee26541))
* **persistence:** export a database's data as a COPY script ([2c68362](https://github.com/moongate-community/moongate/commit/2c6836209ae5a16a5ce4005d60d150d677648be8)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* **persistence:** expose the data export on the persistence service ([0c548d1](https://github.com/moongate-community/moongate/commit/0c548d1ff890fbe86e9eb45c5e01ca68e02acacc)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* **scripting:** add the player_say script event ([f194c2d](https://github.com/moongate-community/moongate/commit/f194c2d58e2bbd5b37aec610f3725b5ca294b967))
* **scripting:** add world.moon for the moon phases in Lua ([eb05e77](https://github.com/moongate-community/moongate/commit/eb05e771ebfddd98f27c00d1310cd814d2a0c04c))
* **scripting:** call a function value a script handed to the host ([a361f5f](https://github.com/moongate-community/moongate/commit/a361f5f31d5744636b10664eb68bf9d7f02fbb66))
* **scripts:** add run_server.sh to publish a release into dist and start it ([ab5a8a0](https://github.com/moongate-community/moongate/commit/ab5a8a024968d0bbf376c9456979be09c7b657ca))
* **scripts:** pass the other options of run_server.sh straight to the server ([ae4b2ff](https://github.com/moongate-community/moongate/commit/ae4b2ff6bd475f82dc78c9431166c518efba2c16))
* **scripts:** teleporter item script and the mobile Lua module ([108fe25](https://github.com/moongate-community/moongate/commit/108fe2562854862b939b2012abb41dce73f3114b))
* **seasons:** add .season to show or set a map's season ([4c5c6b6](https://github.com/moongate-community/moongate/commit/4c5c6b6f9fe5130e1a78f0514886b03ba69f4315))
* **seasons:** count game days and add the season rotation settings ([4384f85](https://github.com/moongate-community/moongate/commit/4384f854607b03b7669d2177cd09c7bb7143d46a))
* **seasons:** follow each player's season and send it with light and weather ([e58d011](https://github.com/moongate-community/moongate/commit/e58d01159b964be23803bce594da8ca3035ad9c9))
* **seasons:** read a region season and inherit it from the parents ([134791e](https://github.com/moongate-community/moongate/commit/134791eb3f09e37d0e5c95523400598a40220734))
* **seasons:** region seasons, optional rotation and .season ([8dbc994](https://github.com/moongate-community/moongate/commit/8dbc9948ed04385dc4970a225183225f3559e307))
* **server:** add the SQL backup service with schedule and rotation ([db5b3e1](https://github.com/moongate-community/moongate/commit/db5b3e1d3abc03553842c6b0def333d3d201a3e7)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* **server:** add the sql_backup command and register the backup service ([d6c7ec1](https://github.com/moongate-community/moongate/commit/d6c7ec1f5c59fa768854ea5a05aa9b7b5e7de550)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* **server:** add the sql_backup configuration section ([8ebc696](https://github.com/moongate-community/moongate/commit/8ebc6969a18ef02558393c2f5411955bc3f57d5e)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* **server:** move a mobile to any spot of its map ([5fe3c37](https://github.com/moongate-community/moongate/commit/5fe3c37be16995745d48412df4fc1dac1aca9308))
* **server:** name the server executable mgserver ([5f87900](https://github.com/moongate-community/moongate/commit/5f8790045e2bf706cba7c429e7fba3d0473e8e5b))
* **server:** place the teleporters of the decoration files ([9e24be9](https://github.com/moongate-community/moongate/commit/9e24be934b24feaa6b1214946aea911617f150de))
* **server:** show hidden ground items only to the accounts that see them ([7505971](https://github.com/moongate-community/moongate/commit/7505971af1248589dab350e7586fd1f0c5d1b5fc))
* **server:** teleport a mobile on its map ([61fbc07](https://github.com/moongate-community/moongate/commit/61fbc07947e6b10eb14eb8e531cafeec823f1108))
* **server:** teleport a mobile to another map ([c7ebab2](https://github.com/moongate-community/moongate/commit/c7ebab2eff9c9bc59e60b35834a7bb2816dfef97))
* **server:** tell the scripted items a player steps on ([3086d74](https://github.com/moongate-community/moongate/commit/3086d74bc368c59157c61f5f5bb1df859d4d8f99))
* **spawns:** add .initial_spawn to fill the whole world at the next check ([4065e9e](https://github.com/moongate-community/moongate/commit/4065e9e4c5e98886a0267afb3771cc5ecb6a6826))
* **spawns:** fill each region to its max at its first spawn after the start ([f2a58da](https://github.com/moongate-community/moongate/commit/f2a58dafa5c18aa62c12d6d13d7ab67b2febbf0b))
* **spawns:** Malas, Tokuno and TerMur spawns from ModernUO ([cc650c2](https://github.com/moongate-community/moongate/commit/cc650c28d5d7151fbe637e5d42c40b838b542d37))
* **spawns:** populate Malas, Tokuno and TerMur from ModernUO ([49218e1](https://github.com/moongate-community/moongate/commit/49218e1b219b054513ab2845a7cab7a96d84a7f2))
* **spawns:** populate New Haven with ModernUO's spawn points ([4122822](https://github.com/moongate-community/moongate/commit/4122822c608d5a5efc77b89b77d33fd5b10e7177))
* **spawns:** show how full the world is in the spawn summary ([882085a](https://github.com/moongate-community/moongate/commit/882085af1a361df62c55c5a75e92864594fff91d))
* **speech:** pass the client's speech keywords to on_speech ([1ef398a](https://github.com/moongate-community/moongate/commit/1ef398a65d3e7943331b0dc408e453bf52398976))
* TAB completion for mgctl ([e789e31](https://github.com/moongate-community/moongate/commit/e789e3117d57b61bf66ab40b8276255548297da8))
* teleporters across maps ([9f0e090](https://github.com/moongate-community/moongate/commit/9f0e09043af7f4fd0ea578d09565922b3b309455))
* **teleporters:** teleporters that answer a word ([97e4784](https://github.com/moongate-community/moongate/commit/97e478419233dffe88110a615010734e4a0017bc))
* **teleporters:** teleporters that answer a word ([e134240](https://github.com/moongate-community/moongate/commit/e134240858152315987671f0270ed5da70ead118)), closes [#252](https://github.com/moongate-community/moongate/issues/252)
* **tools:** rename mgboot to mgctl and ship it as the only tool ([0b20840](https://github.com/moongate-community/moongate/commit/0b2084077577d84bfa8295520fb0d946d05847ac))
* **tools:** TAB completion for mgctl ([acb551d](https://github.com/moongate-community/moongate/commit/acb551d5f0fb5362d775220645b482301feaafad))
* walk-on teleporters on the same map ([abc2395](https://github.com/moongate-community/moongate/commit/abc23952dbdc4e768bb356710249133e35f715ff))
* **weather:** resend a player's weather on request ([49ba3a9](https://github.com/moongate-community/moongate/commit/49ba3a9f94446a85bde6d82eb53a19e9f9ebc0a7))
* **world:** play the music of the region a player is in ([532bdae](https://github.com/moongate-community/moongate/commit/532bdaecaf3fa0d0210f8788957007915d1f34fd))
* **world:** region music ([4620588](https://github.com/moongate-community/moongate/commit/46205886917dd269c3e2a40dc9ff7aaf70608287))


### Bug Fixes

* **bank:** settle the important findings of the bank review ([83ed754](https://github.com/moongate-community/moongate/commit/83ed7548ab2168fa44fb7e5686fcab5b11a07580))
* **bank:** settle the minor findings of the bank review ([2620e31](https://github.com/moongate-community/moongate/commit/2620e310e202a98be2734393a2a8139e40216822))
* **build:** clear the analyzer warnings of the Release build ([8b4eb3f](https://github.com/moongate-community/moongate/commit/8b4eb3faffbfa6d19496b401140ec809092df7b5))
* **characters:** bring a created character into the world ([4af97f4](https://github.com/moongate-community/moongate/commit/4af97f40f903919f789a31f6391accfaf35dadbf))
* **characters:** bring a created character into the world ([57baecd](https://github.com/moongate-community/moongate/commit/57baecdf20863c2e7ae1d34bbb0a3e56445da75d)), closes [#236](https://github.com/moongate-community/moongate/issues/236)
* **characters:** settle the findings of the create and enter world review ([7ac0aaf](https://github.com/moongate-community/moongate/commit/7ac0aafde55bff6de09b363d7b36c8e867ed5e0d)), closes [#236](https://github.com/moongate-community/moongate/issues/236)
* **ci:** let git see the workspace when checking the third-party notices ([214062b](https://github.com/moongate-community/moongate/commit/214062b348a12e37232e0c782f6c4e6dfd2f41c1))
* **converter:** settle the important findings of the modernuo-spawns review ([059ff79](https://github.com/moongate-community/moongate/commit/059ff794fca3857539ecd4dc70a3c70c7f01513a))
* **data:** log the data loader times in milliseconds ([a24ea17](https://github.com/moongate-community/moongate/commit/a24ea17d579df46afb183061748c059f4f3b0390))
* **decorations:** keep generated doors out of doorways an item closes ([0b62a47](https://github.com/moongate-community/moongate/commit/0b62a47c8a49fcfbc20e2c14f33f7ebf3b7ace70)), closes [#238](https://github.com/moongate-community/moongate/issues/238)
* **effects:** settle the findings of the effects review ([88fbc07](https://github.com/moongate-community/moongate/commit/88fbc07dc3a09f2a9813ff5b39562f3e03a30393)), closes [#247](https://github.com/moongate-community/moongate/issues/247)
* **gumps:** check gump files more closely and warn about missing on_click functions ([b91c99b](https://github.com/moongate-community/moongate/commit/b91c99b43f2134ec51dfd70d5bb102cdfffdf993))
* **gumps:** escape and tidy gump texts, and keep the open gumps consistent ([e0abca3](https://github.com/moongate-community/moongate/commit/e0abca3a03aa3ac2d26cb4522b04826e89e5f520))
* **gumps:** open slot gumps from scripts and check built gumps like files ([9ab08e9](https://github.com/moongate-community/moongate/commit/9ab08e9787bfc3ab953549ccba58aa774b1bdf82))
* **gumps:** refuse namespaced gump files, bound the ids, and never wait on a closed session ([3781acb](https://github.com/moongate-community/moongate/commit/3781acb307092f6fdabe139595cdb237017b4371))
* **gumps:** settle the minor findings of the gump review ([b8ed55f](https://github.com/moongate-community/moongate/commit/b8ed55f00a82349d2c2182c3f25eb8a8c182cae5))
* **gumps:** write layouts the client can read on every host and client ([6fe82be](https://github.com/moongate-community/moongate/commit/6fe82bee480a345cf4f4c928f5f59711e70a4965))
* **items:** settle the findings of the container grid review ([0ddb4f6](https://github.com/moongate-community/moongate/commit/0ddb4f6770713c1eff497335b006fb0fe571b7b8)), closes [#239](https://github.com/moongate-community/moongate/issues/239)
* **localization:** settle the minor findings of the messages directory review ([10b3f0c](https://github.com/moongate-community/moongate/commit/10b3f0ce487b5adb17649612bf9c5e181b9a626d)), closes [#221](https://github.com/moongate-community/moongate/issues/221)
* **logging:** settle the review findings and test the logger end to end ([e991a38](https://github.com/moongate-community/moongate/commit/e991a38981fff9cd2869f058aa6cb92effe8e789))
* **login:** accept the hardware information the Enhanced Client sends at login ([52f0e5f](https://github.com/moongate-community/moongate/commit/52f0e5f4397601e0bfd9beb0dd8e3e6d94922105))
* **moongates:** settle the findings of the moongate item review ([6db15a7](https://github.com/moongate-community/moongate/commit/6db15a71664b37354eb21a54ebd069ff50987ae7)), closes [#262](https://github.com/moongate-community/moongate/issues/262)
* **moongates:** settle the findings of the public moongates review ([0be02a0](https://github.com/moongate-community/moongate/commit/0be02a00422784ccbfc9ab64f1a022138aaca847)), closes [#255](https://github.com/moongate-community/moongate/issues/255)
* **music:** inherit parent music and play .music on the game loop ([aa07c0c](https://github.com/moongate-community/moongate/commit/aa07c0c5e3e4481953bcc5709bd175af122dabc1))
* **music:** settle the minor findings of the region music review ([9ef07ac](https://github.com/moongate-community/moongate/commit/9ef07ac005900b079dc75bd782723bb8fe78774c))
* **network:** settle the findings of the ping server review ([96b150d](https://github.com/moongate-community/moongate/commit/96b150d29de9ffb91f231c4a143a50e1c5dd51dc)), closes [#232](https://github.com/moongate-community/moongate/issues/232)
* **network:** settle the minor findings of the ping server review ([0dc2e38](https://github.com/moongate-community/moongate/commit/0dc2e3884aaa503e79a123411f4658d6d8329733))
* **network:** stop the listener without reporting a transport failure ([6662c41](https://github.com/moongate-community/moongate/commit/6662c41ccf103f085ec5c48e70f46cd73ccd563f))
* **packets:** accept the character profile request of the Enhanced Client ([3aa1985](https://github.com/moongate-community/moongate/commit/3aa1985ec98ce00c92bf66da7f34c0ed111561bf))
* **packets:** hold a login burst of packets instead of disconnecting the client ([c5220e6](https://github.com/moongate-community/moongate/commit/c5220e62935c4d2a22a6969e99e443db9f026b32))
* **persistence:** declare the account API access default the shipped SQL sets ([4336fb3](https://github.com/moongate-community/moongate/commit/4336fb3e4365e2b5fc97c8840b04c1a3cb7fe098))
* **persistence:** leave table and column comments out of the schema comparison ([dd50bab](https://github.com/moongate-community/moongate/commit/dd50bab6465a7647dfaa7f0ff6545d0cdb299052)), closes [#231](https://github.com/moongate-community/moongate/issues/231)
* **persistence:** settle the findings of the save-diff review ([4cbf228](https://github.com/moongate-community/moongate/commit/4cbf2284f9a61a3810dd1d5cfb9c3ce52d93851b))
* **persistence:** settle the findings of the SQL backup review ([a86d88c](https://github.com/moongate-community/moongate/commit/a86d88c60b33ca67c1ecb356d57886ab2f2b705c)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* **scripts:** export MOONGATE_ROOT in run_server.sh ([1c4bfef](https://github.com/moongate-community/moongate/commit/1c4bfefa4381c5c491708bf2857ae1d4f96ea865))
* **scripts:** publish run_server.sh's build into a clean dist ([dcac661](https://github.com/moongate-community/moongate/commit/dcac661057ca69db8b3a9e4c69213ff2c1fb6d4b))
* **seasons:** settle the important findings of the seasons review ([f21cf8b](https://github.com/moongate-community/moongate/commit/f21cf8b66c4977ffe49a04978da86a98fa0ed20c))
* **server:** settle the findings of the map change review ([43321d7](https://github.com/moongate-community/moongate/commit/43321d7cf237b82801c70c844bdbeccf12e119dc))
* **server:** settle the findings of the mgserver review ([a3b24b2](https://github.com/moongate-community/moongate/commit/a3b24b2c27bd9ba7ed8bff814bf155b4c67325cf))
* **server:** settle the findings of the teleporter import review ([31f7e47](https://github.com/moongate-community/moongate/commit/31f7e478e65f4c79968f17476268cf963a9aba57))
* **server:** settle the findings of the teleporters review ([4224ce3](https://github.com/moongate-community/moongate/commit/4224ce38b605e2f8e75d2f2c543378f6e073c1f7))
* **server:** settle the minor findings of the SQL backup review ([bac30d9](https://github.com/moongate-community/moongate/commit/bac30d944abad491cf98a5b4ea54615861a84969)), closes [#242](https://github.com/moongate-community/moongate/issues/242)
* settle the code findings of the documentation audit ([80a3740](https://github.com/moongate-community/moongate/commit/80a37400766588621ec8a7df2e9d15e15cf9e1d3))
* settle the minor findings of the teleporter reviews ([cfaec26](https://github.com/moongate-community/moongate/commit/cfaec2689c07e47f3ed5479479cdc6b8ef6c41fb))
* **speech:** read the unicode speech of any client instead of disconnecting it ([1db662f](https://github.com/moongate-community/moongate/commit/1db662f0741977692a43c7401d9fdeddffa0fcad))
* **teleporters:** settle the findings of the keyword teleporter review ([1a45227](https://github.com/moongate-community/moongate/commit/1a45227f5a23632550b44b73de353a15b322aa63)), closes [#252](https://github.com/moongate-community/moongate/issues/252)
* **tools:** settle the findings of the completion review ([6981b76](https://github.com/moongate-community/moongate/commit/6981b76c7c6526d81904af451a8ebf944a22c751))
* **tools:** settle the findings of the mgctl review ([9c892ed](https://github.com/moongate-community/moongate/commit/9c892eda530c0413aaf1534c35c211bf15ac8750))


### Performance Improvements

* **localization:** count the values of a message without throwing exceptions ([4bf83e0](https://github.com/moongate-community/moongate/commit/4bf83e0c98847125ae643ad1a0ed185c26e6aaee))
* **persistence:** write only the snapshots that changed in a world save ([9605976](https://github.com/moongate-community/moongate/commit/9605976d17ea771139f98d1cea202426b9c4e302))
* **persistence:** write only the snapshots that changed in a world save ([3622c21](https://github.com/moongate-community/moongate/commit/3622c21f250362c6f51c7c28d61d4aee9933b8de))

## [0.11.0](https://github.com/moongate-community/moongate/compare/v0.10.0...v0.11.0) (2026-09-30)


### Upgrade notes

- No new database migrations.
- New optional `[ultima.world]` settings for the game clock and the light (`seconds_per_uo_minute`, `day_light`, `night_light`, `dungeon_light`, `jail_light`, `lamp_post_light`) start with their defaults; nothing to change.
- Run `mgboot` again to add the new data (regions, weather), templates (world decoration, NPC lists, spawn regions, mobiles with `movement`) and scripts (`door.lua`, `light.lua`, the updated `wander.lua`); existing files are preserved. Then run `.decorate` once as an administrator to place the world decoration.
- The world fills itself with NPCs from the spawn regions over the first minutes; days and nights pass, dungeons are dark and regions have their weather. Combat, pathfinding AI, death and skill gain are not yet available.

### Features

* **commands:** add .spawns to list the spawn regions where you stand ([566cd21](https://github.com/moongate-community/moongate/commit/566cd21e547c4cdf1f2c211d1017ad79db6767c1))
* **commands:** add fame and karma for game masters ([dd33911](https://github.com/moongate-community/moongate/commit/dd339110dbfffc7df3f9e2a58b6c871b808735bd))
* **commands:** add the decorate command ([e9e69a9](https://github.com/moongate-community/moongate/commit/e9e69a9f81fd3cc2203cb6c940e05a55fb4a56e7))
* **commands:** add the globallight command ([ae4dd20](https://github.com/moongate-community/moongate/commit/ae4dd20e08a7f8db76cab959a7f9fe407340a04b))
* **commands:** add the lock and unlock commands ([3675634](https://github.com/moongate-community/moongate/commit/3675634979f5a5c8c4b1c987e9c986ba0dd88eca))
* **commands:** add the weather command ([7296108](https://github.com/moongate-community/moongate/commit/72961087ac3ac3b759bce0fec5b8d8b85c92085a))
* **commands:** give doors key numbers and make their keys ([43390b3](https://github.com/moongate-community/moongate/commit/43390b389194748a5b1dfa9c77f0acb01cbfdb0b))
* **commands:** print the region of the targeted spot in where ([18479bb](https://github.com/moongate-community/moongate/commit/18479bb7cf7750e914b335a6c336f97a9df1e8a4))
* **converter:** carry UOX3's light script over as script_id light ([70dc1da](https://github.com/moongate-community/moongate/commit/70dc1da6df84fb88c786055d15ac7ae572f7f716))
* **converter:** convert UOX3 npc lists and spawn regions ([90b308c](https://github.com/moongate-community/moongate/commit/90b308ce34de6acf49aa574d2447e6667f7693d1))
* **decorations:** add New Haven's decoration from ServUO ([2339d34](https://github.com/moongate-community/moongate/commit/2339d34802debab3422df2127e301c3e81c160d7))
* **decorations:** add the decoration and decoration_door item templates ([7920cab](https://github.com/moongate-community/moongate/commit/7920cab458531746c70636bf7e5d8b5b7c4155a1))
* **decorations:** convert ModernUO's world decoration into TOML ([439556f](https://github.com/moongate-community/moongate/commit/439556f40ef0cad045207310e00c2f01e021a469)), closes [#193](https://github.com/moongate-community/moongate/issues/193)
* **decorations:** place the decoration files in the world ([27f8753](https://github.com/moongate-community/moongate/commit/27f87536cbe61c690f1454ec0a8f4057a7726ba7))
* **decorations:** place the lights with the light template ([8ed4b7e](https://github.com/moongate-community/moongate/commit/8ed4b7e7abf7c237090966955b0f453ae83dece5))
* **decorations:** place the world decoration with .decorate and open doors from Lua ([470e53d](https://github.com/moongate-community/moongate/commit/470e53d1f670673382a95cf483fada383b249d4c))
* **decorations:** read the decoration files on demand ([ebefb54](https://github.com/moongate-community/moongate/commit/ebefb543247f04102c14f2ef968eb2edd59fbf28))
* **decorations:** world decoration data converted from ModernUO ([4581ec7](https://github.com/moongate-community/moongate/commit/4581ec72833e872146b16cdc7adb31bab7b373bc))
* **doors:** keep locked doors shut for players ([b2940de](https://github.com/moongate-community/moongate/commit/b2940de8997648ec928e1999f9d3db76add24030))
* **doors:** open a locked door with its key ([9877296](https://github.com/moongate-community/moongate/commit/98772963d06bb68c3b61daedc0dd879f92ffa3fc))
* **items:** add the ground item decay queue ([b807adc](https://github.com/moongate-community/moongate/commit/b807adcb57007047b7b883af78b4afb34077846e)), closes [#191](https://github.com/moongate-community/moongate/issues/191)
* **items:** delete the ground items whose decay time has passed ([454cd40](https://github.com/moongate-community/moongate/commit/454cd408aab6891dba339a18dd3ecb777e5b1c57)), closes [#191](https://github.com/moongate-community/moongate/issues/191)
* **items:** ground items decay ([966b89b](https://github.com/moongate-community/moongate/commit/966b89b349cd32999b0e32835a94f45c5a4b2e6f))
* **items:** keep the decay queue up to date with the ground items ([a460a3d](https://github.com/moongate-community/moongate/commit/a460a3df76696d2b71630c6a33734260341455a1)), closes [#191](https://github.com/moongate-community/moongate/issues/191)
* **items:** let item scripts change, move and sound an item ([806577f](https://github.com/moongate-community/moongate/commit/806577fbaf11f0417388e989515524c31fa22083)), closes [#195](https://github.com/moongate-community/moongate/issues/195)
* **items:** lights you can light and douse from Lua ([37563ad](https://github.com/moongate-community/moongate/commit/37563adbe862f5cfea73bc97932a0e4b581c3b92))
* **items:** send the light shape of a light source ([1d6affe](https://github.com/moongate-community/moongate/commit/1d6affeae288bec23fc5f36a9eb575e18cb4692d))
* **movement:** find where a mobile can be placed at a cell ([29a0847](https://github.com/moongate-community/moongate/commit/29a08479eaa1b2b18deefe57cf5aa1261b32e12b))
* **movement:** let a mobile template say where its mobiles move ([8adb606](https://github.com/moongate-community/moongate/commit/8adb606555a2b31f3a2b73af141a2ba5e17c321d))
* **npcs:** keep the spawned NPCs of wander.lua in their home area ([2e79f54](https://github.com/moongate-community/moongate/commit/2e79f5497ded31d130e4d8ceef4f508abed4b711))
* **npcs:** let mobile scripts play a sound ([6daaf5b](https://github.com/moongate-community/moongate/commit/6daaf5bb26c79c5a69ac5ba153e91f41d599d0bd))
* **npcs:** let water and amphibious NPCs swim ([407019f](https://github.com/moongate-community/moongate/commit/407019f34264125084a0c287043d630712f9c313))
* **npcs:** play a mobile's template sounds by kind ([d9a00c3](https://github.com/moongate-community/moongate/commit/d9a00c313087e37d76b556fd3dedc46c190b98e1))
* **packets:** add the weather packet ([a4a261c](https://github.com/moongate-community/moongate/commit/a4a261c8bc00bf47b20d6aeba10044f8d55a8ab4))
* **paperdoll:** show the fame and karma title in the paperdoll ([afdbb4b](https://github.com/moongate-community/moongate/commit/afdbb4bc65b3f4149e140de3958f4fce6120ec79))
* **regions:** find the region of a place ([dd19409](https://github.com/moongate-community/moongate/commit/dd19409118580e46e3d37540b61921a424af2c14))
* **regions:** follow the region each player stands in ([39da79c](https://github.com/moongate-community/moongate/commit/39da79c0d5c141205245a652a0d2264271ee32bb))
* **regions:** know which region a player is in ([aa8cb53](https://github.com/moongate-community/moongate/commit/aa8cb5338ac70c330437fd05f8f5dd4415352a03))
* **regions:** tell listeners when a player changes region ([2f20298](https://github.com/moongate-community/moongate/commit/2f202982dd5d561037ab0d09333ba090ac986cb9))
* **scripts:** add the world module and a door-like integration test ([20b121b](https://github.com/moongate-community/moongate/commit/20b121bb0ec3a2db4c67cd27b55902cf04cd2c78)), closes [#195](https://github.com/moongate-community/moongate/issues/195)
* **scripts:** bring Orione and Vega, the cats of Moongate v2 ([7fd14e6](https://github.com/moongate-community/moongate/commit/7fd14e6077fb43227f9eb75f2463f9a2df690539))
* **scripts:** let scripts keep values on NPCs and items across restarts ([a724f77](https://github.com/moongate-community/moongate/commit/a724f771f8ad8b987885c60fdd51cd2bb3229734))
* **scripts:** let scripts read the time of day ([50070fa](https://github.com/moongate-community/moongate/commit/50070fa847f8666318c5adba2998600dbd854dc3))
* **scripts:** let scripts set an item's light and ask whether a player is staff ([729b3e2](https://github.com/moongate-community/moongate/commit/729b3e2bc3ce88d54bf0578af3f44321afff7145))
* **scripts:** light and douse the lights from Lua ([1b2519d](https://github.com/moongate-community/moongate/commit/1b2519d3d691a7dc0ebfdef75ba23e5cf90d878a))
* **scripts:** Lua APIs to change, move and sound items ([2bc4608](https://github.com/moongate-community/moongate/commit/2bc46084ef0559488f709f89542cffef94ac2a65))
* **scripts:** open and close the decoration doors from Lua ([b17befb](https://github.com/moongate-community/moongate/commit/b17befbc2e53b00a11f397dece80961da94a2c80))
* **scripts:** Orione speaks English ([9ab34ea](https://github.com/moongate-community/moongate/commit/9ab34ea17aa515223dd4cde194adfc17fbbbd9a6))
* **scripts:** Vega remembers how many times you said hello ([af096fc](https://github.com/moongate-community/moongate/commit/af096fcc7c3f1cd003f3064885cec6498eb6053d))
* **scripts:** Vega speaks English ([b0ef77a](https://github.com/moongate-community/moongate/commit/b0ef77a666330af88b7fd824963a98850d584437))
* **spawns:** convert and load the UOX3 spawn regions ([3124837](https://github.com/moongate-community/moongate/commit/31248371fdef4a565053eb747c9aa01b774d19e8))
* **spawns:** load the npc lists and the spawn regions ([c851dc8](https://github.com/moongate-community/moongate/commit/c851dc8e4f87b9c4db8ebdece619cf40812bdce7))
* **spawns:** show retrying regions in .spawns and keep .spawn from putting swimmers on land ([405522e](https://github.com/moongate-community/moongate/commit/405522eee19c9a57d1ad0399c081a82f8c0d2fed))
* **spawns:** spawn the NPCs of the spawn regions at runtime ([71d60d3](https://github.com/moongate-community/moongate/commit/71d60d3440b6d00584b61b63262bfa2139bf9ba8))
* **spawns:** spawn the NPCs of the spawn regions at runtime ([d0f6523](https://github.com/moongate-community/moongate/commit/d0f6523c0fa9d72f457504134e8ae327adc2af0c))
* **spawns:** spawn water and amphibious mobiles on the water ([9ae503d](https://github.com/moongate-community/moongate/commit/9ae503dffc035fe080fd520c82e4541dff3a0f87))
* **templates:** bring Lilly, the noble lady of Moongate v2 ([8259870](https://github.com/moongate-community/moongate/commit/82598703838e398688089a4626070a209e0a90d7))
* **weather:** region weather with thunder and the in-building check ([cc83090](https://github.com/moongate-community/moongate/commit/cc830901a784bb59b683ce78efc8596389d18e0c))
* **weather:** roll the weather of a profile ([9aa50aa](https://github.com/moongate-community/moongate/commit/9aa50aa9f928c120b1e22daf8fdd988235bc59cb))
* **weather:** send the players the weather of their region ([fa41203](https://github.com/moongate-community/moongate/commit/fa41203486fb3da0f2ec391a78245a73aab89418))
* **world:** add the day and night light cycle ([55822f1](https://github.com/moongate-community/moongate/commit/55822f1d25878eb2f7ca57a836b2ede21f25aff7))
* **world:** add the game clock and the light cycle settings ([8454fcd](https://github.com/moongate-community/moongate/commit/8454fcd95a27231eca7d867b906193ffbe2eae4d))
* **world:** dark dungeons and dim jails ([94458a5](https://github.com/moongate-community/moongate/commit/94458a5360404129fee2ed991c5ee669ee6eeae7))
* **world:** light the town lamp posts at night ([6a4faff](https://github.com/moongate-community/moongate/commit/6a4faffc6c31b9214f6bb51869da5b4363203019))
* **world:** log at debug what a player is sent on entering a sector ([b27f3af](https://github.com/moongate-community/moongate/commit/b27f3af7375c99a27aab22e587b069dc7a1abe69))
* **world:** make dungeons dark and jails dim ([d1d8a16](https://github.com/moongate-community/moongate/commit/d1d8a16a25c59804ccbaf4e10e024244397a4ba2))
* **world:** send the players the light of their time of day ([dd38f9e](https://github.com/moongate-community/moongate/commit/dd38f9ef53fd4286dc396ef18edbb74eb2227707))


### Bug Fixes

* **converter:** convert spawn regions as UOX3 loads them ([9cf6b93](https://github.com/moongate-community/moongate/commit/9cf6b9353e0c0e76b05d540d5722a34be440df74))
* **converter:** keep a GET child with a land body from inheriting water movement ([edbf3ec](https://github.com/moongate-community/moongate/commit/edbf3ec944c1a46a9884b26cf4c99013fe543d12))
* **decorations:** keep open doors, saved items and a running decoration from doubling the world ([84acb6b](https://github.com/moongate-community/moongate/commit/84acb6b28e1002648b42761c6c4dcfaec569620e))
* **items:** delete a decayed dropped item with its dropper's save and honour an immovable template ([f219b18](https://github.com/moongate-community/moongate/commit/f219b1855f33f4aebcc6edf13c8dc9a31d66c507)), closes [#191](https://github.com/moongate-community/moongate/issues/191)
* **items:** refuse to move an item outside its map ([532d897](https://github.com/moongate-community/moongate/commit/532d89771165b0b4ca356f72ac03050ddf111d80)), closes [#195](https://github.com/moongate-community/moongate/issues/195)
* **movement:** refuse water under the ground's centre as a swim spot ([a84237f](https://github.com/moongate-community/moongate/commit/a84237f87dde0ec3ad24d11359959c483498da9d))
* **paperdoll:** do not add Lord or Lady twice ([10ff181](https://github.com/moongate-community/moongate/commit/10ff181d68fe0a8ec0d42c8b67b875311276a98f))
* **scripts:** keep whole numbers of script props as long and give doors their closed spot ([7e6cf93](https://github.com/moongate-community/moongate/commit/7e6cf933b2755b17c3cb44efa4f39b030a77c26c))
* **spawns:** keep only_outside regions from spawning swimmers under a roof ([dce0759](https://github.com/moongate-community/moongate/commit/dce075900fcdef5e1fc12e5e515e276bf18be77b))
* **spawns:** place swimmers only on real water and keep a call going past a missed pick ([e2e204b](https://github.com/moongate-community/moongate/commit/e2e204b9e6c3481b65fb6afe20d5a134ee767897))
* **spawns:** register the spawn service once and make the spawns survive failures and shutdown ([17eca2f](https://github.com/moongate-community/moongate/commit/17eca2f4eb76b9cfb571629b79bb16f1b2a334b0))
* **weather:** handle logins on the game loop and resend the weather ([aa9e621](https://github.com/moongate-community/moongate/commit/aa9e621f88b093f8b942b7f99ca8798e5b385f14))
* **world:** light the plain regions inside a dungeon as the dungeon ([5aa14fb](https://github.com/moongate-community/moongate/commit/5aa14fb85daa789286ee13939bc9d55500c4649f))
* **world:** send the light cycle only to characters whose login sent their light ([a8ca6fd](https://github.com/moongate-community/moongate/commit/a8ca6fd31bb5ee5cbd5ee1cc2b14f7e6552c15ec))

## [0.10.0](https://github.com/moongate-community/moongate/compare/v0.9.0...v0.10.0) (2026-09-29)


### Upgrade notes

- Apply the new world database migrations `0007`–`0010` (mobile deletion, slot type, mobile direction, ground item index) before starting the updated server; use the [migration guide](https://moongate.sh/server/persistence-migrations/).
- The gameplay settings moved under `[ultima]`: `[ultima.world]`, `[ultima.characters]`, `[ultima.items]`, `[ultima.starting_items]`, `[ultima.line_of_sight]`, `[ultima.localization]` and the new `[ultima.npcs]`. The old top-level sections are no longer read; move their values. The starting gold is an item of the common set in `starting_items.toml`, not a setting. Missing plugin sections are appended to `moongate.toml` with their defaults at the first start.
- Run `mgboot` again to add the item, loot and mobile templates and the example scripts that now ship with the server; existing files are preserved. A template a release renamed stays beside its new copy and stops the server on the duplicate id: delete the stale one.
- Characters enter a playable world: they walk, see each other, talk, move and wear items, and meet the NPCs a game master spawns; NPCs and items run Lua scripts bound by `script_id`. Combat, pathfinding AI, death and skill gain are not yet available.

### Features

* **boot:** ship the templates and scripts with the server and mgboot ([f14f2c5](https://github.com/moongate-community/moongate/commit/f14f2c5ff5a300c9f0c9213ce474263c625a9e11))
* **boot:** ship the templates and scripts with the server and mgboot ([7eba41d](https://github.com/moongate-community/moongate/commit/7eba41d54afc3d33a6c5eb9c4fc99f0c1edd823d)), closes [#187](https://github.com/moongate-community/moongate/issues/187)
* **characters:** a deleted character gives up its slot and the limit ([a47df44](https://github.com/moongate-community/moongate/commit/a47df44e3bdd68177040ee4715c411cc011bb17a))
* **characters:** add the deletion request column and delay setting ([770c046](https://github.com/moongate-community/moongate/commit/770c046c53a09fd0289118b6514327c5efbd2f2b))
* **characters:** enter the world with the chosen character ([5dd63a2](https://github.com/moongate-community/moongate/commit/5dd63a22d0dc3a63ee50fed36691aca3e24be056))
* **characters:** handle the delete character packet ([f48f10d](https://github.com/moongate-community/moongate/commit/f48f10d0b3fcdd5cc1bd3cc96f76019a039c62d7))
* **characters:** list and restore pending deletions from the console ([a7b88f9](https://github.com/moongate-community/moongate/commit/a7b88f9133e3d09bf0ec2e96efee8907f7e5288b))
* **characters:** load a character and its worn items for play ([9bcd9cd](https://github.com/moongate-community/moongate/commit/9bcd9cdd37a504a60e1af1fd5aa00a53507846b8))
* **characters:** load the backpack contents and keep them live in the world ([0472feb](https://github.com/moongate-community/moongate/commit/0472feb554be6e98be8069d0cc5f69e129bc3425))
* **characters:** publish character_entered_world ([42e6f03](https://github.com/moongate-community/moongate/commit/42e6f03e8b54943601c02a3da02bb5e7ca94e833))
* **characters:** request, list and restore character deletions ([9d314d0](https://github.com/moongate-community/moongate/commit/9d314d0cdf63cd4a7991bac031cfdad21001ceab))
* **characters:** save the character and publish character_left_world when the player leaves ([942d4f7](https://github.com/moongate-community/moongate/commit/942d4f754b1811baf45e6f2bb9eeac052611be97))
* **commands:** add .spawn and .remove for NPCs ([3892f3c](https://github.com/moongate-community/moongate/commit/3892f3c645f7d9ac476d8fd7b78a5d21f988b2a3)), closes [#153](https://github.com/moongate-community/moongate/issues/153)
* **commands:** add .where to show what a target cursor picks ([5232fab](https://github.com/moongate-community/moongate/commit/5232fab8850334ca02c02e5cf46e6185fca7b401)), closes [#151](https://github.com/moongate-community/moongate/issues/151)
* **commands:** add graceful shutdown with optional delay ([e49379e](https://github.com/moongate-community/moongate/commit/e49379ea81ae4a99038d5e677701c20b827bdb53))
* **commands:** add world save and broadcast commands ([54e9f78](https://github.com/moongate-community/moongate/commit/54e9f7811a4d772893093030d02886b67d2efcd1))
* **commands:** include elapsed time in world save messages ([3f5c156](https://github.com/moongate-community/moongate/commit/3f5c156d0516624def680705ee3682211f9cef2a))
* **commands:** translate the dispatcher replies, help and descriptions ([51c9204](https://github.com/moongate-community/moongate/commit/51c9204f774cf916379c2bfb59feccabb9d82000)), closes [#173](https://github.com/moongate-community/moongate/issues/173)
* **commands:** translate the in-game command texts ([c3a053f](https://github.com/moongate-community/moongate/commit/c3a053f829b80fc54b087706f0ae41f322f95285)), closes [#173](https://github.com/moongate-community/moongate/issues/173)
* **config:** group the gameplay settings under [ultima] ([0d2545c](https://github.com/moongate-community/moongate/commit/0d2545cad49094b34799652cdd5e1b7989f4b9fe))
* **config:** group the gameplay settings under [ultima] ([8426ec5](https://github.com/moongate-community/moongate/commit/8426ec5579032aad4de90d4496ac0ca59920e01a)), closes [#157](https://github.com/moongate-community/moongate/issues/157)
* **config:** register the parsed config for plugins ([e5a6977](https://github.com/moongate-community/moongate/commit/e5a69771528acbc393f7236f03fd5f4502bb8213)), closes [#159](https://github.com/moongate-community/moongate/issues/159)
* **docs:** add interactive packet reference ([4938292](https://github.com/moongate-community/moongate/commit/49382928886d1bcc1199f65ef2982472aeb46996))
* **entities:** mobile display name with its title ([3312237](https://github.com/moongate-community/moongate/commit/33122375494aba8c1105aadbf23a5c9117809896)), closes [#127](https://github.com/moongate-community/moongate/issues/127)
* **entities:** readable ToString for items and mobiles, and an item display name ([004b579](https://github.com/moongate-community/moongate/commit/004b579797ff8181ffc6947f239dc43e6e9973ed)), closes [#127](https://github.com/moongate-community/moongate/issues/127)
* **items:** add the 0x1A and 0xF3 world item packets ([9df7dae](https://github.com/moongate-community/moongate/commit/9df7daee7f20777871c9abbd0b76f3a3b73c9a5b)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **items:** add the item Lua module ([5684d5a](https://github.com/moongate-community/moongate/commit/5684d5a4166e2e4381a79402e0070b6637ebe9ca)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* **items:** add the rules for reaching and dropping on the ground ([15b9092](https://github.com/moongate-community/moongate/commit/15b90925d6c24c0a3cd670875e57f410edf417e0)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **items:** decide where an item is worn and whether it fits ([8de3a98](https://github.com/moongate-community/moongate/commit/8de3a98acd910d7ff89266033fba49c5bd9833d6)), closes [#165](https://github.com/moongate-community/moongate/issues/165)
* **items:** drop a held item into the player's own containers ([24a57ae](https://github.com/moongate-community/moongate/commit/24a57ae7a221026a4f2838c58258d7a00061b752))
* **items:** drop items on the ground and see them in range ([8befabd](https://github.com/moongate-community/moongate/commit/8befabd3bab220d641746e4001a91b21f16ca9dd))
* **items:** item scripts with on_use and the item module ([b790fc7](https://github.com/moongate-community/moongate/commit/b790fc7eb2863fef14325b2407436f9cdcab06c5))
* **items:** keep a pool of reserved item serials ([7a913ea](https://github.com/moongate-community/moongate/commit/7a913eaefb9d475b20ed8b1693e27ce982f27aae))
* **items:** keep ground items aligned with the sector grid ([c682963](https://github.com/moongate-community/moongate/commit/c6829632a84f7736a85b2d39303523588673b889)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **items:** keep the items of the characters in the world live ([16ffe45](https://github.com/moongate-community/moongate/commit/16ffe4555521f0919edbbd768c51037b75d0bf5d))
* **items:** lift a whole item from the player's own containers ([3c0f830](https://github.com/moongate-community/moongate/commit/3c0f830b51bd8de4ea41903dc6048c54306aa232))
* **items:** lift, drop and merge items on the ground ([1ac58aa](https://github.com/moongate-community/moongate/commit/1ac58aa39ffd1b7c19dd8524d9cc6225694c3995)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **items:** load and run the item scripts ([c49f080](https://github.com/moongate-community/moongate/commit/c49f080acbd7b672e3dc7988c622a8649833b414)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* **items:** load the items on the ground at startup ([2af29f2](https://github.com/moongate-community/moongate/commit/2af29f251f08118ecbf6134043ced0b50de9725e)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **items:** mark two-handed weapons in item templates ([6ba4993](https://github.com/moongate-community/moongate/commit/6ba49939c188815131dd0aa4f10f2030bf0b4f21)), closes [#165](https://github.com/moongate-community/moongate/issues/165)
* **items:** on_equip and on_unequip for item scripts ([fc6d7ac](https://github.com/moongate-community/moongate/commit/fc6d7ac4d777c3d1f2fdd6cd768e266901d435b4))
* **items:** on_pickup, on_drop and on_create for item scripts ([4ab3615](https://github.com/moongate-community/moongate/commit/4ab361563cc91ac4aa20b468e922cbead3cb1994))
* **items:** open the player's own containers with a double click ([35498a1](https://github.com/moongate-community/moongate/commit/35498a13cb61d6e40d1526411d9e486e5f421bef))
* **items:** run on_create of the items of a spawned NPC ([32d546d](https://github.com/moongate-community/moongate/commit/32d546dda3825b3a87fcd86a65c9b5f424148607)), closes [#185](https://github.com/moongate-community/moongate/issues/185)
* **items:** run on_equip and on_unequip of item scripts ([bfdd727](https://github.com/moongate-community/moongate/commit/bfdd727c985d9a52361ad4ec3ae6ce09764754a0)), closes [#183](https://github.com/moongate-community/moongate/issues/183)
* **items:** run on_pickup and on_drop of item scripts ([3dfaa1d](https://github.com/moongate-community/moongate/commit/3dfaa1dae3d1b4d7c8835ba0e3abf59983ca88e6)), closes [#185](https://github.com/moongate-community/moongate/issues/185)
* **items:** run on_use on double click ([56ee90a](https://github.com/moongate-community/moongate/commit/56ee90a6bf406433e9487889d2ed82d4c2df0363)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* **items:** save the character's items when it leaves and in the world save ([19a2d74](https://github.com/moongate-community/moongate/commit/19a2d74c1a0578b1e2ec294150e64f4893e8b5e8))
* **items:** show worn item changes to players in range ([eff5e2b](https://github.com/moongate-community/moongate/commit/eff5e2b5362f7b14d6d79ad2691dece88d08c578)), closes [#165](https://github.com/moongate-community/moongate/issues/165)
* **items:** split a stack when lifting part of it and merge stacks on drop ([245bff4](https://github.com/moongate-community/moongate/commit/245bff4ce54da1cac522d7b913251c81ccd88086))
* **items:** split and absorb live items with tombstones for the save ([c6f4373](https://github.com/moongate-community/moongate/commit/c6f43731bb18fd3541cfa5af9c8bd680340d7c4e))
* **localization:** add the command texts in every language ([03b0040](https://github.com/moongate-community/moongate/commit/03b0040a71e979264c6b7bff30e951019dd213e8)), closes [#173](https://github.com/moongate-community/moongate/issues/173)
* **localization:** translate the in-game command texts ([63db66f](https://github.com/moongate-community/moongate/commit/63db66f090c48b3cd662952fe6b91667bf9d052c))
* **localization:** translate the item rarities in every language ([d6aad07](https://github.com/moongate-community/moongate/commit/d6aad0749eef3f8d017b89087b0c961997085d96))
* **mgboot:** show shared banner during root setup ([b2815e2](https://github.com/moongate-community/moongate/commit/b2815e2666b662764123bf50dfa6d02513d53f0e))
* **mgboot:** show shared banner during root setup ([d93aa14](https://github.com/moongate-community/moongate/commit/d93aa1403ef5410bae918a231ecb51bdc1a1f10a))
* **mobiles:** add the mobile service with virtual serials for hair and beard ([51fdf96](https://github.com/moongate-community/moongate/commit/51fdf96ec82fe28601eb43935c61b4077223e70a))
* **mobiles:** delete mobiles through the world save ([620f979](https://github.com/moongate-community/moongate/commit/620f97954d23611dcf6459d9205a635c9d9dfd7c)), closes [#153](https://github.com/moongate-community/moongate/issues/153)
* **mobiles:** keep the characters in the world live and move them ([a70af3f](https://github.com/moongate-community/moongate/commit/a70af3fc7d9136638df4fbdcf09e4185d07d6da1))
* **mobiles:** keep the direction a mobile faces and take detached snapshots ([8abdfc6](https://github.com/moongate-community/moongate/commit/8abdfc6b254a095c76b500d6dedb755cde9226cc))
* **motd:** add extensible variable registry ([9598dc2](https://github.com/moongate-community/moongate/commit/9598dc212d14f4455113a47e928e92dfe18600df))
* **motd:** load and validate MOTD templates ([7ae76dc](https://github.com/moongate-community/moongate/commit/7ae76dc532828081c36964fdbe9e2a522840e2c5))
* **motd:** render built-in and plugin variables ([dccaf75](https://github.com/moongate-community/moongate/commit/dccaf75579261b3e619a7a4fa43c3ef1e8cbfdfe))
* **motd:** send configurable private welcome messages ([b3b0185](https://github.com/moongate-community/moongate/commit/b3b018537789ac84f42bb9e844ed2ed348008846))
* **movement:** walk and run with sequence and speed checks ([88f09ed](https://github.com/moongate-community/moongate/commit/88f09edfea4389a45a14c73cd312aa569385b8e0))
* **npcs:** add the [ultima.npcs] sense range ([0e17b97](https://github.com/moongate-community/moongate/commit/0e17b97631f17a9b74a0199897ac0ec5c7107074)), closes [#179](https://github.com/moongate-community/moongate/issues/179)
* **npcs:** add the [ultima.npcs] think interval ([9f428d3](https://github.com/moongate-community/moongate/commit/9f428d3d5071748d38c035df71863b3fa4049bc3)), closes [#175](https://github.com/moongate-community/moongate/issues/175)
* **npcs:** add the npc Lua module and overhead speech ([356bc14](https://github.com/moongate-community/moongate/commit/356bc14c7f31df1797fba25a6fbcbd77f407c2c3)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **npcs:** add the NPC tick service ([036ab93](https://github.com/moongate-community/moongate/commit/036ab93ec9dda54f3e895229758cb5021e15b3ee)), closes [#175](https://github.com/moongate-community/moongate/issues/175)
* **npcs:** call on_spawn of a spawned NPC's script ([a13c7d9](https://github.com/moongate-community/moongate/commit/a13c7d9f5b216ed74cc1df2a8f77014762a2e7f3)), closes [#179](https://github.com/moongate-community/moongate/issues/179)
* **npcs:** drive NPC thinks with their mobile scripts ([dc3aa36](https://github.com/moongate-community/moongate/commit/dc3aa366eab064b3cad3cbeebf5790ca7d13e413)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **npcs:** let mobile scripts hear the players nearby ([37ded83](https://github.com/moongate-community/moongate/commit/37ded839a2157aaf9eb94d6aa21ebce26d751b0a)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **npcs:** let mobile scripts sense the mobiles coming within range ([a5828d2](https://github.com/moongate-community/moongate/commit/a5828d2d836585a6f50215b527297c8ac16b9b2a)), closes [#179](https://github.com/moongate-community/moongate/issues/179)
* **npcs:** let npc.step run ([6fa0dd8](https://github.com/moongate-community/moongate/commit/6fa0dd8267317d986248e2a1acef5c7eca9bb737)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **npcs:** load, spawn and remove NPCs in the live world ([8924d45](https://github.com/moongate-community/moongate/commit/8924d4550c1c144a29b66191b4b4de817377883c)), closes [#153](https://github.com/moongate-community/moongate/issues/153)
* **npcs:** Lua mobile scripts for NPCs ([7ad6f2d](https://github.com/moongate-community/moongate/commit/7ad6f2d98bac07dda2a33fd08f322767a7e4dea0))
* **npcs:** NPC tick for NPCs near players ([93d311d](https://github.com/moongate-community/moongate/commit/93d311d4bf9c83770aad837caaa50ea9a1266517))
* **npcs:** on_spawn and on_mobile_in_range for mobile scripts ([5d76357](https://github.com/moongate-community/moongate/commit/5d76357cf45e7f3ef755d8de71965133649660b7))
* **npcs:** queue mobile script calls on the game loop ([e30d85c](https://github.com/moongate-community/moongate/commit/e30d85c1c7790b3ab2851af26819edfa3902f5f3)), closes [#179](https://github.com/moongate-community/moongate/issues/179)
* **npcs:** register the NPC tick and its metrics ([4e3f7fe](https://github.com/moongate-community/moongate/commit/4e3f7feef12c66a91a0b7479d56ecba04e9feabf)), closes [#175](https://github.com/moongate-community/moongate/issues/175)
* **npcs:** ship the scripts directory with a wandering mobile script ([16e72d7](https://github.com/moongate-community/moongate/commit/16e72d7022e0a84a3ff5c5acf2d72d518dfbdbbe)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **npcs:** spawn and remove NPCs in the live world ([ad2c2e0](https://github.com/moongate-community/moongate/commit/ad2c2e0404f57363521f358f7ab5491134e76aa9))
* **packets:** add the delete character request and its answers ([eacc26f](https://github.com/moongate-community/moongate/commit/eacc26f6226849641c6a98fbdfe707b1a86e1ad6))
* **packets:** add the packets that bring a character into the world ([50664e1](https://github.com/moongate-community/moongate/commit/50664e1644c68c66520d1e28d6cc9ad8a5e27781))
* **packets:** ignore the packets the client sends while playing ([ae0913b](https://github.com/moongate-community/moongate/commit/ae0913bbaf2d1021a94b8a910f9cff70d27d9a26))
* **packets:** open containers with 0x24 and 0x3C and read the used object from 0x06 ([e574922](https://github.com/moongate-community/moongate/commit/e574922248298550f11ccb20522b617fe30b9373))
* **packets:** read lift and drop requests and add 0x27 and 0x25 ([0d64530](https://github.com/moongate-community/moongate/commit/0d64530a79d7a48385ad150718ec31b3173e1197))
* **packets:** read move requests and answer them with 0x22 and 0x21 ([6a6ed92](https://github.com/moongate-community/moongate/commit/6a6ed924b4a02b5f0e8fbc76e9bc17e5c86fed80))
* **packets:** receive the play character packet (0x5D) ([bd75856](https://github.com/moongate-community/moongate/commit/bd7585633383b206e263e7976fd0995f279f082f))
* **packets:** recognise the client's first in-world packets and ignore them for now ([25daaae](https://github.com/moongate-community/moongate/commit/25daaae465db0d575b2523c38784cc91eb9a1e01))
* **paperdoll:** dress and undress from the paperdoll ([1e233de](https://github.com/moongate-community/moongate/commit/1e233de5989f8f5186c6e47cec687ac64067f780))
* **paperdoll:** dress and undress from the paperdoll ([a70a374](https://github.com/moongate-community/moongate/commit/a70a37492f4b50d6ab6ec59f8422e6a33e3f5241)), closes [#165](https://github.com/moongate-community/moongate/issues/165)
* **paperdoll:** open the paperdoll on a double click ([65852e4](https://github.com/moongate-community/moongate/commit/65852e4f1f46fdfbc4db87a2fc17c76e51a3fa1b))
* **paperdoll:** open the paperdoll on a double click ([0a25927](https://github.com/moongate-community/moongate/commit/0a259277bd6b8bf25e46ee00986b9508301d587d)), closes [#162](https://github.com/moongate-community/moongate/issues/162)
* **persistence:** delete captured serials in the world save and reserve serials ([d6a96c6](https://github.com/moongate-community/moongate/commit/d6a96c6f66cf29f65ebe01271a6c6ef99eac2706))
* **plugins:** let plugins register a config section ([4918de2](https://github.com/moongate-community/moongate/commit/4918de2fff9f1fbec8b5efa44a8063157ec168b6)), closes [#159](https://github.com/moongate-community/moongate/issues/159)
* **plugins:** let plugins register their own config section ([81cabbf](https://github.com/moongate-community/moongate/commit/81cabbf5129d5ef5a96b6aea17232594eea6f74d))
* **scripting:** call a function of a global table ([24bb7a3](https://github.com/moongate-community/moongate/commit/24bb7a37aa205c8c510fd6c83b0888efb33a5160)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **scripts:** greet on spawn and call out to players in the wander script ([bc35b7f](https://github.com/moongate-community/moongate/commit/bc35b7f430b2cfb35d2075d5a9c19d781431f403))
* **scripts:** ship a potion item script ([348545a](https://github.com/moongate-community/moongate/commit/348545ac1a89751e27f13b442217686843d73655)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* **sectors:** wake and put to sleep the NPCs with their sectors ([31741ac](https://github.com/moongate-community/moongate/commit/31741ac2e30bb3fb85e93e470a3306761e0ac3c6)), closes [#175](https://github.com/moongate-community/moongate/issues/175)
* **sessions:** let services react when a game session closes ([3d8d9af](https://github.com/moongate-community/moongate/commit/3d8d9af036f29ab0ab7d2010c1b78db74fa826eb))
* **speech:** decode client speech requests ([9f0af66](https://github.com/moongate-community/moongate/commit/9f0af66f94f26e9fc946e6df21795b36d67807c3))
* **speech:** local say and in-game dot commands ([e1b4239](https://github.com/moongate-community/moongate/commit/e1b42394c87a44393d936bcf6e71f3b6278c01cd))
* **speech:** route local speech and in-game commands ([0ebc858](https://github.com/moongate-community/moongate/commit/0ebc858bb426ca08ee902f030e8b6cc54d6dbff0))
* **speech:** send Unicode speech messages ([fdb582b](https://github.com/moongate-community/moongate/commit/fdb582b73ad1a118bd936e10334d05e29852ea9f))
* **targeting:** add the 0x6C target cursor packets ([245bd61](https://github.com/moongate-community/moongate/commit/245bd61487bdade9c706a59e7ef4f460c89dd948)), closes [#151](https://github.com/moongate-community/moongate/issues/151)
* **targeting:** keep one target per player and hand the result to a callback ([c4e4840](https://github.com/moongate-community/moongate/commit/c4e48405cc219033a60dbffc5284b92fa35a0c3f)), closes [#151](https://github.com/moongate-community/moongate/issues/151)
* **targeting:** resolve the client's target response ([6a8a17d](https://github.com/moongate-community/moongate/commit/6a8a17d3252ff0fbc1e3fbd68b0710851ee0ec34)), closes [#151](https://github.com/moongate-community/moongate/issues/151)
* **targeting:** target cursor with typed results ([69aff03](https://github.com/moongate-community/moongate/commit/69aff034c3f9ff92bc605ae6e1adda90224cfb72))
* **templates:** add the brain of a mobile template ([e2c707d](https://github.com/moongate-community/moongate/commit/e2c707d2a55decf886fa9152374c2e97c1a916be)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **templates:** validate the script of an item template ([c1aad9f](https://github.com/moongate-community/moongate/commit/c1aad9f22e94887354b8534cc02a3accac060617)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* **titles:** load fame and karma title table ([8ab7780](https://github.com/moongate-community/moongate/commit/8ab77802862b2724f2a4db2a073b95fa3f323ea5))
* **titles:** register title lookup and document configuration ([79c7d5c](https://github.com/moongate-community/moongate/commit/79c7d5ca48d79ad141ea375f1922fa1f630b4933))
* **titles:** resolve fame and karma title prefixes ([69ad7c4](https://github.com/moongate-community/moongate/commit/69ad7c4337ff9acc2815fee83cbe25c05fb7fe5f))
* **tooltips:** answer tooltip requests and single clicks ([b09518a](https://github.com/moongate-community/moongate/commit/b09518a5438cc683fc9d0de1ea2ef9b2eaca9f34)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* **tooltips:** AOS tooltips (0xD6/0xDC) and single click ([4cc547e](https://github.com/moongate-community/moongate/commit/4cc547ed31f291dabf87fb805bc9687cb76eb6c6))
* **tooltips:** build the tooltip of items and mobiles ([5193ae2](https://github.com/moongate-community/moongate/commit/5193ae272a047b0d390a0afbc5c5e24975ca17e1)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* **tooltips:** build tooltip lines and their packets ([9800867](https://github.com/moongate-community/moongate/commit/9800867fa208ef727abfe3f9fd05745cddc50537)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* **tooltips:** show the common rarity too ([ccc338f](https://github.com/moongate-community/moongate/commit/ccc338f34ec7ba9e014c33d7758afcc0fd143b7c))
* **tooltips:** take the tooltip texts from the message files ([35f8eca](https://github.com/moongate-community/moongate/commit/35f8eca405710a5176ac0e07973f2a32e6f4b8e9))
* **tooltips:** tell clients the tooltip revision (0xDC) ([3d4d094](https://github.com/moongate-community/moongate/commit/3d4d0940aeacc72f828e0796025b4ebf087e743a)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* **world:** add the 0x77 mobile moving packet ([034e214](https://github.com/moongate-community/moongate/commit/034e2140fd9390356d30b30809ddf294173064c1)), closes [#145](https://github.com/moongate-community/moongate/issues/145)
* **world:** compute where a dropped item lands ([75d217c](https://github.com/moongate-community/moongate/commit/75d217c7e6c9d9a04977bfbc31c56f640b0dde02)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **world:** configurable view range, 0xC8 answer and sector queries ([79c1dae](https://github.com/moongate-community/moongate/commit/79c1daeaa99009f669739077ae02c92316f4125f))
* **world:** keep ground items in the sector grid ([f8c5e62](https://github.com/moongate-community/moongate/commit/f8c5e623e3f161de2cdd6892814a98a87266f5b1)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **world:** keep live mobiles in 16x16 sectors per map ([4638779](https://github.com/moongate-community/moongate/commit/463877909eada8a55120fd88a741bd43d38cbc5e)), closes [#145](https://github.com/moongate-community/moongate/issues/145)
* **world:** keep the sector grid aligned with the live mobiles ([8b1f347](https://github.com/moongate-community/moongate/commit/8b1f347b3ffb98c66eafa72c69d47733abbee95b)), closes [#145](https://github.com/moongate-community/moongate/issues/145)
* **world:** players see each other through map sectors ([23e8502](https://github.com/moongate-community/moongate/commit/23e850254ff6d8ea2e71f7b71253216c8f75db85))
* **world:** show a mobile that comes into the world to the players in range ([6d16fbc](https://github.com/moongate-community/moongate/commit/6d16fbc99a2ca6df44306ddc5f0e776dab269817)), closes [#153](https://github.com/moongate-community/moongate/issues/153)
* **world:** show ground items to the players in range ([dc78eae](https://github.com/moongate-community/moongate/commit/dc78eae95ca3621e9ab872fd79ce703e6911aafa)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **world:** show logins, steps and logouts to the players in range ([0155c17](https://github.com/moongate-community/moongate/commit/0155c1705c74f01448573121bbc86a268921f05c)), closes [#145](https://github.com/moongate-community/moongate/issues/145)
* **world:** show players in range as they enter, move and leave ([f9cf942](https://github.com/moongate-community/moongate/commit/f9cf942fc141326568ca0b55427b6f74b5c68828)), closes [#145](https://github.com/moongate-community/moongate/issues/145)
* **world:** wake the sectors around players ([43e8055](https://github.com/moongate-community/moongate/commit/43e80556cf180042561d67f4dc08daf28b2028bd))


### Bug Fixes

* **bootstrap:** resolve the shutdown service before the host runs ([1c080ee](https://github.com/moongate-community/moongate/commit/1c080ee3e96ab066e1d0769b0974fc8e1998636d))
* **characters:** allow one character per account in the world ([bf19a48](https://github.com/moongate-community/moongate/commit/bf19a4895278222824b48f8504cb30c409957f6a))
* **characters:** enter the world on the game loop with the session character ([c63faf6](https://github.com/moongate-community/moongate/commit/c63faf6b5b320676023b36d7555b32ad6fb658f0))
* **characters:** keep a pending character's list position empty instead of moving others into it ([4ebc8e1](https://github.com/moongate-community/moongate/commit/4ebc8e1abc66c5b612e9da06e547297578a31797))
* **characters:** save a leaving character, its items and its merged stacks in one transaction ([f4ec945](https://github.com/moongate-community/moongate/commit/f4ec94500faa6dd573efc4085f6c35316004d89c))
* **ci:** make the unwritable config test hold as root and scope the CI token ([294de6e](https://github.com/moongate-community/moongate/commit/294de6eee8547fa7233af036e0aba3493c3ee5bc))
* **commands:** keep test logger constructor internal ([32a410c](https://github.com/moongate-community/moongate/commit/32a410cf3be43da0cb72be3ba728a0dd89ac8fdb))
* **commands:** resolve command service through DryIoc ([450aaa8](https://github.com/moongate-community/moongate/commit/450aaa8269f6addc7a90a9f6bd0b85ae53b10eb2))
* **commands:** show an untranslated description as is ([ba56816](https://github.com/moongate-community/moongate/commit/ba56816a5753867f2c639dfb075157700378eb0a)), closes [#173](https://github.com/moongate-community/moongate/issues/173)
* **config:** guard plugin sections from review findings ([7c11c0f](https://github.com/moongate-community/moongate/commit/7c11c0f8d0f00f143b0aa722d2ec50e2d5b4ccb1)), closes [#159](https://github.com/moongate-community/moongate/issues/159)
* **config:** reserve the old Ultima section names and tidy usings ([d9e3400](https://github.com/moongate-community/moongate/commit/d9e340072ef11e40fc2c2cec076cbccefdf864f3)), closes [#159](https://github.com/moongate-community/moongate/issues/159)
* **items:** bounce a held item dropped on a paperdoll and never show others' items ([4202f9e](https://github.com/moongate-community/moongate/commit/4202f9ef4f8590ad1ef365489ddc66b16ba2e70e))
* **items:** close the split re-login duplication and the merge loss paths ([5463bb1](https://github.com/moongate-community/moongate/commit/5463bb1bb2efe3f0fac609209dba875ad0308777))
* **items:** leave items held on a cursor alone and tidy the item module ([e4bec45](https://github.com/moongate-community/moongate/commit/e4bec45765210efb4c4eb1b3635a4a1128f76524)), closes [#181](https://github.com/moongate-community/moongate/issues/181)
* **items:** queue on_equip and on_unequip after the move ([d3c27ce](https://github.com/moongate-community/moongate/commit/d3c27ce7a07d1921a2652961b29b7cf74a7646cf)), closes [#183](https://github.com/moongate-community/moongate/issues/183)
* **items:** refuse starting amounts above one pile ([991e42b](https://github.com/moongate-community/moongate/commit/991e42bd09442a825956a3aef56cb68d6db39515))
* **items:** save what a character leaves on the ground and keep held items off other screens ([a8a25b5](https://github.com/moongate-community/moongate/commit/a8a25b5980bd4e38f61b8862e46cc82396249d36)), closes [#149](https://github.com/moongate-community/moongate/issues/149)
* **motd:** stop delivery after resolver cancellation ([875e036](https://github.com/moongate-community/moongate/commit/875e036cb765996408568ef86b672abd0ee9f25b))
* **npcs:** keep the server starting with a broken mobile script and own each script's waits ([c1155da](https://github.com/moongate-community/moongate/commit/c1155da8584cf24fb9ca0a86f778581cb67414f5)), closes [#177](https://github.com/moongate-community/moongate/issues/177)
* **npcs:** never ask the wheel for a zero first delay nor throw into the sectors ([6363a6e](https://github.com/moongate-community/moongate/commit/6363a6e59c7dc7bb2c6b1bf661437e49567d43d9)), closes [#175](https://github.com/moongate-community/moongate/issues/175)
* **npcs:** run on_spawn before anything else a spawned NPC's script gets ([17440d7](https://github.com/moongate-community/moongate/commit/17440d75e60631cd1f3c116fd6ed324b37aa6f14)), closes [#179](https://github.com/moongate-community/moongate/issues/179)
* **packets:** hold packets that arrive while a session's async handler runs ([a51cf69](https://github.com/moongate-community/moongate/commit/a51cf694711e270f7c8750040591981903c638dd))
* **paperdoll:** address review findings ([9cc3d77](https://github.com/moongate-community/moongate/commit/9cc3d77e8eab987a822c9ea9ebb41d35da2e10de)), closes [#162](https://github.com/moongate-community/moongate/issues/162)
* **paperdoll:** address review findings on held and worn items ([9f8aff2](https://github.com/moongate-community/moongate/commit/9f8aff201d322b3f64974c930feca0444eca455a)), closes [#165](https://github.com/moongate-community/moongate/issues/165)
* **persistence:** write unworn items before worn ones ([0dd6eec](https://github.com/moongate-community/moongate/commit/0dd6eecbaba1607d40b56703475097f12c561d80)), closes [#165](https://github.com/moongate-community/moongate/issues/165)
* **redis:** name the unreachable Redis endpoints without leaking the connection string ([75bd78d](https://github.com/moongate-community/moongate/commit/75bd78d2a40e44227aefeb9c87638966b08974cf))
* **speech:** handle encoded ASCII and protect command logs ([2fd3421](https://github.com/moongate-community/moongate/commit/2fd342149f0028c26239310d332f3df6492ba886))
* **speech:** run in-game commands without holding the session's packets ([013ebb5](https://github.com/moongate-community/moongate/commit/013ebb59030bed38d5f36fb1d759bd794823a827)), closes [#151](https://github.com/moongate-community/moongate/issues/151)
* **targeting:** one command at a time per player and the clicked floor of a static ([271a3dc](https://github.com/moongate-community/moongate/commit/271a3dcdb7e213bfa6c8dfd4a20b34df0d3294e2)), closes [#151](https://github.com/moongate-community/moongate/issues/151)
* **tooltips:** address review findings ([850e1f6](https://github.com/moongate-community/moongate/commit/850e1f6b3c2c0cf73c21421e9f1303591e782ce6)), closes [#171](https://github.com/moongate-community/moongate/issues/171)
* **tooltips:** register 0x09, 0xBF and 0xD6 as incoming packets ([7b62ea6](https://github.com/moongate-community/moongate/commit/7b62ea606ca7dfb48efeeb14c10e3fb98edd8759))
* **world:** remove an unregistered leaver from observers and stop a login whose session closed ([ee3862d](https://github.com/moongate-community/moongate/commit/ee3862d1585a90b744d64fffef0afe94d31759b9)), closes [#145](https://github.com/moongate-community/moongate/issues/145)


### Performance Improvements

* **tooltips:** cache tooltips by the fields they depend on ([bb30dbc](https://github.com/moongate-community/moongate/commit/bb30dbc9b09586dea86e954fde84d8dae4a6b7d3)), closes [#171](https://github.com/moongate-community/moongate/issues/171)

## [0.9.0](https://github.com/moongate-community/moongate/compare/v0.8.0...v0.9.0) (2026-09-27)


### Upgrade notes

- Apply the included world database migrations before starting the updated server; use the [migration guide](https://moongate.sh/server/persistence-migrations/).
- Move custom `backpack_template` and `gold_template` settings from `[starting_items]` to `[items]`. The old keys are ignored; the shared settings now serve player characters and spawned mobiles.
- Run `mgboot` again to add missing root files and compare your edited data with the shipped files; existing files are preserved.
- Character creation and saved-character lists are implemented. Character selection, world entry and a playable world are not yet available.

### Features

* **characters:** add the characters config section and the mobile slot column ([c91340a](https://github.com/moongate-community/moongate/commit/c91340a193018c4764bf5f72378c224fd824e200))
* **characters:** create and save player characters with their starting items ([4a190ea](https://github.com/moongate-community/moongate/commit/4a190ea8f17a8818a817c7157babae89e72d1f38))
* **characters:** give new characters their backpack, starting items and gold ([4b284fd](https://github.com/moongate-community/moongate/commit/4b284fd37b43305524afd84ff050e05ed6c7f7b1))
* **characters:** handle the classic and enhanced create-character packets ([aa5e6fa](https://github.com/moongate-community/moongate/commit/aa5e6fa3456a970ad53ab32e68be68182f02e78a))
* **characters:** list the account's saved characters at game login ([c0eb3e7](https://github.com/moongate-community/moongate/commit/c0eb3e7bc028d2ce0fee9753b376e369ecd77763))
* **characters:** load starting item sets and their config ([a305547](https://github.com/moongate-community/moongate/commit/a305547139dcb835850fbdfc40c1337a9c550cf6))
* **characters:** publish character_created to Lua scripts ([64a294d](https://github.com/moongate-community/moongate/commit/64a294dc8783f1a2c6169a72bcf9f673503e383d))
* **core:** add DiceSpec, a dice-notation field for templates, and its TOML converter ([848ccfa](https://github.com/moongate-community/moongate/commit/848ccfa92e5c38be7170110c6b6790ef8b229693))
* **core:** port the dice notation parser from Moongate v2 ([31ade1d](https://github.com/moongate-community/moongate/commit/31ade1d3013997ebc93180e365c186b64f9686f0))
* **items:** add ItemQualityType for the quality prop ([0840f44](https://github.com/moongate-community/moongate/commit/0840f449cc557cbfda51b72df652901710f2dfa6))
* **items:** create items from templates and save them with a real serial ([6869274](https://github.com/moongate-community/moongate/commit/6869274d35f7978b633449f0ba8fc8ff14f42168))
* **items:** give starting items inside a caller's transaction ([ee574da](https://github.com/moongate-community/moongate/commit/ee574daf357a23e2bb19c05002150705fbd67fed))
* **items:** pick a container's layout and a random spot inside it ([2563b53](https://github.com/moongate-community/moongate/commit/2563b53532c7af6b173c3a6b47cc6442bde385bc))
* **loot:** load loot tables and roll them into items ([7872d68](https://github.com/moongate-community/moongate/commit/7872d68509d34c8c34501c9074cd1dd1c8e8bbc6))
* **mobiles:** create mobiles from templates with every random value rolled once ([ada8aab](https://github.com/moongate-community/moongate/commit/ada8aab2fb7411874783311f42579b06ecb5ace8))
* **mobiles:** give spawned NPCs a backpack with their gold, loot and spare equipment ([7a57893](https://github.com/moongate-community/moongate/commit/7a5789319f889b05afcc34ad4bd9a982f40e5ddb))
* **mobiles:** spawn dressed mobiles in one transaction with spawn events ([ce101e0](https://github.com/moongate-community/moongate/commit/ce101e0eacea9d5e09696170c52f4dbc5181a7f8))
* **network:** configure POL client encryption profiles ([4a5b969](https://github.com/moongate-community/moongate/commit/4a5b969c93e9ea10269e92bc1184014502865a93))
* **network:** decrypt UO connections before packet framing ([6db051e](https://github.com/moongate-community/moongate/commit/6db051e947133edcf4ca7d4bd8241d401f1a315e))
* **network:** merge POL-compatible client encryption ([763d4ee](https://github.com/moongate-community/moongate/commit/763d4ee1e7de98971399b4337a4cd216c28a0324)), closes [#115](https://github.com/moongate-community/moongate/issues/115)
* **network:** port POL login and game stream ciphers ([17bdd43](https://github.com/moongate-community/moongate/commit/17bdd437951195f3a7d49752c9a8ac312714d0e5))
* **network:** wire encrypted listeners and startup diagnostics ([807ca9d](https://github.com/moongate-community/moongate/commit/807ca9d0b1831dd89de2caadfbb40d3fa2643cf6))
* **packets:** add the 0x53 popup message packet ([520527b](https://github.com/moongate-community/moongate/commit/520527b0de7ac6fb862feb2cbb81b67da8610d9b))
* **persistence:** add the world mobiles and items tables and register them ([7e3e2bc](https://github.com/moongate-community/moongate/commit/7e3e2bc2cfb4422393cf357dde137e577af55bbc))
* **persistence:** store NPC fields and props on mobiles ([ffb9acd](https://github.com/moongate-community/moongate/commit/ffb9acd44e882ee44a438758686302352f26ec52))
* **persistence:** store the item rarity and an optional loot type ([5764d7c](https://github.com/moongate-community/moongate/commit/5764d7cea5d0e4af719b13a2f3b050747aa537c4))
* **scripting:** add the events module with on and off ([8df6eab](https://github.com/moongate-community/moongate/commit/8df6eab675ad2cc954b8dc7102331c0497473db3))
* **scripting:** deliver bus events to Lua handlers on the game loop ([b7e7186](https://github.com/moongate-community/moongate/commit/b7e71863d72aa9c4c4fa6edf2694114bffe17491))
* **scripting:** list registered event names in the Lua definitions ([5884f42](https://github.com/moongate-community/moongate/commit/5884f42b0fb137274e0ac3e7d45799f7b2f30883))
* **scripting:** register bus events for Lua with AddScriptEvent ([8ce9dfb](https://github.com/moongate-community/moongate/commit/8ce9dfb1c6a8b73dc436c2f5e71e298542118bd3))
* **templates:** add weight, stack, layer, price, decay, loot type and tags to item templates ([b13f061](https://github.com/moongate-community/moongate/commit/b13f061a6407645f2e84007ee9fb398317dfaf18))
* **templates:** load item templates and resolve base_id at startup ([a1a27ae](https://github.com/moongate-community/moongate/commit/a1a27ae03a633ba2672e299a1053dc0f781e5d1a))
* **templates:** load mobile templates and resolve base_id at startup ([e348c5c](https://github.com/moongate-community/moongate/commit/e348c5ccb776d8553984e831f2526ddd85af0ee3))
* **templates:** mark the starting item set every character gets with common = true ([2466883](https://github.com/moongate-community/moongate/commit/246688314a3aa3bbe121e68fa299c46ca7f073c5))
* **templates:** serve the item templates and load them with the game server ([90fe71f](https://github.com/moongate-community/moongate/commit/90fe71f0358d56447a255616c4f07571b9df64fd))
* **templates:** ship UOX3 npcs as mobile templates and all twenty name lists ([a0a0f73](https://github.com/moongate-community/moongate/commit/a0a0f7385333451b40c53c6b03b30e7f35149a02))
* **ultima:** add a name service that picks random names from a list ([cfb1bd8](https://github.com/moongate-community/moongate/commit/cfb1bd8e0f275d3466b78b1740ca325faa2ebfc1))
* **ultima:** add ItemEntity with helpers that keep an item in one place ([5782b09](https://github.com/moongate-community/moongate/commit/5782b092f525ae4c79f40d27186cdf3d13a52f9f))
* **ultima:** add MobileTemplate with dice stats, appearance, equipment, loot and sounds ([96cbb21](https://github.com/moongate-community/moongate/commit/96cbb21a703fac0210ac704e9168e7749a87c763))
* **ultima:** add names.toml with the male and female lists and its loader ([d1a3b78](https://github.com/moongate-community/moongate/commit/d1a3b787d55cad86486ed9763ace20127f688149))
* **ultima:** add the dice Lua module ([3dd8ea3](https://github.com/moongate-community/moongate/commit/3dd8ea38303142194fa7888f15d43c22aa6fa4cc))
* **ultima:** add the mobile gender and notoriety types and the mobile template parts ([05274fe](https://github.com/moongate-community/moongate/commit/05274fe4d13a1942cc061bf7df64e083b2f8b3ac))
* **ultima:** validate mobile templates, naming the template and field ([d8ed3a8](https://github.com/moongate-community/moongate/commit/d8ed3a8f80560f1fa34e5f9fa8c44626585cb81d))
* **uoxconv:** add creature sounds to the npc template that sets the body ([37824db](https://github.com/moongate-community/moongate/commit/37824db911e19b0869ba065c4dc95d0afa3dfa6c))
* **uoxconv:** convert item blocks that inherit their graphic from one parent ([afdbff8](https://github.com/moongate-community/moongate/commit/afdbff823b1c0dd7f24be7ec248836d88b3e31bd))
* **uoxconv:** convert newbie.dfn into starting_items.toml ([9867709](https://github.com/moongate-community/moongate/commit/9867709caa5e97d6f1ec489223d79b6c7ab7bf42))
* **uoxconv:** convert npc equipment, colour lists and loot ([1b3166a](https://github.com/moongate-community/moongate/commit/1b3166a2420f43eddb6cbfc36fc498317a249738))
* **uoxconv:** convert npc stats, combat values, skills, notoriety and tags to dice ([d4592be](https://github.com/moongate-community/moongate/commit/d4592be7e78d9c6dd2e8afb3c7afabcb33b3f0a0))
* **uoxconv:** convert UOX3 name lists into names.toml ([ed2ce90](https://github.com/moongate-community/moongate/commit/ed2ce9008fbbf96474a6c8b065dcbb1bcd597da3))
* **uoxconv:** convert UOX3 npc blocks into mobile templates with base_id inheritance ([195e3a8](https://github.com/moongate-community/moongate/commit/195e3a8848cc5167151cd846381efa0c0a1c43d4))
* **uoxconv:** keep field comments and block labels when parsing dfn files ([710f7bd](https://github.com/moongate-community/moongate/commit/710f7bd957b8032d17b69ddbeb4984c0a8daeffe))
* **uoxconv:** merge male and female npc pairs into one random-gender template ([44f8d9e](https://github.com/moongate-community/moongate/commit/44f8d9e0492d8f3c37dcb55120ef96db58964ad2))
* **uoxconv:** read the converted mobiles back and verify every reference and rule ([441bab2](https://github.com/moongate-community/moongate/commit/441bab260beca90445056220e8e88adda92c35f2))


### Bug Fixes

* **characters:** answer every failed create and login lookup, and read characters without the write gate ([3f633d1](https://github.com/moongate-community/moongate/commit/3f633d174175ab64bd19ae988740d3494f8d1062))
* **characters:** check the starting gold template stacks at startup ([849b549](https://github.com/moongate-community/moongate/commit/849b5499022b4fd238b814e56264403832c40dc7))
* **characters:** put a new character in the first free slot instead of refusing ([5fb3c63](https://github.com/moongate-community/moongate/commit/5fb3c63a0d3503d5990bbb254e2ef035573d97b9))
* **ci:** accept the encryption NOTICE.md in the Network.Packets package ([3bd851c](https://github.com/moongate-community/moongate/commit/3bd851c3fabfb98b50ca7b67609a1deb05932425))
* **core:** exact dice bounds, thread-safe keep rolls and a strict dice parser ([7730faf](https://github.com/moongate-community/moongate/commit/7730faffff8e67f44efc3145e66ba42c9594de60))
* **docs:** preserve inline coverage marker examples ([cbefe46](https://github.com/moongate-community/moongate/commit/cbefe468ae852cb9f9329f366456455f28eb1928))
* **docs:** publish without waiting for server CI ([173c4f2](https://github.com/moongate-community/moongate/commit/173c4f280d16332fa9fb5c5723d771cc5b1866e3))
* **items:** take the tiledata layer only for wearable graphics ([cb4cf71](https://github.com/moongate-community/moongate/commit/cb4cf7137b453e18422a6a775a9f4d099b6756d0))
* **loot:** roll a nested table as many times as its amount, as UOX3 does ([ba1f1ab](https://github.com/moongate-community/moongate/commit/ba1f1ab532a38b3a441ae813175476aeb5ccd002))
* **mobiles:** close the small gaps the spawn review left ([f20903e](https://github.com/moongate-community/moongate/commit/f20903ec7428d2b6d44daf8e44a40fb0872e1c35))
* **mobiles:** follow the template defaults and the real event bus when spawning ([3285804](https://github.com/moongate-community/moongate/commit/3285804061be1c7b10187aca0fa4b5b9c4dd4a40))
* **network:** validate complete encrypted login packets ([ff19871](https://github.com/moongate-community/moongate/commit/ff19871adb29b3faadd84c2a99fa31b4a4b65c8c))
* **persistence:** give the world tables the comments FreeSql expects ([ae1cfe6](https://github.com/moongate-community/moongate/commit/ae1cfe6e8bafb238b3904fcc22c6c228c55a6bc6))
* **persistence:** move a Serial sequence left below its entity's range forward ([a602bb4](https://github.com/moongate-community/moongate/commit/a602bb48a3975903a3ac00146118481247c7a1e9)), closes [#121](https://github.com/moongate-community/moongate/issues/121)
* **persistence:** provision the world schema in the Docker example and require templates ([52b3cd4](https://github.com/moongate-community/moongate/commit/52b3cd45c5758a4e77e4db7b710d5a9d05f21bf6))
* **persistence:** start a generated Serial sequence at the entity's declared range ([da0d7a8](https://github.com/moongate-community/moongate/commit/da0d7a8921a9485725eaba2406833022f9026cf8)), closes [#121](https://github.com/moongate-community/moongate/issues/121)
* **scripting:** keep a Lua event dispatch from faulting the game loop ([c4e59eb](https://github.com/moongate-community/moongate/commit/c4e59eb4ebb45e30ac3f7fc09e69d3c89ed0b3fd))
* **scripting:** warn and count Lua events dropped by a full game loop ([c57481d](https://github.com/moongate-community/moongate/commit/c57481d45188b8abcbf6bbba86df93fb9d30e156))
* **templates:** give each child template its own copy of inherited tags ([a9b7296](https://github.com/moongate-community/moongate/commit/a9b7296bf87c53ff60e9ed97984d9835e8abdae0))
* **uoxconv:** convert random NPC picks, fix gender and race slips in UOX3 data ([2d47b7e](https://github.com/moongate-community/moongate/commit/2d47b7e7fbcdba93b821e53ccf80943fa0a7b3a3))
* **uoxconv:** drop base_id when a block gets itself ([575bf70](https://github.com/moongate-community/moongate/commit/575bf70cf7d52c5bda20d5dd6c1ebfebe3d0831f))
* **uoxconv:** follow item aliases for npc equipment and leave differing pair sounds unset ([61208d4](https://github.com/moongate-community/moongate/commit/61208d49bdb8f26e06ebf1f5bbd262ae56ab629a))
* **uoxconv:** inline get= targets that have no id of their own ([64d36cf](https://github.com/moongate-community/moongate/commit/64d36cf08d71156703c889d793137615028e82c7))
* **uoxconv:** keep ids from each block's own lines ([d6d1839](https://github.com/moongate-community/moongate/commit/d6d183922fac9687ce7561587ddedd5430714f84))
* **uoxconv:** leave a blank line after the header of names.toml and starting_items.toml ([5e60e00](https://github.com/moongate-community/moongate/commit/5e60e00174e6eedea717d8fb569232fe4ac34f4f))
* **uoxconv:** read ids, colours, numbers, empty tags and duplicates as UOX3 does ([28a69f2](https://github.com/moongate-community/moongate/commit/28a69f229d500b80f533f065aaa6a73b0b925e66))


### Performance Improvements

* **ci:** keep disposable PostgreSQL data in memory ([1b53afe](https://github.com/moongate-community/moongate/commit/1b53afeafaa57aeb7ee02bc7b296959e84b9ca02))

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
