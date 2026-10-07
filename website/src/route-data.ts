import { defineRouteMiddleware } from '@astrojs/starlight/route-data';
import { contentEntries } from '../content-manifest.mjs';
import { isEnglishOnlyPath } from '../scripts/translations.mjs';
import { docsBasePath } from '../site-config.mjs';

export const onRequest = defineRouteMiddleware(({ locals, site, url }) => {
  const route = locals.starlightRoute;
  const rewrite = (items: typeof route.sidebar) => {
    for (const item of items) {
      if (item.type === 'group') rewrite(item.entries);
      else if (isEnglishOnlyPath(item.href, contentEntries)) item.href = item.href.replace(/^\/it\//, '/');
    }
  };
  rewrite(route.sidebar);
  for (const link of [route.pagination.prev, route.pagination.next]) {
    if (link && isEnglishOnlyPath(link.href, contentEntries)) link.href = link.href.replace(/^\/it\//, '/');
  }
  // Custom routes and intentionally English references have no translated equivalent.
  if (isEnglishOnlyPath(url.pathname, contentEntries) || url.pathname.replace(/\/$/, '') === `${docsBasePath}404`) {
    route.head = route.head.filter(element => !(element.tag === 'link' && element.attrs?.rel === 'alternate'));
  }
  if (route.locale === 'it' && isEnglishOnlyPath(url.pathname, contentEntries)) {
    const original = new URL(url.pathname.replace(/^\/it\//, '/'), site ?? url).href;
    for (const element of route.head) {
      if (element.tag === 'link' && element.attrs?.rel === 'canonical') element.attrs.href = original;
      if (element.tag === 'meta' && element.attrs?.property === 'og:url') element.attrs.content = original;
    }
    route.head.push({ tag: 'meta', attrs: { name: 'robots', content: 'noindex' } });
  }
  // GitHub Pages serves this document as 404.html, not the virtual /404/ route.
  if (url.pathname.replace(/\/$/, '') !== `${docsBasePath}404`) return;
  const href = new URL(`${docsBasePath}404.html`, site ?? url).href;
  for (const element of locals.starlightRoute.head) {
    if (element.tag === 'link' && element.attrs?.rel === 'canonical') element.attrs.href = href;
    if (element.tag === 'meta' && element.attrs?.property === 'og:url') element.attrs.content = href;
  }
});
