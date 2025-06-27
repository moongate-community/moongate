/**
 * Moongate Server v0.3.41.0 JavaScript API TypeScript Definitions
 * Auto-generated documentation on 2025-06-27 15:43:16
 **/

// Constants

/**
 * VERSION constant 
 * ""0.3.41.0""
 */
declare const VERSION: string;


/**
 * LoggerModule module
 */
declare const logger: {
    /**
     * Log info
     * @param message string
     * @param args any[]
     */
    info(message: string, args: any[]): void;
    /**
     * Log warning
     * @param message string
     * @param args any[]
     */
    warn(message: string, args: any[]): void;
    /**
     * Log error
     * @param message string
     * @param args any[]
     */
    error(message: string, args: any[]): void;
    /**
     * Log debug
     * @param message string
     * @param args any[]
     */
    debug(message: string, args: any[]): void;
};

/**
 * AccountModule module
 */
declare const accounts: {
    /**
     * Create new account
     * @param username string
     * @param password string
     * @param accountLevel string
     */
    createAccount(username: string, password: string, accountLevel?: string): void;
    /**
     * Change password of account
     * @param accountName string
     * @param newPassword string
     * @returns boolean
     */
    changePassword(accountName: string, newPassword: string): boolean;
};

/**
 * SystemModule module
 */
declare const system: {
    /**
     * Get server time
     * @returns string
     */
    getServerTime(): string;
    /**
     * Get server uptime
     * @returns string
     */
    getServerUptime(): string;
    /**
     * Delay via event loop
     * @param milliseconds number
     */
    delay(milliseconds: number): void;
};

/**
 * CommandsModule module
 */
declare const commands: {
    /**
     * Register new command
     * @param commandName string
     * @param handler (arg: ICommandSystemContext) => any
     * @param description string
     * @param accountLevel accountLevelType
     * @param source commandSourceType
     */
    registerCommand(commandName: string, handler: (arg: ICommandSystemContext) => any, description?: string, accountLevel?: accountLevelType, source?: commandSourceType): void;
};

/**
 * LoadScriptModule module
 */
declare const scripts: {
    /**
     * Include script
     * @param scriptName string
     */
    includeScript(scriptName: string): void;
    /**
     * Include directory
     * @param directoryName string
     */
    includeDirectory(directoryName: string): void;
};

/**
 * ConsoleModule module
 */
declare const console: {
    /**
     * Log info
     * @param message string
     * @param args any[]
     */
    log(message: string, args: any[]): void;
};

/**
 * CommonEventModule module
 */
declare const events: {
    /**
     * Register handler for CharacterInGameEvent
     * @param handler (arg: ICharacterInGameEvent) => any
     */
    onCharacterInGame(handler: (arg: ICharacterInGameEvent) => any): void;
    /**
     * Register handler for character created event
     * @param handler ICharacterCreatedHandler
     */
    onCharacterCreated(handler: ICharacterCreatedHandler): void;
};


/**
 * Generated enum for Moongate.Core.Server.Types.AccountLevelType
 */
export enum accountLevelType {
    User = 0,
    GM = 1,
    Admin = 2,
}

/**
 * Generated enum for Moongate.Core.Server.Types.CommandSourceType
 */
export enum commandSourceType {
    None = 0,
    Console = 1,
    InGame = 2,
    All = 3,
}

/**
 * Generated enum for Moongate.UO.Data.Types.NetworkSessionFeatureType
 */
export enum networkSessionFeatureType {
    None = 0,
    Compression = 1,
    Encryption = 2,
}

/**
 * Generated enum for Moongate.UO.Data.Types.NetworkSessionStateType
 */
export enum networkSessionStateType {
    None = 0,
    Connecting = 1,
    Connected = 2,
    Authenticated = 3,
    InGame = 4,
    Disconnected = 5,
    Error = 6,
}

/**
 * Generated enum for Moongate.UO.Data.Types.GenderType
 */
export enum genderType {
    Male = 0,
    Female = 1,
}

/**
 * Generated enum for Moongate.UO.Data.Types.DirectionType
 */
export enum directionType {
    North = 0,
    Right = 1,
    East = 2,
    Down = 3,
    South = 4,
    Left = 5,
    West = 6,
    Up = 7,
    Mask = 7,
    Running = 128,
    ValueMask = 135,
}

/**
 * Generated enum for Moongate.UO.Data.Types.ItemLayerType
 */
