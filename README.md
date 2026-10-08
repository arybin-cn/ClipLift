# ClipLift

[English](#english) | [中文](#中文)

---

## English

ClipLift is a small Windows tray app that uploads the image in your clipboard to an SSH server and puts the remote path into the clipboard, ready to paste.

It is made for terminal tools running on a remote machine, such as Claude Code over SSH, inside tmux, or in a container. A terminal can only send text, so pasting an image there does not work. With ClipLift you take a screenshot, click the tray icon, then paste the path, and the remote tool reads the image from disk.

### Features

- **Left-click** the tray icon: upload the clipboard image using the last used profile.
- **Right-click** the tray icon: a menu that lists every profile (click one to upload there), followed by **Settings...** and **Exit**.
- **Multiple profiles**, so you can use different servers, directories, or path prefixes.
- **Path prefix**: the pasted path can use a different directory than the upload directory. This is useful when the directory is mounted at another path inside a container.
- **Keeps your image**: by default the clipboard holds both the path (text) and the original image. Terminals paste the path; other apps still paste the image.
- Copied image files from Explorer (png, jpg, jpeg, gif, webp, bmp) are uploaded as well.
- Quiet feedback: a small balloon-style popup above the tray. It never takes focus and does not go to the notification center. The tray icon is **green** when idle and **blue** while uploading.
- Nothing happens when the clipboard has no image.

### Requirements

- Windows 10 / 11. The app uses .NET Framework 4.8, which is built into Windows, so nothing needs to be installed.
- The Windows OpenSSH client (`ssh.exe` and `scp.exe`), which is installed by default on Windows 10 1809 and later.
- **Key-based SSH login** to the server. ClipLift runs `scp` in batch mode, so it cannot ask for a password.

### Installation

1. Put `ClipLift.exe` and `ClipLift.exe.config` in the same folder, for example `C:\Tools\ClipLift\`.
2. Run `ClipLift.exe`. The settings window opens on first start.
3. Optional: tick **Start ClipLift with Windows** in the settings.

`ClipLift.exe.config` enables per-monitor DPI scaling. The app also runs without it, but windows will not rescale when moved between monitors with different DPI.

### Settings

| Field | Description |
|---|---|
| Name | Display name of the profile, shown in the tray menu. |
| Host | An alias from `~/.ssh/config`, or `user@host`. |
| Port | Keep `22` to use `~/.ssh/config` as is (including `Port`, `ProxyJump`, `ProxyCommand`). Any other value overrides it. |
| Remote directory | The upload directory on the server, for example `/home/me/Images`. |
| Paste path prefix | Optional. Replaces the remote directory in the pasted path, for example `/workspace/Images` when `/home/me` is mounted as `/workspace` in a container. |

**Test connection** connects to the server and creates the remote directory (`mkdir -p`).

Global options:

- **Keep the original image in the clipboard**: when off, the clipboard holds only the path.
- **Add a trailing space after the pasted path**: makes it easier to keep typing after pasting.
- **Start ClipLift with Windows**: registers ClipLift under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Settings are stored in `%APPDATA%\ClipLift\settings.xml`.

### Example: Claude Code inside a container on a remote server

Setup:

- The server user is `me`. The container mounts `/home/me/Projects` as `/workspace`.
- `~/.ssh/config` on Windows contains a `Host devsrv` entry, and key-based login works.

Profile:

| Field | Value |
|---|---|
| Name | `dev` |
| Host | `devsrv` |
| Port | `22` |
| Remote directory | `/home/me/Projects/Images` |
| Paste path prefix | `/workspace/Images` |

Workflow:

1. Take a screenshot to the clipboard, for example with Snipaste or Win+Shift+S.
2. Left-click the ClipLift tray icon.
3. In the terminal, press Ctrl+V. You get `/workspace/Images/shot_20260101_120000_000.png `.
4. Ask Claude Code about the image.

### Troubleshooting

- **`scp exited with code 255`**: run `ssh <host>` in a terminal. It must log in without asking for a password and must not show a host-key prompt.
- **`Connection closed by UNKNOWN port -1`**: the host is reached through `ProxyJump` or `ProxyCommand`, or it uses a non-default port, and Port is overriding that. Set Port back to `22` so that `~/.ssh/config` is used.
- **Nothing happens on click**: the clipboard has no image. Copy the screenshot first; some tools only save it to a file.

### Building from source

The project targets .NET Framework 4.8 and builds with the .NET SDK on Windows, Linux, or macOS:

```bash
dotnet build -c Release -o dist
```

On Linux without ICU, set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` first. The output is `dist/ClipLift.exe` together with `dist/ClipLift.exe.config`.

---

## 中文

ClipLift 是一个 Windows 托盘小工具：把剪贴板里的图片上传到 SSH 服务器，然后把远程路径写进剪贴板，直接就能粘贴。

它适合在远程机器上运行的终端工具，比如通过 SSH、在 tmux 或容器里运行的 Claude Code。终端只能传文字，所以图片粘贴不进去。用 ClipLift 的话，截图后点一下托盘图标，再粘贴路径，远程的工具就能从磁盘上读取这张图片。

### 功能

- **左键**点击托盘图标：用上次使用的配置上传剪贴板里的图片。
- **右键**点击托盘图标：弹出菜单，列出所有配置，点哪个就上传到哪个；菜单底部是 **Settings...**（配置）和 **Exit**（退出）。
- **支持多个配置**：可以对应不同的服务器、目录或路径前缀。
- **路径前缀**：粘贴出来的路径可以和上传目录不同。比如目录在容器里挂载到了另一个路径时，就可以用容器里的路径。
- **保留原图**：默认情况下，剪贴板里同时有路径文本和原来的图片。在终端里粘贴得到的是路径，在其他程序里粘贴的仍然是图片。
- 在资源管理器里复制的图片文件（png、jpg、jpeg、gif、webp、bmp）也可以上传。
- 提示方式很安静：托盘上方弹出一个经典气泡样式的小窗，不会抢焦点，也不会进通知中心。托盘图标平时是**绿色**，上传中是**蓝色**。
- 剪贴板里没有图片时，点击不会有任何反应。

### 运行要求

- Windows 10 / 11。程序使用 Windows 自带的 .NET Framework 4.8，不需要额外安装任何东西。
- Windows 的 OpenSSH 客户端（`ssh.exe` 和 `scp.exe`）。Windows 10 1809 及以后的版本默认已安装。
- 能用**密钥免密登录**服务器。ClipLift 以批处理模式运行 `scp`，没法弹出密码输入框。

### 安装

1. 把 `ClipLift.exe` 和 `ClipLift.exe.config` 放在同一个文件夹里，例如 `C:\Tools\ClipLift\`。
2. 运行 `ClipLift.exe`。第一次启动时会自动打开配置窗口。
3. 可选：在配置窗口里勾选 **Start ClipLift with Windows**，开机自动启动。

`ClipLift.exe.config` 用来开启按显示器的高 DPI 缩放。缺少这个文件程序也能运行，只是窗口在不同 DPI 的显示器之间移动时不会重新缩放。

### 配置说明

| 字段 | 说明 |
|---|---|
| Name | 配置的显示名称，会出现在托盘菜单里。 |
| Host | `~/.ssh/config` 里的主机别名，或者 `user@host`。 |
| Port | 保持 `22` 时完全按 `~/.ssh/config` 连接（包括其中的 `Port`、`ProxyJump`、`ProxyCommand`）；填其他端口则会覆盖配置文件里的端口。 |
| Remote directory | 服务器上的上传目录，例如 `/home/me/Images`。 |
| Paste path prefix | 可选。用来替换粘贴路径里的上传目录。例如容器把 `/home/me` 挂载成了 `/workspace` 时，可以填 `/workspace/Images`。 |

**Test connection** 会连接服务器，并创建上传目录（`mkdir -p`）。

全局选项：

- **Keep the original image in the clipboard**：保留原图。关闭后，剪贴板里只有路径。
- **Add a trailing space after the pasted path**：在路径后面加一个空格，粘贴后可以直接接着输入。
- **Start ClipLift with Windows**：开机自启，写入注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`。

配置保存在 `%APPDATA%\ClipLift\settings.xml`。

### 示例：在远程服务器的容器里运行 Claude Code

环境：

- 服务器用户是 `me`，容器把 `/home/me/Projects` 挂载成了 `/workspace`。
- Windows 上的 `~/.ssh/config` 里有一条 `Host devsrv`，并且能免密登录。

配置：

| 字段 | 值 |
|---|---|
| Name | `dev` |
| Host | `devsrv` |
| Port | `22` |
| Remote directory | `/home/me/Projects/Images` |
| Paste path prefix | `/workspace/Images` |

使用流程：

1. 截图并复制到剪贴板，例如用 Snipaste 或 Win+Shift+S。
2. 左键点击托盘里的 ClipLift 图标。
3. 在终端里按 Ctrl+V，粘贴出来的是 `/workspace/Images/shot_20260101_120000_000.png `。
4. 让 Claude Code 查看这张图片。

### 常见问题

- **`scp exited with code 255`**：在终端里执行 `ssh <host>`，确认不需要输入密码就能登录，并且不会弹出主机密钥确认。
- **`Connection closed by UNKNOWN port -1`**：这台主机是通过 `ProxyJump` 或 `ProxyCommand` 连接的，或者使用了非默认端口，而 Port 字段把它覆盖了。把 Port 改回 `22`，让 `~/.ssh/config` 生效。
- **点击没有反应**：剪贴板里没有图片。请先把截图复制到剪贴板；有些截图工具默认只保存成文件。

### 从源码编译

项目的目标框架是 .NET Framework 4.8，可以在 Windows、Linux 或 macOS 上用 .NET SDK 编译：

```bash
dotnet build -c Release -o dist
```

在没有 ICU 库的 Linux 上，先设置环境变量 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`。编译产物是 `dist/ClipLift.exe` 和 `dist/ClipLift.exe.config`。
