# Final whole-branch review fixes

Date: 2026-07-14

## Findings addressed

1. Set the VitePress project-site base to `/moongate/`. Updated the raw home SFC image and links to use VitePress `withBase`; VitePress-managed logo, navigation, sidebar, pager, and generated page links were verified in generated HTML with the same prefix.
2. Added prominent danger callouts to First launch and Operations. The wording is based on `MoongatePersistencePlugin.cs`: its persistence seeder upserts an active administrator with username `admin` and password hash derived from `admin`, logs both plaintext credentials at warning level, and saves a snapshot. Repository searches found no implemented or documented credential-change/disable procedure, and the callouts say so explicitly.
3. Added `--mg-periwinkle-300: #9da2ef` for normal foreground text while retaining canonical `--mg-periwinkle-400: #7377c8` for structural borders/shadows. The lighter token is used by the secondary eyebrow, contributor links, and breadcrumb links.
4. Added `DocBreadcrumbs.vue` through DefaultTheme Layout's `doc-before` slot. It renders `nav aria-label="Breadcrumb"`, a base-aware Home link, English section labels and page-data titles, intermediate links, and `aria-current="page"` current text. The homepage has no breadcrumb. Styles wrap responsively and provide an explicit 3px `:focus-visible` outline.

## Verification evidence

### Build

Command: `npm run docs:build`

Result: exit 0; VitePress 1.6.4 built client/server bundles and rendered all pages successfully in 2.62s on the final component revision.

### Project-base deployment smoke test

Command: `npm run docs:preview -- --host 127.0.0.1 --port 4173`, followed by `wget --server-response --spider` for the routes/assets below.

Result: HTTP 200 for:

- `/moongate/`
- `/moongate/images/moongate-logo.png`
- `/moongate/server/`
- `/moongate/contributors/`
- `/moongate/architecture/`
- `/moongate/assets/chunks/@localSearchIndexroot.CDXKUVwC.js`
- `/moongate/hashmap.json`

Generated HTML checks confirmed home image/operator/contributor/architecture URLs start with `/moongate/`; managed nav, logo, sidebar, and pager URLs do too. No `/moongate/moongate/` duplicate prefix remained.

### Breadcrumb semantics and keyboard/focus inspection

Generated HTML was inspected for the server index, first-launch, contributors index, and architecture index. Each contains exactly one `nav aria-label="Breadcrumb"` and one `aria-current="page"`; intermediate crumbs are native anchors with `/moongate/`-prefixed hrefs. The homepage contains zero breadcrumb navs. Native anchors remain in sequential keyboard focus order, and the built CSS contains `.mg-breadcrumbs a:focus-visible` with a 3px high-contrast outline and 3px offset.

### Contrast calculation

Command: a Node script implementing WCAG relative luminance (sRGB linearization, coefficients 0.2126/0.7152/0.0722) and `(Llighter + 0.05) / (Ldarker + 0.05)`.

Result: `contrast #9da2ef on #191823 = 7.40:1`, above WCAG AA's 4.5:1 requirement for normal text.

### Source/security inspection

Commands:

- `rg -n "admin|PasswordHash|Default account|Username:" src/Moongate.Persistence/MoongatePersistencePlugin.cs`
- `rg -n "change.*password|password.*change|disable.*account|account.*disable" src docs tests`

Result: source lines identify the exact predictable administrator creation and warning logs; the procedure search returned no matches. No targeted .NET test was necessary because the documentation claim directly describes the inspected plugin registration and literal log templates.

### Repository hygiene

Command: `git diff --check`

Result: exit 0 with no whitespace errors.

## Concerns

The server still has the insecure bootstrap behavior described by the new danger callouts. This documentation wave does not change server authentication behavior and does not invent an unsupported remediation procedure.
