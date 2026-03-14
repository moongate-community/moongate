export function formatSerialHex(value: string | number): string {
  const numericValue = typeof value === 'number' ? value : Number.parseInt(value, 10)

  if (!Number.isFinite(numericValue)) {
    return String(value)
  }

  const normalized = Math.max(0, Math.trunc(numericValue)) >>> 0
  return `0x${normalized.toString(16).toUpperCase().padStart(8, '0')}`
}
