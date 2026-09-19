# Writing documentation

Moongate uses [Astro Starlight](https://starlight.astro.build/) for its English
documentation. The public site is hosted at
[moongate-community.github.io/moongate](https://moongate-community.github.io/moongate/).
Its first publication happens with the first release containing the website.

## Run locally

Use Node **24.21.0** (also recorded in `website/.nvmrc`). From the repository root:

```sh
npm --prefix website ci
npm --prefix website run dev
```

Open the `/moongate/` URL printed by Astro. Development search is unavailable;
use the production preview to check the search index.

```sh
npm --prefix website test
npm --prefix website run build
npm --prefix website run preview -- --host 127.0.0.1 --port 4321
```

Open `http://127.0.0.1:4321/moongate/`. The build imports the source documents,
builds the site and search index, and validates local links, images, and fragments.
External links are not fetched by the validator.

## Edit a page

Keep the source text in its existing location:

- `docs/*.md` contains server, reference, and contributor guides.
- `src/*/README.md` contains library documentation and NuGet examples.
- The root `README.md` supplies the overview.
- `website/src/content/docs/index.md` is the authored landing page.

Do not edit or commit `website/src/content/docs/generated/` or
`website/public/generated/`. The importer replaces these directories.
It preserves the original files, including NuGet smoke-test markers and code examples.

After editing an imported source while the dev server is running, run this in a
second terminal:

```sh
npm --prefix website run prepare:docs
```

Astro watches the generated pages; there is no separate watcher for source files
outside `website/`. The dev and production build commands also prepare pages first.

## Add a guide or library

1. Write the English Markdown source in `docs/` or the library's `README.md`.
2. Add an entry to `website/content-manifest.mjs` with its repository-relative
   source path, unique slug, title, and navigation group.
3. Run the tests and production build above.

Slugs use lowercase letters, digits, hyphens, and slash-separated segments.
Use relative links to other repository documents or source files, and preserve
heading fragments. Imported documents become site links, local images are copied,
and other repository files become GitHub links at the published release tag.
The first top-level title is removed from imported content because Starlight
renders the title from the manifest. Existing absolute GitHub links to imported
documents on `develop` or `main` are also converted.

Missing source files, duplicate slugs, unresolved local links, and broken links
in the built output fail the build. A failed import keeps the previous generated
content and the authored homepage intact.

## Release publication

Documentation builds and deploys **only when the existing release workflow
creates a release**. Commits to `develop`, ordinary `main` pushes, and pull
requests do not build or publish the website. There is no manual docs trigger.

The `docs` job in `.github/workflows/release.yml` calls the reusable
`.github/workflows/docs.yml`, passing the released SHA and tag. It checks out
that SHA and displays the tag in the site title. This direct call also works
when release-please creates the release using `GITHUB_TOKEN`.

The jobs run on GitHub-hosted Ubuntu runners. GitHub Pages must use **GitHub
Actions** as its source, and the `github-pages` environment must permit `main`.
Pages deployments are serialized. A failed deployment can be retried from its
release workflow run without publishing a new release.

Each release replaces the current website; historical versions are not hosted.
Local builds use `develop` for source links and `development` in the title. To
preview a release label and source ref, set `MOONGATE_DOCS_VERSION` and
`MOONGATE_DOCS_REF` before running the build.