export enum itemLayerType {
    Invalid = 0,
    FirstValid = 1,
    OneHanded = 1,
    TwoHanded = 2,
    Shoes = 3,
    Pants = 4,
    Shirt = 5,
    Helm = 6,
    Gloves = 7,
    Ring = 8,
    Talisman = 9,
    Neck = 10,
    Hair = 11,
    Waist = 12,
    InnerTorso = 13,
    Bracelet = 14,
    Unused_xF = 15,
    FacialHair = 16,
    MiddleTorso = 17,
    Earrings = 18,
    Arms = 19,
    Cloak = 20,
    Backpack = 21,
    OuterTorso = 22,
    OuterLegs = 23,
    InnerLegs = 24,
    LastUserValid = 24,
    Mount = 25,
    ShopBuy = 26,
    ShopResale = 27,
    ShopSell = 28,
    Bank = 29,
    LastValid = 29,
}

/**
 * Generated enum for Moongate.UO.Data.Types.Notoriety
 */
export enum notoriety {
    Invalid = 0,
    Innocent = 1,
    Friend = 2,
    Animal = 3,
    Criminal = 4,
    Enemy = 5,
    Murdered = 6,
    Invulnerable = 7,
}

/**
 * Generated enum for Moongate.UO.Data.Bodies.BodyType
 */
export enum bodyType {
    Empty = 0,
    Monster = 1,
    Sea = 2,
    Animal = 3,
    Human = 4,
    Equipment = 5,
}

/**
 * Generated enum for Moongate.UO.Data.Types.SeasonType
 */
export enum seasonType {
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3,
    Desolation = 4,
}

/**
 * Generated enum for Moongate.UO.Data.Maps.MapRules
 */
export enum mapRules {
    None = 0,
    FeluccaRules = 0,
    Internal = 1,
    FreeMovement = 2,
    BeneficialRestrictions = 4,
    HarmfulRestrictions = 8,
    TrammelRules = 14,
}

/**
 * Generated enum for Moongate.UO.Data.Skills.SkillLock
 */
export enum skillLock {
    Up = 0,
    Down = 1,
    Locked = 2,
}

/**
 * Generated enum for Moongate.UO.Data.Types.Stat
 */
export enum stat {
    Str = 0,
    Dex = 1,
    Int = 2,
}


/**
 * Generated interface for Moongate.Core.Server.Data.Internal.Commands.CommandSystemContext
 */
interface ICommandSystemContext {
    /**
     * sourceType
     */
    sourceType: commandSourceType;
    /**
     * sessionId
     */
    sessionId: string;
    /**
     * command
     */
    command: string;
    /**
     * arguments
     */
    arguments: string[];
}

/**
 * Generated interface for Moongate.UO.Data.Events.Characters.CharacterInGameEvent
 */
interface ICharacterInGameEvent {
    /**
     * gameSession
     */
    gameSession: IGameSession;
    /**
     * mobile
     */
    mobile: IUOMobileEntity;
}

/**
 * Generated interface for Moongate.Server.Modules.CommonEventModule+CharacterCreatedHandler
 */
interface ICharacterCreatedHandler {
    /**
     * target
     */
    target: any;
    /**
     * method
     */
    method: any;
}

/**
 * Generated interface for Moongate.UO.Data.Session.GameSession
 */
interface IGameSession {
    /**
     * sessionId
     */
    sessionId: string;
    /**
     * account
     */
    account: IUOAccountEntity;
    /**
     * seed
     */
    seed: number;
    /**
     * mobile
     */
    mobile: IUOMobileEntity;
    /**
     * pingSequence
     */
    pingSequence: number;
    /**
     * moveSequence
     */
    moveSequence: any;
    /**
     * features
     */
    features: networkSessionFeatureType;
    /**
     * state
     */
    state: networkSessionStateType;
    /**
     * networkClient
     */
    networkClient: IMoongateTcpClient;
}

/**
 * Generated interface for Moongate.UO.Data.Persistence.Entities.UOMobileEntity
 */
