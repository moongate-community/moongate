# Contributing

## Prepare a change

1. Create a focused branch in your fork or local clone.
2. Make the smallest coherent change and follow the [repository conventions](conventions.md).
3. Add or update focused tests when behavior changes, and update documentation when contributor or operator behavior changes.
4. Run the applicable checks from [Build and test](build-and-test.md).
5. Commit with a Conventional Commit message, for example `fix(network): handle incomplete packet frames` or `docs(contributors): clarify test workflow`.
6. Push the branch and open a pull request describing the change and the validation performed.

## Repository automation

The workflow currently present for documentation runs `npm ci` and `npm run docs:build` for documentation-related pull requests targeting `develop` or `main`. Documentation-related pushes to `main` also build the site and, after a successful build, deploy the generated artifact to GitHub Pages. The workflow can also be started manually.

No general .NET build workflow is present on this branch. Run the .NET Release build and tests locally and report the result in the pull request rather than assuming repository automation will run them.

The repository does not define contribution issue templates, required labels, review timing, or branch-protection rules in the files covered by this guide. Follow the instructions shown by GitHub when opening the contribution and keep the pull request description evidence-based.
