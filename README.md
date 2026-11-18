# Copy to MD

一个 Windows 托盘工具：从 PowerPoint 等富文本来源复制内容后，在目标编辑器里按“智能粘贴”快捷键，自动按目标应用输出合适格式。

它主要解决两类问题：

- PPT 项目符号复制到纯文本后退化成 `l`、`m`、`n` 等伪符号。
- Markdown、Word、记事本对同一份剪贴板内容需要不同格式，不能一概而论。

普通 `Ctrl+V` 不会被劫持。工具只在你按下智能粘贴快捷键时读取剪贴板、转换内容并模拟粘贴。

## 功能特性

- 自动识别前台目标应用，也支持托盘手动强制模式。
- Markdown 输出：标题、列表、嵌套列表、粗体、斜体、安全链接、LaTeX 上下标。
- Word 输出：重新生成受控 RTF，保留标题、列表、粗斜体和真实上下标，同时提供 UnicodeText。
- 纯文本输出：保守清理伪项目符号，尽量保留 Unicode 上下标。
- 支持 HTML、RTF、UnicodeText 剪贴板格式；富文本不可信时回退保守纯文本。
- 正确处理 CF_HTML 的 UTF-8 字节偏移，兼容中文片段。
- 剪贴板序号与私有所有权校验，避免转换期间覆盖他人复制内容。
- 单实例运行、快捷键冲突提示、粘贴失败安全提示。

## 快速开始

1. 运行发布版本：`artifacts/win-x64/CopyToMd.exe`。
2. 系统托盘会出现 Copy to MD 图标。
3. 在 PPT 中正常复制内容。
4. 切换到 Obsidian、Typora、VS Code、Word、记事本等目标应用。
5. 按默认智能粘贴快捷键：`Ctrl+Alt+Shift+V`。

托盘菜单提供：

- 自动识别目标
- 强制 Markdown
- 强制 Word
- 强制纯文本
- 打开配置
- 退出

配置文件位于可执行文件旁边的 `appsettings.json`。修改快捷键或规则后需要重启工具。

## 目标识别规则

用户规则优先于默认规则；未匹配或识别失败时回退为保守纯文本。

| 目标 | 默认输出 |
|---|---|
| Obsidian | Markdown |
| Typora | Markdown |
| VS Code 且窗口标题显示 `.md` / `.markdown` / `.mdx` | Markdown |
| Microsoft Word | Word RTF + UnicodeText |
| 记事本 | 纯文本 |
| VS Code 非 Markdown 文件 | 纯文本 |
| 未知应用 | 纯文本 |

VS Code 无标题文档、浏览器编辑器等无法可靠判断的场景不会猜测，可使用托盘手动模式。

## 公式上下标

Markdown 使用 LaTeX 片段；Word 保留真实上下标格式；纯文本优先使用 Unicode 上下标字符。

| 来源示例 | Markdown | Word | 纯文本 |
|---|---|---|---|
| `x²` | `$x^{2}$` | 真实上标 | `x²` |
| `H₂O` | `$H_{2}O$` | 真实下标 | `H₂O` |
| `x<sub>i</sub><sup>2</sup>` | `$x_{i}^{2}$` | 真实上下标 | `xᵢ²` |

兼容来源包括：

- HTML：`<sub>`、`<sup>`、CSS `vertical-align: sub/super/baseline`
- RTF：`\sub`、`\super`
- 已经退化为 Unicode 的 `²`、`₂` 等字符

纯文本中无法映射为 Unicode 上下标的字符会使用可读回退，例如 `^(ab)`、`_(ab)`。

## 配置

默认配置：

```json
{
  "hotKey": "Ctrl+Alt+Shift+V",
  "showSuccessNotifications": false,
  "maxClipboardCharacters": 500000,
  "userRules": []
}
```

字段说明：

| 字段 | 说明 |
|---|---|
| `hotKey` | 全局智能粘贴快捷键，至少包含一个修饰键和一个主键 |
| `showSuccessNotifications` | 成功粘贴后是否弹出托盘通知 |
| `maxClipboardCharacters` | 剪贴板文本长度上限 |
| `userRules` | 自定义目标规则，按数组顺序优先匹配 |

自定义规则示例：

```json
{
  "userRules": [
    {
      "name": "我的 Markdown 编辑器",
      "processName": "myeditor",
      "mode": "Markdown"
    },
    {
      "name": "Word 文档",
      "processName": "winword",
      "titlePattern": "\\.docx?",
      "mode": "Word"
    }
  ]
}
```

规则字段：

| 字段 | 说明 |
|---|---|
| `name` | 规则名称，用于状态显示 |
| `processName` | 进程名，可省略 `.exe`，不区分大小写 |
| `titlePattern` | 窗口标题正则，忽略大小写，带超时保护 |
| `extensions` | 窗口标题中匹配的文件扩展名，例如 `["md", "markdown"]` |
| `mode` | `Markdown`、`Word`、`PlainText` |

## 构建与测试

需要 Windows 和 .NET 10 SDK。

```bash
dotnet test CopyToMd.sln -c Release
dotnet build CopyToMd.sln -c Release
```

发布压缩自包含单文件版本：

```bash
dotnet publish src/CopyToMd/CopyToMd.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o artifacts/win-x64
```

当前测试覆盖纯文本保守清理、HTML/CF_HTML、RTF、目标识别、剪贴板保护、粘贴协调、Markdown/Word/纯文本渲染以及上下标转换。

## 安全与隐私

- 转换在本地完成，不主动上传剪贴板内容。
- 不监听剪贴板变化，不劫持普通 `Ctrl+V`。
- 只在智能粘贴时一次性读取剪贴板快照。
- 转换前后检查前台窗口和剪贴板序号，发现变化会中止。
- 写入剪贴板后校验私有所有权标记，避免误覆盖其他程序的新复制。
- 模拟粘贴失败或部分失败时不盲目重试，避免重复粘贴。
- 粘贴后保留转换结果，不定时恢复原剪贴板，以免覆盖用户后续复制。
- 状态只表示粘贴按键已发送，不虚构目标应用已确认接收成功。

## 已知限制

- 不支持图片、表格、嵌入对象和完整 PowerPoint OMML 公式对象转换；当前公式支持限于文本上下标。
- Word 超链接字段在 RichTextBox 往返中不稳定，因此 Word 输出保留链接显示文字；Markdown 链接正常输出。
- 未知应用默认纯文本，不猜测目标格式。
- 纯文本伪项目符号清理是保守策略：少量可疑行可能保留，避免误删正常英文。
- 真实 PPT 到桌面编辑器的端到端粘贴仍建议手动验收一次。

## 项目结构

```text
src/CopyToMd/
  App/             托盘应用与快捷键消息窗口
  Clipboard/       剪贴板快照、保护与模拟粘贴
  Configuration/   JSON 配置与快捷键解析
  Conversion/      文档模型、导入器、渲染器、上下标处理
  Interop/         Win32 互操作
  Target/          前台目标识别与规则
tests/CopyToMd.Tests/
  xUnit 测试，包含 STA RichTextBox 测试
artifacts/win-x64/
  已发布的自包含单文件版本
```
