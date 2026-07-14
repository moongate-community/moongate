import DefaultTheme from 'vitepress/theme-without-fonts'
import type { Theme } from 'vitepress'
import SplitGateHome from './components/SplitGateHome.vue'
import './styles/tokens.css'
import './styles/base.css'
import './styles/components.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('SplitGateHome', SplitGateHome)
  }
} satisfies Theme