interface IUOMobileEntity {
    /**
     * id
     */
    id: ISerial;
    /**
     * name
     */
    name: string;
    /**
     * title
     */
    title: string;
    /**
     * isPlayer
     */
    isPlayer: boolean;
    /**
     * x
     */
    x: number;
    /**
     * y
     */
    y: number;
    /**
     * z
     */
    z: number;
    /**
     * strength
     */
    strength: number;
    /**
     * dexterity
     */
    dexterity: number;
    /**
     * intelligence
     */
    intelligence: number;
    /**
     * hits
     */
    hits: number;
    /**
     * mana
     */
    mana: number;
    /**
     * stamina
     */
    stamina: number;
    /**
     * maxHits
     */
    maxHits: number;
    /**
     * maxMana
     */
    maxMana: number;
    /**
     * maxStamina
     */
    maxStamina: number;
    /**
     * gender
     */
    gender: genderType;
    /**
     * race
     */
    race: IRace;
    /**
     * body
     */
    body: IBody;
    /**
     * hairStyle
     */
    hairStyle: number;
    /**
     * hairHue
     */
    hairHue: number;
    /**
     * facialHairStyle
     */
    facialHairStyle: number;
    /**
     * facialHairHue
     */
    facialHairHue: number;
    /**
     * skinHue
     */
    skinHue: number;
    /**
     * profession
     */
    profession: IProfessionInfo;
    /**
     * location
     */
    location: IPoint3D;
    /**
     * map
     */
    map: IMap;
    /**
     * direction
     */
    direction: directionType;
    /**
     * level
     */
    level: number;
    /**
     * experience
     */
    experience: number;
    /**
     * skillPoints
     */
    skillPoints: number;
    /**
     * statPoints
     */
    statPoints: number;
    /**
     * isAlive
     */
    isAlive: boolean;
    /**
     * isHidden
     */
    isHidden: boolean;
    /**
     * isFrozen
     */
    isFrozen: boolean;
    /**
     * isWarMode
     */
    isWarMode: boolean;
    /**
     * isFlying
     */
    isFlying: boolean;
    /**
     * isBlessed
     */
    isBlessed: boolean;
    /**
     * ignoreMobiles
     */
    ignoreMobiles: boolean;
    /**
     * isPoisoned
     */
    isPoisoned: boolean;
    /**
     * isParalyzed
     */
    isParalyzed: boolean;
    /**
     * isInvulnerable
     */
    isInvulnerable: boolean;
    /**
     * created
     */
    created: any;
    /**
     * lastLogin
     */
    lastLogin: any;
    /**
     * lastSaved
     */
    lastSaved: any;
    /**
     * totalPlayTime
     */
    totalPlayTime: any;
    /**
     * equipment
     */
    equipment: Map<itemLayerType, IItemReference>;
    /**
     * notoriety
     */
    notoriety: notoriety;
    /**
     * skills
     */
    skills: ISkillEntry[];
    /**
     * gold
     */
    gold: number;
}

/**
 * Generated interface for Moongate.UO.Data.Persistence.Entities.UOAccountEntity
 */
interface IUOAccountEntity {
    /**
     * id
     */
    id: string;
    /**
     * username
     */
    username: string;
    /**
     * hashedPassword
     */
    hashedPassword: string;
    /**
     * accountLevel
     */
    accountLevel: accountLevelType;
    /**
     * created
     */
    created: any;
    /**
     * lastLogin
     */
    lastLogin: any;
    /**
     * isActive
     */
    isActive: boolean;
    /**
     * characters
     */
    characters: IUOAccountCharacterEntity[];
}

/**
 * Generated interface for Moongate.Core.Network.Servers.Tcp.MoongateTcpClient
 */
interface IMoongateTcpClient {
    /**
     * serverId
     */
    serverId: string;
    /**
     * id
     */
    id: string;
    /**
     * isConnected
     */
    isConnected: boolean;
    /**
     * ip
     */
    ip: string;
    /**
     * haveCompression
     */
    haveCompression: boolean;
    /**
     * availableBytes
     */
    availableBytes: number;
    /**
     * isReceiveBufferFull
     */
    isReceiveBufferFull: boolean;
}

/**
 * Generated interface for Moongate.UO.Data.Ids.Serial
 */
interface ISerial {
    /**
     * value
     */
    value: any;
    /**
     * isMobile
     */
    isMobile: boolean;
    /**
     * isItem
     */
    isItem: boolean;
    /**
     * isValid
     */
    isValid: boolean;
}

/**
 * Generated interface for Moongate.UO.Data.Races.Base.Race
 */
interface IRace {
    /**
     * maleBody
     */
    maleBody: number;
    /**
     * maleGhostBody
     */
    maleGhostBody: number;
    /**
     * femaleBody
     */
    femaleBody: number;
    /**
     * femaleGhostBody
     */
    femaleGhostBody: number;
    /**
     * raceId
     */
    raceId: number;
    /**
     * raceIndex
     */
    raceIndex: number;
    /**
     * raceFlag
     */
    raceFlag: number;
    /**
     * name
     */
    name: string;
    /**
     * pluralName
     */
    pluralName: string;
}

/**
 * Generated interface for Moongate.UO.Data.Bodies.Body
 */
interface IBody {
    /**
     * type
     */
    type: bodyType;
    /**
     * isHuman
     */
    isHuman: boolean;
    /**
     * isMale
     */
    isMale: boolean;
    /**
     * isFemale
     */
    isFemale: boolean;
    /**
     * isGhost
     */
    isGhost: boolean;
    /**
     * isMonster
     */
    isMonster: boolean;
    /**
     * isAnimal
     */
    isAnimal: boolean;
    /**
     * isEmpty
     */
    isEmpty: boolean;
    /**
     * isSea
     */
    isSea: boolean;
    /**
     * isEquipment
     */
    isEquipment: boolean;
    /**
     * isGargoyle
     */
    isGargoyle: boolean;
    /**
     * bodyId
     */
    bodyId: number;
}

