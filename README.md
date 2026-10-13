# airwar-together 开发环境

当前工作区包含 `airwar-together/` Godot 工程和 `参考项目/newjavahub/` Java 飞机大战参考代码。Godot 工程已接入 Godot-MCP；游戏玩法迁移与升级尚未开始。

Git 仓库根目录为 `D:\airwar-together`，本地分支为 `main`，远程 `origin` 为 [6NewUser6/Airwar-Together](https://github.com/6NewUser6/Airwar-Together)。

已配置的版本：

- Godot **4.7.2 .NET**：`D:\godot\Godot_v4.7.2-stable_mono_win64/`。
- [Godot-MCP 0.26.0](https://godotengine.org/asset-library/asset/5245)，资源库提交 `e25d807ff99406952f105b17d614acf6b072405b`。
- 插件指定的 GameDev-MCP-Server **9.2.10**，下载包已通过发布方 SHA256 校验，保存在 `.tools/mcp-server/9.2.10/`。
- NuGet：ReflectorNet **5.4.1**、McpPlugin **8.7.0**；项目已包含 Extensions 面板需要的嵌入资源。

## 启动

在本工作区打开 PowerShell，执行：

```powershell
.\scripts\Start-GodotMcp.ps1
```

脚本先编译 C# 工程，再启动本地 MCP 服务和 Godot 编辑器。服务只监听 `127.0.0.1:5300`。重复执行会复用脚本已启动的进程；端口被其他程序占用时会提示错误。请使用 **.NET 版** Godot 打开工程；游戏逻辑仍可使用 GDScript。

Codex 已添加服务器 `godot-airwar`，地址为 `http://127.0.0.1:5300/mcp`。首次配置后，在 Codex **设置 → MCP servers** 中执行 **Restart**，使客户端加载新增工具。Godot 编辑器需要保持打开。插件面板已选择 Codex，42 个对应工具技能会自动生成到项目的 `.agents/skills/`。

## 验证和停止

```powershell
python .\scripts\Verify-GodotMcp.py
.\scripts\Stop-GodotMcp.ps1
```

验证脚本通过真实 MCP HTTP 会话列出工具，并读取编辑器状态、项目文件树及打开的场景。已验证注册 **39 个 AI 工具**。插件文档中的另外 3 个系统工具通过单独的系统接口提供，因此总计 42 个。

验证报告位于 `.tools/run/mcp-verification.json`；编辑器和服务输出位于 `.tools/run/`。停止脚本会正常请求关闭可见编辑器；有未保存内容时请在 Godot 中处理保存提示，再停止服务。

用于自动验证的无窗口模式：

```powershell
.\scripts\Start-GodotMcp.ps1 -Headless
python .\scripts\Verify-GodotMcp.py
.\scripts\Stop-GodotMcp.ps1
```

## 配置与恢复

项目的本地连接设置保存在 `airwar-together/.env`，模板为 `.env.example`。启动脚本会在 `.env` 缺失时从模板恢复。Codex 配置保存在 `C:\Users\wang\.codex\config.toml`；加入 MCP 前的备份位于 `.tools/run/codex-config.before-godot.toml`。

`.tools/`、Godot 缓存和本地 `.env` 已排除在版本管理之外。需要在其他电脑复现时，安装相同 Godot .NET 版本及 .NET 8 或更新的 SDK，下载 GameDev-MCP-Server 的 `v9.2.10` Windows x64 发布包到上述目录，再通过 `-EditorPath` 指定该电脑的 Godot .NET 可执行文件。

移除 Codex 中的连接：

```powershell
codex mcp remove godot-airwar
```

配置方式依据 [OpenAI 官方 MCP 文档](https://developers.openai.com/codex/mcp/)。
