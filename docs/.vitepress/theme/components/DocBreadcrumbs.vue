<script setup lang="ts">
import { computed } from 'vue'
import { useData, useRoute, withBase } from 'vitepress'

const route = useRoute()
const { page, site } = useData()

const sectionLabels: Record<string, string> = {
  architecture: 'Architecture',
  contributors: 'Contributors',
  server: 'Server Guide'
}

const humanize = (segment: string) => segment
  .split('-')
  .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
  .join(' ')

const breadcrumbs = computed(() => {
  const relativePath = route.path.startsWith(site.value.base)
    ? route.path.slice(site.value.base.length)
    : route.path
  const segments = relativePath.replace(/^\/+|\/+$/g, '').split('/').filter(Boolean)

  return segments.map((segment, index) => {
    const isCurrent = index === segments.length - 1
    const href = `/${segments.slice(0, index + 1).join('/')}${isCurrent ? '' : '/'}`
    const fallback = sectionLabels[segment] ?? humanize(segment)

    return {
      href: withBase(href),
      isCurrent,
      label: isCurrent ? (page.value.title || fallback) : fallback
    }
  })
})
</script>

<template>
  <nav v-if="breadcrumbs.length" class="mg-breadcrumbs" aria-label="Breadcrumb">
    <ol>
      <li><a :href="withBase('/')">Home</a></li>
      <li v-for="crumb in breadcrumbs" :key="crumb.href">
        <span v-if="crumb.isCurrent" aria-current="page">{{ crumb.label }}</span>
        <a v-else :href="crumb.href">{{ crumb.label }}</a>
      </li>
    </ol>
  </nav>
</template>
