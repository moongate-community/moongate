import DefaultTheme from 'vitepress/theme-without-fonts'
import type { Theme } from 'vitepress'
import { h } from 'vue'
import SplitGateHome from './components/SplitGateHome.vue'
import DocBreadcrumbs from './components/DocBreadcrumbs.vue'
import './styles/tokens.css'
import './styles/base.css'
import './styles/components.css'

export default {
  extends: DefaultTheme,
  Layout: () => h(DefaultTheme.Layout, null, {
    'doc-before': () => h(DocBreadcrumbs)
  }),
  enhanceApp({ app }) {
    app.component('SplitGateHome', SplitGateHome)
  }
} satisfies Theme
