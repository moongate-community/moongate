import { useState, type ReactNode } from 'react'

/**
 * An image that gives way to something readable when it fails. A character whose body has no
 * animation legitimately 404s from the image route, and the browser's broken-image icon is not an
 * answer for a page whose whole point is the picture.
 *
 * The failure resets when `src` changes, so a reused row — a table paging to a different character
 * in the same position — is not stuck with the previous one's placeholder. Storing which src failed
 * rather than a boolean is what makes that reset free: no effect, no stale flag.
 */
export function CharacterImage({
  src,
  alt,
  className,
  fallback,
}: {
  src: string
  alt: string
  className?: string
  fallback: ReactNode
}) {
  const [failed, setFailed] = useState<string | null>(null)

  if (failed === src) {
    return <>{fallback}</>
  }

  return <img src={src} alt={alt} className={className} onError={() => setFailed(src)} />
}
