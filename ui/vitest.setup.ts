import '@testing-library/jest-dom/vitest'
import i18n from './src/lib/i18n'

// jsdom lacks the pointer-capture and scroll APIs the Radix primitives call (Select, Dialog, …). Stub
// them so those components can be exercised under Vitest; without these, opening a Radix Select throws.
if (!Element.prototype.hasPointerCapture) {
  Element.prototype.hasPointerCapture = () => false
  Element.prototype.setPointerCapture = () => {}
  Element.prototype.releasePointerCapture = () => {}
}
Element.prototype.scrollIntoView = () => {}

// jsdom has no ResizeObserver, which the Radix Switch measures its thumb with.
if (!('ResizeObserver' in globalThis)) {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}

// Pinned, not inherited. Assertions below match on rendered copy, so leaving the language to the app's
// default would make every one of them a test of what that default happens to be — and they would all
// break the day it changes, which is exactly what happened once already.
void i18n.changeLanguage('en')
