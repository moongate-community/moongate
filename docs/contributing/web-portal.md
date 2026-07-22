# The web portal

The portal is a React single-page app in `ui/` at the repository root. Vite builds it into its own
`ui/dist`, and the HTTP plugin *finds* that directory and serves it — the build never writes into a C#
project.

A checkout where nobody has run npm still builds and runs. `dotnet build` never needs Node, and a server
that finds no portal simply serves the API alone.

## Working on it

```bash
npm --prefix ui install
npm --prefix ui run dev      # http://localhost:5173, proxying /api to the server on 8933
```

Run the game server alongside it. To see the portal served by the real server instead of Vite:

```bash
npm --prefix ui run build
dotnet run --project src/Moongate.Server -- --root-directory ~/moongate --uo-directory ~/uo
```

`dotnet run` sets the working directory to the project folder, so the probe below will not find
`ui/dist` from there. Point at it explicitly:

```bash
MOONGATE_UI_DIST=$PWD/ui/dist dotnet run --project src/Moongate.Server -- --root-directory ~/moongate
```

### Where the server looks

In order, first hit wins:

1. `http.UiDistPath` in `moongate.yaml`
2. `$MOONGATE_UI_DIST`
3. `ui/dist` under the working directory
4. `ui/dist` beside the executable — this is the layout the container ships

A candidate counts only if it contains `index.html`. A directory that exists but holds no entry point
would otherwise pass the check and then serve nothing.

`ui/dist` is gitignored, and excluded from the Docker build context too: the image takes the portal from
its own node stage, so a stale local build can never end up shipped in place of the one just compiled.

### The SPA fallback

Any unmatched **GET** receives `index.html`, so the router owns client-side routes and a deep link
survives a refresh. Four prefixes are reserved and never fall back — `/api`, `/health`, `/swagger`,
`/scalar` — and neither does any non-GET. A REST caller asking for a route that does not exist has to
receive its JSON 404, not an HTML page with status 200.

Hashed assets are served `immutable` for a year. `index.html` is `no-cache`, because it carries the
pointers to them and a stale copy would keep naming the previous bundle's files.

## API types are generated, not written

`ui/src/lib/api-types.ts` comes from the server's own OpenAPI document. Both it and `ui/openapi.json` are
committed, and CI regenerates them and fails on any difference. After changing any endpoint or response
DTO:

```bash
npm --prefix ui run openapi:sync    # boots a throwaway server, saves /swagger/v1/swagger.json
npm --prefix ui run openapi:types   # regenerates the TypeScript
```

Commit both files with the change that caused them.

The sync script boots a real server on an isolated root and on ports 18933/12593 rather than the
defaults, so it cannot read the document of a server you already have running. It needs no UO client
files.

Two things about the generator are worth knowing before you touch it:

- It runs through `npx` with `typescript@5.9.3` and `openapi-typescript@7.13.0` pinned, not as a
  devDependency. It emits through the TypeScript compiler API, and this project's `typescript` is the
  native port, whose `lib/` ships `tsc.js` and no JavaScript API at all.
- A route only appears with a typed body if its registration declares one. Handlers return `IResult`,
  which tells the API explorer nothing, so **new endpoints need `.Produces<T>()`** or the portal gets a
  path with an untyped response and no compiler help.

## Styling

Colours, fonts, spacing and radii come from `ui/src/styles/tokens.css`, vendored from the Claude Design
project. Do not edit that file by hand — it is authored there and a sync overwrites it.

`globals.css` maps the tokens into Tailwind with `@theme inline`, which makes each utility *reference*
the custom property rather than copy its value. Switching `data-theme` on `<html>` therefore re-points
every colour at once, and every utility follows.

The theme is applied from `ui/src/lib/theme.ts`, called by the entry point before the first render — not
by the toggle, which lives in the app shell. The login screen has no shell, and leaving the job to the
toggle meant the first screen a visitor met ignored their remembered choice.

### shadcn components

Primitives under `ui/src/components/ui/` come from the shadcn registry. **Do not run `shadcn init`**: it
rewrites `globals.css` with its own variables and a class-based dark mode, which is a second theming
system competing with the one above. `components.json` is written by hand, and `npx shadcn add <name>`
is fully non-interactive and leaves the stylesheet alone.

The registry's vocabulary — `bg-primary`, `text-foreground`, `border-input` and the rest — is aliased
onto the Moongate tokens inside `@theme inline`, so a generated component arrives already themed. Two
consequences:

- `dark:` **is** used, by the registry's own components, and `@custom-variant dark` re-points it at
  `[data-theme="dark"]`. Tailwind's stock variant follows the operating system, which would disagree
  with the in-app toggle. Do not add `dark:` to Moongate-authored markup — the tokens already handle it —
  but do not strip it from generated components either.
- `muted` means muted *text* in Moongate and a muted *surface* in the registry. `bg-muted` would come
  out the colour of secondary text; use `bg-raised`.

Tailwind 4 defaults `border-color` to `currentColor`, and registry components write a bare `border`
trusting a base rule to pin it. `globals.css` carries that rule; without it every card draws a hairline
in its own text colour.

## Text

No user-facing string literal may appear outside `ui/src/locales/`. English is the default and Italian
is available; both bundles must define exactly the same keys, and a test enforces it. A key added to one
file and forgotten in the other fails nothing at runtime — i18next falls back — so the gap would surface
only to whoever reads that screen in that language.

Tests pin the language explicitly in `vitest.setup.ts` rather than inheriting the app default, so
changing that default does not rewrite the assertions.

## Sessions

Login returns a token valid for `http.Jwt.LifetimeMinutes` (60 by default). The portal renews it two
minutes before expiry through `POST /api/v1/auth/renew`, so an active session does not expire mid-use.
Coming back into focus is a second chance: timers in a backgrounded tab are throttled, so a session left
alone can return past its renewal point.

Renewal keeps the `auth_time` of the original login, so a session cannot be extended forever: past
`http.Jwt.MaxSessionHours` (12 by default) the renewal is refused and the user signs in again. The
endpoint also re-reads the account, so deactivating one stops its renewals and a level change takes
effect on the next renewal.

"Remember me" decides where the token lives: `localStorage` when ticked, so the session survives the
browser closing, `sessionStorage` when not, so it dies with the tab. Renewals write back to whichever
store already holds it.
