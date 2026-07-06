# 窗口翻译工具

当前实现范围：P1 到 P9。

## 已完成

- .NET 8 WPF 项目结构
- 后台常驻托盘图标
- 托盘菜单：截图翻译、历史记录、设置、退出
- `appsettings.json` 设置读取和保存
- 全局快捷键 `Alt + Q`
- 全屏截图框选遮罩，支持拖动选择和 `ESC` 取消
- 截图 Bitmap 捕获
- 截图 OCR 链路：PaddleOCR 本地优先，Windows OCR 和 AI OCR 兜底
- 悬浮翻译窗口，支持关闭、复制、显示音标/注释/来源
- Argos Translate 英中离线翻译；不可用时退回本地词典兜底
- AI 翻译服务，API Key 从 `appsettings.json` 读取
- 翻译协调器：AI 超时先显示离线结果，AI 返回后更新悬浮窗
- 通过 `Alt + Q` 手动框选截图 OCR 翻译
- 剪贴板兜底会保存并恢复用户原剪贴板内容
- 音标接口与本地基础词典，提取重点英文词并过滤常见虚词
- AI 翻译返回结构已预留并解析美式音标字段
- Windows 系统 TTS 朗读兜底
- 悬浮窗“朗读”按钮可朗读原文
- SQLite 历史记录持久化
- 每次最终翻译结果保存原文、译文、音标 JSON、注释、来源、引擎和 OCR 标记
- 历史记录窗口支持倒序列表、复制译文、刷新、清空历史

## 运行

```powershell
dotnet run --project .\TranslatorTool.csproj
```

程序启动后默认隐藏主窗口，请在系统托盘中找到“窗口翻译工具”图标。

## 当前测试方式

```powershell
dotnet build
dotnet run --project ..\TranslatorTool.Tests\TranslatorTool.Tests.csproj
```

手动验收：

1. 启动程序后托盘出现图标。
2. 托盘菜单可以打开设置、历史记录和测试悬浮窗。
3. 按 `Alt + Q` 进入截图模式。
4. 拖动选择区域后触发 PaddleOCR/Windows OCR/AI OCR 和翻译。
5. 按 `ESC` 可以取消截图模式。
6. 翻译结果窗口内可选择英文单词或短句查看词卡、音标、翻译和朗读。
7. 托盘菜单“退出”可以释放资源并退出。

## 自动验证

`TranslatorTool.Tests` 当前覆盖：

- AI 快速返回时直接显示 AI 最终结果
- AI 慢响应时先显示离线结果，再显示 AI 最终结果
- AI 失败时保留离线结果并显示失败状态
- Windows OCR 和 PaddleOCR 对生成图片的基础识别能力
- UI Automation 无选区时，选中文本服务会调用剪贴板兜底并修剪文本
- 音标服务会提取重点技术词并过滤常见虚词
- 悬浮窗朗读按钮会调用朗读服务并传入原文
- SQLite 历史记录服务会保存、倒序读取并清空记录

完整实机验收步骤见仓库根目录：[验收清单.md](../验收清单.md)。

## 下一阶段

- 继续增强在线 TTS、历史记录搜索和更完整的 AI 配置
