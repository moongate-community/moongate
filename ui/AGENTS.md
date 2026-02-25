# Repository Guidelines

## Project Structure & Module Organization
- Frontend code lives in `ui/`.
- Main app sources are in `ui/src/`:
  - `ui/src/api/` for HTTP client logic.
  - `ui/src/store/` for state management (`zustand`).
  - `ui/src/router.tsx` for route wiring.
  - `ui/src/index.css` for global styles.
- Static assets are in `ui/public/`.
- Build output is generated in `ui/dist/` (do not edit manually).

## Build, Test, and Development Commands
- `cd ui && npm install`
  - Installs frontend dependencies.
- `cd ui && npm run dev`
  - Starts Vite dev server with HMR.
- `cd ui && npm run build`
  - Runs TypeScript build (`tsc -b`) and production bundle.
- `cd ui && npm run preview`
  - Serves the production build locally.
- `cd ui && npm run lint`
  - Runs ESLint across the UI codebase.

## Coding Style & Naming Conventions
- Language: TypeScript (`.ts`, `.tsx`) with React 19.
- Indentation: 2 spaces; keep files ASCII unless existing file requires otherwise.
- Naming:
  - Components: `PascalCase` (example: `LoginPage.tsx`).
  - Hooks/utilities/store functions: `camelCase`.
  - Constants: `UPPER_SNAKE_CASE`.
- Keep modules small and domain-focused (`api`, `store`, `router`, UI components).
- Respect repository conventions in `CODE_CONVENTION.md` when touching shared patterns.

## Testing Guidelines
- Minimum gate for UI changes: `npm run lint` and `npm run build` must pass.
- Add tests for non-trivial logic (stores, parsers, helpers) when introducing behavior.
- Suggested naming: `FeatureName.test.ts` or `FeatureName.spec.ts` colocated with the feature or in a dedicated `__tests__` folder.

## Commit & Pull Request Guidelines
- Use Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`).
- Keep commits scoped to one concern.
- PRs should include:
  - Clear summary and motivation.
  - Validation steps and commands run.
  - UI screenshots/GIFs for visual changes.
  - Linked issue/task when applicable.

## Security & Configuration Tips
- Do not commit secrets or tokens in UI code.
- Keep API base URL and environment-specific values configurable (avoid hardcoded production endpoints).
