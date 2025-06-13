/**
 * Moongate Server v0.0.66.0 JavaScript API TypeScript Definitions
 * Auto-generated documentation on 2025-06-13 08:57:56
 **/

// Constants

/**
 * VERSION constant 
 * ""0.0.66.0""
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
};


