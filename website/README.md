# Moongate documentation website

Astro Starlight imports the repository's guides and package READMEs into a
searchable GitHub Pages site. Use Node 24.21.0 and run from the repository root:

```sh
npm --prefix website ci
npm --prefix website run dev
npm --prefix website test
npm --prefix website run build
npm --prefix website run preview -- --host 127.0.0.1 --port 4321
```

Open `/` on the local server. Search requires a production build.
After editing a source outside `website/`, rerun `npm --prefix website run prepare:docs`.

See [Writing documentation](../docs/documentation.md) for source ownership,
adding pages, link validation, and release-only publication.
