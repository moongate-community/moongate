<script setup lang="ts">
import { withBase } from 'vitepress'

const operatorLinks = [
  { label: 'Install', href: '/server/installation' },
  { label: 'Configure', href: '/server/configuration' },
  { label: 'Operate', href: '/server/operations' },
  { label: 'Troubleshoot', href: '/server/troubleshooting' }
]

function openSearch() {
  const button = document.querySelector<HTMLElement>(
    '.VPNavBarSearch button, .DocSearch-Button, .VPNavBarSearchButton'
  )

  if (button) {
    button.click()
    return
  }

  window.dispatchEvent(new KeyboardEvent('keydown', { key: '/', bubbles: true }))
}
</script>

<template>
  <main class="split-gate-home">
    <header class="split-gate-home__hero">
      <img :src="withBase('/images/moongate-logo.png')" alt="Moongate pixel-art portal" width="220" height="220">
      <p class="mg-eyebrow">Ultima Online server emulator</p>
      <h1>Moongate</h1>
      <p>Choose your path — operate a shard or help build it.</p>
      <button type="button" class="split-gate-home__search mg-pixel-frame" @click="openSearch">
        <svg class="split-gate-home__search-icon" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="square" aria-hidden="true">
          <circle cx="11" cy="11" r="7" />
          <line x1="16" y1="16" x2="21" y2="21" />
        </svg>
        <span>Search the docs</span>
        <kbd>/</kbd>
      </button>
    </header>

    <section class="split-gate" aria-label="Documentation paths">
      <article class="split-gate__operators mg-pixel-frame">
        <p class="mg-eyebrow">Primary path</p>
        <h2>Server Operators</h2>
        <p>Install, configure, run, and troubleshoot your shard.</p>
        <nav aria-label="Server operator topics">
          <a v-for="link in operatorLinks" :key="link.href" :href="withBase(link.href)">{{ link.label }}</a>
        </nav>
        <a class="mg-pixel-button" :href="withBase('/server/')">Open the server guide</a>
      </article>

      <article class="split-gate__contributors mg-pixel-frame">
        <p class="mg-eyebrow">Secondary path</p>
        <h2>Contributors</h2>
        <p>Build, test, contribute, and understand Moongate's architecture.</p>
        <a :href="withBase('/contributors/')">Contributor guide</a>
        <a :href="withBase('/architecture/')">Architecture</a>
      </article>
    </section>
  </main>
</template>
