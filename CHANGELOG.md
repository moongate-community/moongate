# Changelog

## [0.10.0](https://github.com/moongate-community/moongate/compare/v0.9.0...v0.10.0) (2026-09-29)


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
