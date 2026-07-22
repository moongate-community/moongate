import '@testing-library/jest-dom/vitest'
import i18n from './src/lib/i18n'

// Pinned, not inherited. Assertions below match on rendered copy, so leaving the language to the app's
// default would make every one of them a test of what that default happens to be — and they would all
// break the day it changes, which is exactly what happened once already.
void i18n.changeLanguage('en')
