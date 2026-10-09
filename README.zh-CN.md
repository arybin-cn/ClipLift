# ClipLift

[English](README.md) | 中文

Windows 托盘小工具：把剪贴板里的图片通过 SSH 传到服务器，再把剪贴板换成远程文件路径。

适合配合远程机器上的 Claude Code 这类工具使用。SSH 终端里贴不了图片，但可以贴路径。截图，点一下托盘图标，Ctrl+V 就行。

## 使用

- 左键托盘图标：用上次的配置上传。
- 右键：选择配置上传，或打开设置。

在设置里勾选自动上传后，剪贴板里一出现图片（截图、从浏览器复制的图片）就会立即传到上次用的配置，不用再点图标。开启时托盘图标变成紫色。在资源管理器里复制的图片文件仍然需要点击上传。

可以建多个配置，对应不同的服务器或目录。路径和原图会同时留在剪贴板里，贴到终端以外的地方还是图片。在资源管理器里复制的图片文件也能传，上传时和截图一样改名为 `CLIPLIFT_时间戳`。

## 运行要求

- Windows 10/11（自带 .NET Framework 4.8）
- 系统自带的 OpenSSH 客户端
- 服务器配好密钥登录，`scp` 不会交互式地问密码

## 配置

从 [Releases](https://github.com/arybin-cn/ClipLift/releases) 下载 zip，解压到任意目录，运行 `ClipLift.exe` 即可，首次启动会打开设置窗口。

| 字段 | |
|---|---|
| Host | `~/.ssh/config` 里的别名，或 `user@host` |
| Port | 保持 `22` 就按 `~/.ssh/config` 来；填其他值会覆盖 |
| Remote directory | 上传目录，如 `/home/me/Images` |
| Paste path prefix | 可选。远程工具看到的路径和上传路径不一样时用，比如在容器里 |
| Keep last | 远程目录里只保留最近 N 个上传的文件（`CLIPLIFT_*`），每次上传后删掉更旧的。默认 10，`0` 表示全部保留 |

设置保存在 `%APPDATA%\ClipLift\settings.xml`。

### 例子

Claude Code 跑在 `devsrv` 的容器里，容器把 `/home/me/Projects` 挂载到 `/workspace`：

- Host：`devsrv`
- Remote directory：`/home/me/Projects/Images`
- Paste path prefix：`/workspace/Images`

上传后在终端里 Ctrl+V，得到类似 `/workspace/Images/CLIPLIFT_20260101_120000_000.png` 的路径。

## 常见问题

- `scp exited with code 255`：确认 `ssh <host>` 能直接登录，不问密码，也没有主机密钥确认。
- `Connection closed by UNKNOWN port -1`：多半是主机走了 `ProxyJump`/`ProxyCommand`，被 Port 字段覆盖了。把 Port 改回 `22`。

## 编译

```bash
dotnet build -c Release -o dist
```

Linux 和 macOS 上也能编（缺 ICU 的话设置 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`）。`ClipLift.ico` 用 `dotnet run --project tools/IconGen -- ClipLift.ico` 生成。
