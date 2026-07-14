import DefaultTheme from 'vitepress/theme-without-fonts'
import type { Theme } from 'vitepress'
import './styles/tokens.css'
import './styles/base.css'
import './styles/components.css'

export default {
  extends: DefaultTheme
} satisfies Theme
