import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** The class merger every shadcn primitive expects: later Tailwind classes win over earlier ones. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
