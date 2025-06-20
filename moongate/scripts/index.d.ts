/**
 * Moongate Server v0.2.9.0 JavaScript API TypeScript Definitions
 * Auto-generated documentation on 2025-06-20 16:36:19
 **/

// Constants

/**
 * VERSION constant 
 * ""0.2.9.0""
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


