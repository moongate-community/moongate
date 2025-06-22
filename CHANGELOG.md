# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="0.3.0"></a>
## [0.3.0](https://www.github.com/moongate-community/moongate/releases/tag/v0.3.0) (2025-06-20)

### Features

* **CharactersHandler.cs:** add event bus service to CharactersHandler constructor to publish CharacterCreatedEvent ([1fc282f](https://www.github.com/moongate-community/moongate/commit/1fc282fa95e33b503915f900e959625868b9767f))
* **moongate:** update Moongate Server version to v0.1.23.0 in TypeScript Definitions ([fd5b8bd](https://www.github.com/moongate-community/moongate/commit/fd5b8bdbb68e83c0648c0bc0a97f08ecc898b759))
* **moongate:** update Moongate Server version to v0.1.27.0 in TypeScript Definitions ([64edde6](https://www.github.com/moongate-community/moongate/commit/64edde69982d1805b81e76ae6428afa740be8f69))
* **Moongate:** add IPersistenceLoadSave interface for load and save operations ([9f5620d](https://www.github.com/moongate-community/moongate/commit/9f5620deac8cc1e41189ca01da6f63b9d870191d))
* **Program.cs:** add support for registering CharacterDeletePacket handler in CharactersHandler to handle character deletion ([2e56b8a](https://www.github.com/moongate-community/moongate/commit/2e56b8a159d7430f3549d5a8bfa8d04a57964e54))

<a name="0.2.0"></a>
## [0.2.0](https://www.github.com/moongate-community/moongate/releases/tag/v0.2.0) (2025-06-19)

### Features

* added professions ([a5c3f8e](https://www.github.com/moongate-community/moongate/commit/a5c3f8e53eabef9b88a138132ddf7c289efd9080))
* added professions ([5af7176](https://www.github.com/moongate-community/moongate/commit/5af7176e5aabd1cf82d8aab4d2e3bb708f8cc615))
* **Body.cs:** add new struct Body with properties and methods to handle different body types and characteristics ([6706004](https://www.github.com/moongate-community/moongate/commit/67060047de44fa484dcb07ef6bed20a293c76c6b))
* **CharacterCreationPacket.cs:** add CharacterCreationPacket class to handle character creation data ([d071e65](https://www.github.com/moongate-community/moongate/commit/d071e65fc0bee0eea2f8e7fbd104e9e05a942714))
* **ClientVersionConverter.cs:** add ClientVersionConverter to handle JSON serialization and deserialization of ClientVersion objects ([6c70d76](https://www.github.com/moongate-community/moongate/commit/6c70d76adfec45cb080dfd2660c4c358ec7be140))
* **CollectionsExtensions.cs:** add CollectionExtensions class with RandomElement method ([da43b20](https://www.github.com/moongate-community/moongate/commit/da43b20ea760528bb4c265a0b2b5d5d3861c7793))
* **Compression:** add Deflate class for compression functionality ([8dc9d2b](https://www.github.com/moongate-community/moongate/commit/8dc9d2b6f1f5587a5c7050f9d5d672d723054962))
* **expansions.json:** add data for various game expansions including their features, map selection flags, character list flags, housing flags, and mobile status version ([c010d8c](https://www.github.com/moongate-community/moongate/commit/c010d8c19a58ecd4de8edd06a1e1b396f9e77fbb))
* **expansions.json:** add data for various game expansions to support different features and maps ([ce5e052](https://www.github.com/moongate-community/moongate/commit/ce5e052fe7124cd5cda8d79705203e3fcc773968))
* **index.d.ts:** update Moongate Server version to v0.1.19.0 and update auto-generated documentation timestamp ([9b19f14](https://www.github.com/moongate-community/moongate/commit/9b19f1492b79cdbd0fec5523d52bb369d40d7324))
* **index.d.ts:** update Moongate Server version to v0.1.7.0 and auto-generated documentation date ([a8e4e45](https://www.github.com/moongate-community/moongate/commit/a8e4e4593060507e6b4774b4297300ff9c754025))
* **JsonProfession:** add JSON representation classes for professions, skills, stats, and root object ([141def1](https://www.github.com/moongate-community/moongate/commit/141def16b0b552617d4fffc15777bc47dc157bba))
* **MultiComponentList.cs:** add MultiComponentList class with constructors and methods ([7a89b7e](https://www.github.com/moongate-community/moongate/commit/7a89b7ebe234eae3b3a9fa29175354e430f184d1))
* **professions:** restructure profession data from txt files to a JSON format for better organization and readability ([0f493fe](https://www.github.com/moongate-community/moongate/commit/0f493fea93bd016d9b1ad9ee41f48fd9ee6046eb))
* **SupportFeaturesPacket.cs:** add new class SupportFeaturesPacket to handle feature flags in packets ([e79b29c](https://www.github.com/moongate-community/moongate/commit/e79b29cf740e80081041275b387cb8c3b9543e2a))
* **UOJsonContext.cs:** add support for new classes and converters related to expansions and versions ([7953ac6](https://www.github.com/moongate-community/moongate/commit/7953ac60d06fc5d130a96df19f1f337cdb79b65b))

### Bug Fixes

* **index.d.ts:** update Moongate Server version in TypeScript definitions to v0.1.13.0 ([ba3193f](https://www.github.com/moongate-community/moongate/commit/ba3193fa40d8040f70c089dc540e58a8e2af02fd))
* **Prof.txt:** remove trailing whitespace in the Skill lines for better consistency ([208c166](https://www.github.com/moongate-community/moongate/commit/208c1663f5bb1c4e9265ec137c96a404846093cc))

<a name="0.1.1"></a>
## [0.1.1](https://www.github.com/moongate-community/moongate/releases/tag/v0.1.1) (2025-06-18)

<a name="0.1.0"></a>
## [0.1.0](https://www.github.com/moongate-community/moongate/releases/tag/v0.1.0) (2025-06-18)

### Features

* **Entry3D.cs, Entry5D.cs, FileIndex.cs, UOFiles.cs:** Add new data structures Entry3D, Entry5D, FileIndex, and UOFiles class to handle file indexing and data loading functionalities. ([afa1082](https://www.github.com/moongate-community/moongate/commit/afa10826c5d4e24159bb08cb0f15644da3917c00))
* **Map.cs, MapRules.cs, MapSelectionFlags.cs, Season.cs:** add new map-related classes and enums for managing maps, map rules, map selection flags, and seasons in the game. ([3878b99](https://www.github.com/moongate-community/moongate/commit/3878b997942f1770fe079030ee096f9bf48cc966))
* **Moongate.UO.Data:** add new tile data structures HuedTile, LandTile, MTile, ([5a5a986](https://www.github.com/moongate-community/moongate/commit/5a5a98651bb41c694fc4364002aa8b052866fd62))
* **Program.cs:** add Moongate.UO.Data.Files namespace to Program.cs for new Art class ([1307a02](https://www.github.com/moongate-community/moongate/commit/1307a027b0fc390bcecb10443a50e26a08b653db))
* **StaticTile.cs:** add new struct StaticTile to represent a static tile in the game world ([a80fdd7](https://www.github.com/moongate-community/moongate/commit/a80fdd74a36416fa305c6976bb643f3e911cceca))
* **Tile.cs:** add new struct Tile with properties for ID and Z, and methods for setting values and comparing tiles ([7f85232](https://www.github.com/moongate-community/moongate/commit/7f85232fbc637427e14c89aabfe80cb3c2903144))
* **TileMatrix.cs:** add new TileMatrix class to manage tile data for the game ([434949b](https://www.github.com/moongate-community/moongate/commit/434949bee43d685c1ff5e35c01d3a7558bd1adc6))

<a name="0.0.1"></a>
## [0.0.1](https://www.github.com/moongate-community/moongate/releases/tag/v0.0.1) (2025-06-18)

### Features

* add Dockerfile for containerizing the application and defining the base ([76963f0](https://www.github.com/moongate-community/moongate/commit/76963f01eaeeeb21d67cb1e43dad47e4c8ddf1cd))
* **accounts.mga:** update Moongate Server JavaScript API TypeScript Definitions to v0.0.76.0 ([8b7139d](https://www.github.com/moongate-community/moongate/commit/8b7139df049707c2acf5438921c09c72c6929ad8))
* **AccountService.cs:** add support for creating and saving accounts with event ([424cdc3](https://www.github.com/moongate-community/moongate/commit/424cdc3393d1a5066252ec27a50ca13ed9002ab3))
* **AccountService.cs:** publish AccountCreatedEvent with account ID and log account creation details ([a933911](https://www.github.com/moongate-community/moongate/commit/a9339111636ebb072c9d9f6e7666f2210767dbff))
* **autoversionize.yml:** add GitHub workflow for automatic versioning and releasing of the project using Versionize tool and GitHub actions ([fc82f5d](https://www.github.com/moongate-community/moongate/commit/fc82f5d00bf4d992ea3877e6886e120d680a0424))
* **autoversionize.yml:** remove unnecessary flag '--changelog-all' from versionize command ([e6e3e64](https://www.github.com/moongate-community/moongate/commit/e6e3e64b6192b1aea03141ff4772cd0b10a8806a))
* **CityInfo.cs:** add CityInfo class to handle city information with location and description ([5cc9138](https://www.github.com/moongate-community/moongate/commit/5cc9138f5d4b25ef4fc9875667eef2391aaec761))
* **commands:** add CommandDefinition and CommandSystemContext classes for defining and handling commands ([619b48d](https://www.github.com/moongate-community/moongate/commit/619b48ddfd2ae27a58320a4d4502b74523c0cc90))
* **CommandSystemContext.cs:** add CommandSystemContext class to handle command system logic ([605c24d](https://www.github.com/moongate-community/moongate/commit/605c24d1b19685eee532107e527f83fd323ec874))
* **CommandSystemService.cs:** add RegisterDefaultCommands method to automatically register default commands on service initialization ([c5b36b6](https://www.github.com/moongate-community/moongate/commit/c5b36b6dbeee23fbe830a515e9d043a6514db8ef))
* **Encryption:** add ClientEncryptor class to handle encryption and decryption of data ([4a0408f](https://www.github.com/moongate-community/moongate/commit/4a0408fe3354f8ce080a4cca993443f5eb8b64eb))
* **EntityDataBlock.cs:** add DataHash property to EntityDataBlock struct for uniqueness ([910a004](https://www.github.com/moongate-community/moongate/commit/910a0049c493bc3d6ab126b657f214905be40ff2))
* **EventLoopService.cs:** implement EventLoopService to manage actions execution based on priorities and in FIFO order within each priority level ([b5ba05f](https://www.github.com/moongate-community/moongate/commit/b5ba05fb0426346d02472fb6d089f6e5436ad073))
* **EventLoopServiceExtensions.cs:** add EnqueueToLoop method to enqueue actions with ([dfad661](https://www.github.com/moongate-community/moongate/commit/dfad66142b8f41b8a1a12fcd1305de0fbc63e17a))
* **EventLoopServiceExtensions.cs:** import Moongate.Core.Extensions.Strings to use string extensions in the file ([7d047cd](https://www.github.com/moongate-community/moongate/commit/7d047cd3237b7fda5cbabac8e8bf8d53303e39f1))
* **GameNetworkSession.cs:** add Account and Seed properties to GameNetworkSession for better session management ([60014b1](https://www.github.com/moongate-community/moongate/commit/60014b1d0ceb0d43f4c3105acb0e7ad147cbc722))
* **Gen2GcCallback.cs:** add Gen2GcCallback class to schedule callbacks on Gen 2 GC ([ff2ca00](https://www.github.com/moongate-community/moongate/commit/ff2ca00e15f5c86014213ba9e5295060003ec5e0))
* **HexStringConverter.cs:** add HexStringConverter class to provide methods for converting byte arrays to hexadecimal strings and vice versa ([94b52f7](https://www.github.com/moongate-community/moongate/commit/94b52f78b847f38412db8dbf4db6f3ecb028e758))
* **ICommandSystemService.cs:** add ExecuteCommandAsync method to ICommandSystemService interface for executing commands asynchronously ([1db1683](https://www.github.com/moongate-community/moongate/commit/1db1683a7d81d4e6dcd520e7a46f6f4e73ddc896))
* **index.d.ts:** update Moongate Server version to v0.0.105.0 and update auto-generated documentation timestamp ([d06c3d2](https://www.github.com/moongate-community/moongate/commit/d06c3d2eb2b337e99b186eb2e691d89c5a61f24b))
* **index.d.ts:** update Moongate Server version to v0.0.70.0 in TypeScript definitions ([715a2b1](https://www.github.com/moongate-community/moongate/commit/715a2b1207210940d426310383cc3545f72a971e))
* **index.d.ts:** update Moongate Server version to v0.0.77.0 and regenerate documentation ([98414c4](https://www.github.com/moongate-community/moongate/commit/98414c459ef2a207e25db1e791dcc1bdd052ef47))
* **moongate:** add moongate.json configuration file with network and web server settings ([5a6bb5f](https://www.github.com/moongate-community/moongate/commit/5a6bb5f9058a9513e5529154745716cb9e18708e))
* **moongate:** update Moongate Server version to 0.0.67.0 ([e2bce27](https://www.github.com/moongate-community/moongate/commit/e2bce271f8ccf49e408856224bc5ab02672cd7da))
* **moongate:** update Moongate Server version to v0.0.106.0 in JavaScript API ([70d1565](https://www.github.com/moongate-community/moongate/commit/70d1565fb008d68101f5ca2d9f6ad699b730743d))
* **Moongate:** add MoongateBootstrap class to handle server initialization and startup tasks ([3c6537d](https://www.github.com/moongate-community/moongate/commit/3c6537d94e8b64ce28e7346d630ec08b99ff4a88))
* **Moongate:** add support for packet logging by introducing MoongateRuntimeConfig class ([0e8ccd0](https://www.github.com/moongate-community/moongate/commit/0e8ccd0e978786e45b1996522b6fd0d209115d40))
* **Moongate.Core.Server.csproj:** add DryIoc.dll package reference with version 5.4.3 ([74b5c5d](https://www.github.com/moongate-community/moongate/commit/74b5c5da12b6912cb5e39ef0b47092b73b400837))
* **Moongate.Server.csproj:** add Spectre.Console package reference to enable rich console output formatting ([0133f9a](https://www.github.com/moongate-community/moongate/commit/0133f9a133b7e2f47530f0a80dec88317c8a7929))
* **Moongate.sln:** add Moongate.Core.Persistence project to the solution ([00b18fd](https://www.github.com/moongate-community/moongate/commit/00b18fdd3c24de5542b982c3acae17359ab1ffb6))
* **Moongate.sln:** add Moongate.UO project to the solution ([503d838](https://www.github.com/moongate-community/moongate/commit/503d838823b244c29dafdbfaab87d97b9a579cbc))
* **Moongate.sln:** add Moongate.UO.Data project to the solution ([2d21500](https://www.github.com/moongate-community/moongate/commit/2d21500e6ac33afc673d2dc3ec9ce74a92945aca))
* **Moongate.UO.Data.csproj:** add reference to Moongate.Core.Server.csproj for better project structure ([720cf9a](https://www.github.com/moongate-community/moongate/commit/720cf9a4a3bc98fe06c18ee6e9e7b904165a544a))
* **MoongateBootstrap.cs:** add ConfigureScriptEngine event to allow configuring script engine service ([bfa67e4](https://www.github.com/moongate-community/moongate/commit/bfa67e43000b4713a60aed8122eec86fa5c91a51))
* **MoongateBootstrap.cs:** add support for loading Moongate server configuration ([76eb9bd](https://www.github.com/moongate-community/moongate/commit/76eb9bde62c6bd65c58efaa52cabbe5a1d90b045))
* **MoongateBootstrap.cs:** remove unused AddService call in MoongateBootstrap ([9b21ae3](https://www.github.com/moongate-community/moongate/commit/9b21ae323e32b3d58d00285d643ad8d79e13ce80))
* **NetworkCompression.cs:** add comments to document compression algorithm and methods ([84cdab2](https://www.github.com/moongate-community/moongate/commit/84cdab2e361bddfb58b03ba3590a7c2d191d9c06))
* **NetworkCompression.cs:** add NetworkCompression class for handling outgoing packet compression ([ed2d4a5](https://www.github.com/moongate-community/moongate/commit/ed2d4a5a612c18dda39f5e7585050d974e67663e))
* **NetworkService:** refactor PacketDefinitionData struct to remove Builder ([0e6bc5c](https://www.github.com/moongate-community/moongate/commit/0e6bc5c07d79ca602162c32681d9f43621a16746))
* **PacketExtensions.cs:** add PacketExtensions class with ToPacketString method ([63662f3](https://www.github.com/moongate-community/moongate/commit/63662f3db3bd2f37acca0c5d44199ab50105489b))
* **Program.cs:** add AccountCommands and AccountModule to support account-related functionalities in the Moongate server ([d528315](https://www.github.com/moongate-community/moongate/commit/d52831581b08f449a3de11c9a2da459559913357))
* **RawInterpolatedStringHandler.cs:** add a new file for handling raw interpolated strings in Moongate.Core.Buffers namespace ([1690318](https://www.github.com/moongate-community/moongate/commit/1690318c706d2783332ec90c858bc2a60778ac54))
* **run_aot.sh:** add script to run Ahead-of-Time compilation for improved performance ([79dc81c](https://www.github.com/moongate-community/moongate/commit/79dc81c8066eba19fbeeeb6524afbebde07b83e3))
* **run_aot.sh:** improve script to detect OS and architecture for building and running ([92f2736](https://www.github.com/moongate-community/moongate/commit/92f2736191502b9d3099aab29a5827b915c168a1))
* **server:** add DiagnosticServiceConfig class with default values for metrics interval and PID file name ([a3ea36c](https://www.github.com/moongate-community/moongate/commit/a3ea36c0a7441c3362848a9c4ecc1397e4fdc440))
* **server:** add new DiagnosticMetrics class to track various system metrics ([92af02a](https://www.github.com/moongate-community/moongate/commit/92af02a7be6bedf3b076508267dbc9363d600690))
* **server:** add new ScriptFunctionAttribute and ScriptModuleAttribute classes ([508d3fe](https://www.github.com/moongate-community/moongate/commit/508d3fe62ca7e3ac9d78471818d5f771f44bc88b))
* **server:** add support for Moongate.Persistence assembly in ILLink descriptors ([e2930d6](https://www.github.com/moongate-community/moongate/commit/e2930d6dc336a1f9d6c20721f4e074a0e5033423))
* **server:** add support for process.env.PORT environment variable to be able to run app on a configurable port ([f6badf2](https://www.github.com/moongate-community/moongate/commit/f6badf28881aef491c28817d5b753ab783e6ddef))
* **ServiceDefinitionObject.cs:** add ServiceDefinitionObject record struct for defining service types and implementations ([efb4f2b](https://www.github.com/moongate-community/moongate/commit/efb4f2bb42386d53ece74c47e7a3ffe4e27bed4e))
* **SkillInfo.cs:** add SkillInfo class to store detailed information about skills ([0c00dca](https://www.github.com/moongate-community/moongate/commit/0c00dcab12953fc0238ce2a7aae0ec0c6764984b))
* **skills.json:** add a new file with 58 different skills and their attributes to the game for character progression and customization ([678e038](https://www.github.com/moongate-community/moongate/commit/678e0385dfb9b644a1789837c58d0b1d3f20a8fd))
* **SpanReader.cs:** add SpanReader class to Moongate.Core.Spans namespace for ([85b660a](https://www.github.com/moongate-community/moongate/commit/85b660aaf2015c9d436032767388504f6e17fc80))
* **SpanWriter.cs:** add a new SpanWriter class to handle writing spans of bytes efficiently and safely ([b640559](https://www.github.com/moongate-community/moongate/commit/b6405597ba6d735384a9f5c643686df3bd1223ad))
* **STArrayPool.cs:** implement a custom ArrayPool for single-threaded unsafe usage ([662a7d3](https://www.github.com/moongate-community/moongate/commit/662a7d3e194435d9c0993d869625e717dec25219))
* **StringHelper.cs:** add various string manipulation methods for handling strings efficiently ([a2b2b30](https://www.github.com/moongate-community/moongate/commit/a2b2b303e3bcaf1dcb1519d9ec802eb497c36b03))
* **StringMethodExtension.cs:** add StringMethodExtension class with methods for various string case conversions ([260b4c1](https://www.github.com/moongate-community/moongate/commit/260b4c16904287ab25a06c16e32fec58d40d987d))
* **UoNetworkPacket.cs:** add using statement for Moongate.Core.Spans namespace ([c7e9b4a](https://www.github.com/moongate-community/moongate/commit/c7e9b4ad606b7af3cba19362aa869b5ec0ed4ebf))
* **ValueStringBuilder.cs:** add ValueStringBuilder class to Moongate.Core.Buffers namespace ([6f8a6ef](https://www.github.com/moongate-community/moongate/commit/6f8a6efcf96c7b64a3b25f8cca2e09f5a8b29d1e))

### Bug Fixes

* **tests:** update NUnit.Analyzers package version to 4.9.1 to fix known issues and improve code analysis accuracy ([2f177ac](https://www.github.com/moongate-community/moongate/commit/2f177ac0268dd319bbd556d85426e49cd49aded8))