/**
 * Generated interface for Moongate.UO.Data.Professions.ProfessionInfo
 */
interface IProfessionInfo {
    /**
     * id
     */
    id: number;
    /**
     * name
     */
    name: string;
    /**
     * nameId
     */
    nameId: number;
    /**
     * descId
     */
    descId: number;
    /**
     * topLevel
     */
    topLevel: boolean;
    /**
     * gumpId
     */
    gumpId: number;
    /**
     * skills
     */
    skills: any[];
    /**
     * stats
     */
    stats: any[];
}

/**
 * Generated interface for Moongate.UO.Data.Geometry.Point3D
 */
interface IPoint3D {
    /**
     * x
     */
    x: number;
    /**
     * y
     */
    y: number;
    /**
     * z
     */
    z: number;
}

/**
 * Generated interface for Moongate.UO.Data.Maps.Map
 */
interface IMap {
    /**
     * index
     */
    index: number;
    /**
     * mapId
     */
    mapId: number;
    /**
     * fileIndex
     */
    fileIndex: number;
    /**
     * width
     */
    width: number;
    /**
     * height
     */
    height: number;
    /**
     * season
     */
    season: seasonType;
    /**
     * name
     */
    name: string;
    /**
     * rules
     */
    rules: mapRules;
    /**
     * tiles
     */
    tiles: ITileMatrix;
}

/**
 * Generated interface for Moongate.UO.Data.Persistence.Entities.ItemReference
 */
interface IItemReference {
    /**
     * id
     */
    id: ISerial;
    /**
     * itemId
     */
    itemId: number;
    /**
     * hue
     */
    hue: number;
}

/**
 * Generated interface for Moongate.UO.Data.Skills.SkillEntry
 */
interface ISkillEntry {
    /**
     * value
     */
    value: number;
    /**
     * skill
     */
    skill: ISkillInfo;
    /**
     * base
     */
    base: number;
    /**
     * cap
     */
    cap: number;
    /**
     * lock
     */
    lock: skillLock;
}

/**
 * Generated interface for Moongate.UO.Data.Persistence.Entities.UOAccountCharacterEntity
 */
interface IUOAccountCharacterEntity {
    /**
     * slot
     */
    slot: number;
    /**
     * name
     */
    name: string;
    /**
     * mobileId
     */
    mobileId: ISerial;
}

/**
 * Generated interface for Moongate.UO.Data.Tiles.TileMatrix
 */
interface ITileMatrix {
    /**
     * patch
     */
    patch: ITileMatrixPatch;
    /**
     * blockWidth
     */
    blockWidth: number;
    /**
     * blockHeight
     */
    blockHeight: number;
    /**
     * mapStream
     */
    mapStream: any;
    /**
     * indexStream
     */
    indexStream: any;
    /**
     * dataStream
     */
    dataStream: any;
    /**
     * indexReader
     */
    indexReader: any;
    /**
     * emptyStaticBlock
     */
    emptyStaticBlock: IStaticTile[][][];
}

/**
 * Generated interface for Moongate.UO.Data.Skills.SkillInfo
 */
interface ISkillInfo {
    /**
     * skillId
     */
    skillId: number;
    /**
     * name
     */
    name: string;
    /**
     * title
     */
    title: string;
    /**
     * strScale
     */
    strScale: number;
    /**
     * dexScale
     */
    dexScale: number;
    /**
     * intScale
     */
    intScale: number;
    /**
     * statTotal
     */
    statTotal: number;
    /**
     * strGain
     */
    strGain: number;
    /**
     * dexGain
     */
    dexGain: number;
    /**
     * intGain
     */
    intGain: number;
    /**
     * gainFactor
     */
    gainFactor: number;
    /**
     * professionSkillName
     */
    professionSkillName: string;
    /**
     * primaryStat
     */
    primaryStat: stat;
    /**
     * secondaryStat
     */
    secondaryStat: stat;
}

/**
 * Generated interface for Moongate.UO.Data.Tiles.TileMatrixPatch
 */
interface ITileMatrixPatch {
    /**
     * landBlocks
     */
    landBlocks: number;
    /**
     * staticBlocks
     */
    staticBlocks: number;
}

/**
 * Generated interface for Moongate.UO.Data.Tiles.StaticTile
 */
interface IStaticTile {
    /**
     * id
     */
    id: number;
    /**
     * x
     */
    x: number;
    /**
     * y
     */
    y: number;
    /**
     * z
     */
    z: number;
    /**
     * hue
     */
    hue: number;
    /**
     * height
     */
    height: number;
}
