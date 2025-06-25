/**
 * Moongate Server v0.3.26.0 JavaScript API TypeScript Definitions
 * Auto-generated documentation on 2025-06-25 16:40:21
 **/

// Constants

/**
 * VERSION constant 
 * ""0.3.26.0""
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
 * Generated enum for Moongate.Core.Server.Types.AccountLevelType
 */
export enum accountLevelType {
    USER = 0,
    GM = 1,
    ADMIN = 2,
}

/**
 * Generated enum for Moongate.Core.Server.Types.CommandSourceType
 */
export enum commandSourceType {
    NONE = 0,
    CONSOLE = 1,
    IN_GAME = 2,
    ALL = 3,
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
     * command
     */
    command: string;
    /**
     * arguments
     */
    arguments: string[];
}
