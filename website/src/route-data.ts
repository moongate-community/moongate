import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

export const onRequest = defineRouteMiddleware(({ locals, site, url }) => {
  // GitHub Pages serves this document as 404.html, not the virtual /404/ route.
  if (url.pathname.replace(/\/$/, '') !== '/moongate/404') return;
  const href = new URL('/moongate/404.html', site ?? url).href;
  for (const element of locals.starlightRoute.head) {
    if (element.tag === 'link' && element.attrs?.rel === 'canonical') element.attrs.href = href;
    if (element.tag === 'meta' && element.attrs?.property === 'og:url') element.attrs.content = href;
  }
});
