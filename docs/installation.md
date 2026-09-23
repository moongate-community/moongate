# Install on Linux

This page installs the released server binary. To run the published container instead, use
[Run with Docker](docker.md); to build from source, use [First start](getting-started.md).

## Install

```sh
curl -fsSL https://moongate.sh/install.sh | sh
```

The script resolves the latest release, downloads the archive for this machine, checks it
against the checksum published beside it, and puts it in place:

| Path | Contents |
| --- | --- |
| `/opt/moongate/` | The archive's contents: the server binary, the core SQL in `migrations/`, the migration runner in `migration-runner/`, `mgboot` (releases after 0.6.0), `LICENSE`, `THIRD-PARTY-NOTICES.md` and the debug symbols |
| `/usr/local/bin/moongate` | A symlink to `/opt/moongate/Moongate.Server` |
| `/usr/local/bin/mgboot` | A symlink to `/opt/moongate/mgboot`, when the release contains it |

Both locations need root. Run the line as root, or leave it to `sudo`, which the script
uses itself when it is not running as root. Nothing else is created: no service, no system user
and no data directory, because you choose where the server root lives at the first start.

Releases ship `linux-x64` and `linux-arm64`. Both binaries are self-contained, so the machine
needs no .NET runtime. Systems using musl, Alpine among them, are refused: the binary is built
against glibc. On macOS and Windows, take the archive for your platform from the
[releases page](https://github.com/moongate-community/moongate/releases), or use Docker.

## Next: first start

Never run the server inside `/opt/moongate`. Upgrading replaces that whole directory,
so configuration, plugins and generated files kept there are lost the next time the
install line runs; as an ordinary user the attempt fails anyway with
`Access to the path '/opt/moongate/moongate.pid.lock' is denied`. Give the server a
root of its own and prepare it:

```sh
sudo mkdir -p /srv/moongate && sudo chown "$USER" /srv/moongate
mgboot /srv/moongate
```

`mgboot` ships in releases after 0.6.0; on 0.6.0 the first-start guide shows the
equivalent manual steps. Then follow [Start a Moongate server](getting-started.md#first-start): edit the
generated `config/moongate.toml`, create the two PostgreSQL databases, apply the
core migrations with `/opt/moongate/migration-runner/Moongate.MigrationRunner`,
and start with `moongate --root-directory /srv/moongate`.

## Upgrade

Run the same line again. The new release is staged beside the current one and swapped in with a
rename, so a failed download or a bad checksum leaves the running installation untouched. Stop
the server first: replacing the binary under a live process is not supported.

The upgrade replaces `/opt/moongate` entirely and deletes the copy it moved aside. Nothing you
want to keep belongs in there, which is why the server root goes somewhere else.

## Remove

```sh
sudo rm -rf /opt/moongate /usr/local/bin/moongate /usr/local/bin/mgboot
```

Your server root is untouched by both the installer and this line.

## Options

The script reads five environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `MOONGATE_VERSION` | the latest release | Install a specific version, such as `0.6.0`; a leading `v` is accepted |
| `MOONGATE_RID` | detected from `uname -m` | `linux-x64` or `linux-arm64` |
| `MOONGATE_BASE_URL` | the GitHub release downloads | A mirror holding the same file names |
| `MOONGATE_INSTALL_DIR` | `/opt/moongate` | Where the archive's contents go |
| `MOONGATE_BIN_DIR` | `/usr/local/bin` | Where the `moongate` symlink goes |

Pointing the last two at paths you own installs without root:

```sh
curl -fsSL https://moongate.sh/install.sh |
  MOONGATE_INSTALL_DIR="$HOME/.local/lib/moongate" MOONGATE_BIN_DIR="$HOME/.local/bin" sh
```

## Read it before you run it

Piping a script into a shell runs whatever that URL serves. The file is
[`scripts/install.sh`](../scripts/install.sh) in this repository, and the site serves it
verbatim, so you can read the copy you are about to run:

```sh
curl -fsSL https://moongate.sh/install.sh | less
```

To skip the script entirely, download the archive and its checksum from the releases page and
check it yourself:

```sh
sha256sum -c moongate-linux-x64-0.6.0.tar.gz.sha256
tar -xzf moongate-linux-x64-0.6.0.tar.gz
```

## When it refuses

Every refusal prints one line starting with `moongate:`. Up to `could not install into`,
nothing was installed; the symlink messages below it come after the files are already in place.

| Message | Meaning |
| --- | --- |
| `this installer supports Linux only` | Use Docker or the archive for your platform |
| `unsupported architecture '...'` | The releases ship `linux-x64` and `linux-arm64` |
| `musl libc is not supported; the release binary needs glibc` | Alpine and other musl systems need the container image |
| `curl or wget is required`, `tar is required`, `sha256sum or shasum is required` | Install the named tool |
| `root privileges are required` | Re-run with `sudo`, or set `MOONGATE_INSTALL_DIR` and `MOONGATE_BIN_DIR` |
| `could not resolve the latest release; set MOONGATE_VERSION` | The releases page did not redirect to a version tag; pin one with `MOONGATE_VERSION` |
| `release v... has no asset for ...`, `release v... has no checksum for ...` | That version has no archive, or no checksum file, for this architecture, or the download itself failed; `linux-arm64` exists from 0.4.1 onwards |
| `checksum mismatch for ...` | The download does not match the published checksum; nothing was installed |
| `the archive could not be extracted`, `the archive does not contain moongate-.../Moongate.Server` | The downloaded archive is damaged or has an unexpected layout |
| `could not clear a leftover staging directory beside ...`, `could not create ...`, `could not stage the new files in ...`, `could not make ... executable`, `could not move the current installation aside; ... is untouched`, `could not install into ...` | The filesystem refused a step of the installation, for instance a full disk. Nothing new is installed, an upgrade keeps the previous installation, and the message says where it is |
| `could not replace .../moongate`, `could not link .../moongate` | The new files are in place under `/opt/moongate`, but the `moongate` symlink could not be replaced. Fix the bin directory and run the line again, or make the link yourself with `sudo ln -sfn /opt/moongate/Moongate.Server /usr/local/bin/moongate` |
| `could not replace .../mgboot`, `could not link .../mgboot`, `could not remove obsolete .../mgboot link` | The server files are in place; only the `mgboot` symlink could not be updated or removed. Fix the bin directory and run the line again, or link it yourself with `sudo ln -sfn /opt/moongate/mgboot /usr/local/bin/mgboot` |
