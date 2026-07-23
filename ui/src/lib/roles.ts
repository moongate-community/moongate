// The single definition of who counts as staff, matching the server's AdminPolicy (Administrator or
// GrandMaster). The level is the role name exactly as the token carries it, so the match is exact.
const ADMIN_LEVELS = ['Administrator', 'GrandMaster']

export function isAdmin(level: string | null): boolean {
  return level !== null && ADMIN_LEVELS.includes(level)
}
