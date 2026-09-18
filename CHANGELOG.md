# Changelog

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
