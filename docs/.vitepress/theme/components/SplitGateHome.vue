<script setup lang="ts">
const operatorLinks = [
  { label: 'Install', href: '/server/installation' },
  { label: 'Configure', href: '/server/configuration' },
  { label: 'Operate', href: '/server/operations' },
  { label: 'Troubleshoot', href: '/server/troubleshooting' }
]
</script>

<template>
  <main class="split-gate-home">
    <header class="split-gate-home__hero">
      <img src="/images/moongate-logo.png" alt="Moongate pixel-art portal" width="220" height="220">
      <p class="mg-eyebrow">Ultima Online server emulator</p>
      <h1>Choose your path.</h1>
      <p>Operate a Moongate server or help build it.</p>
    </header>

    <section class="split-gate" aria-label="Documentation paths">
      <article class="split-gate__operators mg-pixel-frame">
        <p class="mg-eyebrow">Primary path</p>
        <h2>Server Operators</h2>
        <p>Install, configure, run, and troubleshoot your shard.</p>
        <nav aria-label="Server operator topics">
          <a v-for="link in operatorLinks" :key="link.href" :href="link.href">{{ link.label }}</a>
        </nav>
        <a class="mg-pixel-button" href="/server/">Open the server guide</a>
      </article>

      <article class="split-gate__contributors mg-pixel-frame">
        <p class="mg-eyebrow">Secondary path</p>
        <h2>Contributors</h2>
        <p>Build, test, contribute, and understand Moongate's architecture.</p>
        <a href="/contributors/">Contributor guide</a>
        <a href="/architecture/">Architecture</a>
      </article>
    </section>
  </main>
</template>
