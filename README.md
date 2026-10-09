# ClipLift

English | [中文](README.zh-CN.md)

A Windows tray app that uploads the clipboard image to a server over SSH and replaces the clipboard with the remote file path.

Handy for tools like Claude Code running on a remote machine: you can't paste an image into an SSH terminal, but you can paste a path. Take a screenshot, click the tray icon, Ctrl+V.

## Usage

- Left-click the tray icon to upload with the last used profile.
- Right-click to pick a profile, or to open Settings.

With auto upload turned on in Settings, every image that lands in the clipboard (a screenshot, a picture copied from a browser) is uploaded to the last used profile right away, no click needed. The tray icon turns purple while this is on. Image files copied in Explorer still need a click.

You can set up several profiles (different hosts or directories). The original image stays in the clipboard alongside the path, so pasting into a non-terminal app still gives you the image. Image files copied in Explorer work too; they are uploaded under a `CLIPLIFT_<timestamp>` name like screenshots.

## Requirements

- Windows 10/11 (.NET Framework 4.8 is already there)
- The built-in OpenSSH client
- Key-based SSH login to the server, since `scp` runs non-interactively

## Setup

Download the zip from [Releases](https://github.com/arybin-cn/ClipLift/releases), extract it anywhere and run `ClipLift.exe`. The settings window opens on first launch.

| Field | |
|---|---|
| Host | An alias from `~/.ssh/config`, or `user@host` |
| Port | Leave at `22` to use whatever `~/.ssh/config` says; anything else overrides it |
| Remote directory | Where files are uploaded, e.g. `/home/me/Images` |
| Paste path prefix | Optional. Use it when the path seen by the remote tool differs from the upload path, e.g. inside a container |
| Keep last | Only the newest N uploads (`CLIPLIFT_*` files) are kept in the remote directory; older ones are deleted after each upload. Default 10, `0` keeps everything |

Settings are saved to `%APPDATA%\ClipLift\settings.xml`.

### Example

Claude Code runs in a container on `devsrv`, which mounts `/home/me/Projects` at `/workspace`:

- Host: `devsrv`
- Remote directory: `/home/me/Projects/Images`
- Paste path prefix: `/workspace/Images`

After uploading, Ctrl+V in the terminal gives something like `/workspace/Images/CLIPLIFT_20260101_120000_000.png`.

## Troubleshooting

- `scp exited with code 255`: check that `ssh <host>` logs in without a password or host-key prompt.
- `Connection closed by UNKNOWN port -1`: the host probably goes through `ProxyJump`/`ProxyCommand` and the Port field is overriding it. Set Port back to `22`.

## Building

```bash
dotnet build -c Release -o dist
```

Works on Linux and macOS too (set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` if ICU is missing). `ClipLift.ico` is generated with `dotnet run --project tools/IconGen -- ClipLift.ico`.
