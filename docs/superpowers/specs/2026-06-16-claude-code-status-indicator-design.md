# Claude Code CLI Status Indicator — 设计文档

## 概述

一个 Windows 桌面悬浮灯插件，通过圆环呼吸动画实时提示 Claude Code CLI 的运行状态。基于 **WPF (.NET 10)** 开发，通过 Claude Code hooks 机制 + 文件状态同步实现状态检测。

## 系统架构

```
┌─────────────────────────────────────────────┐
│  Claude Code CLI                            │
│  ┌─────────────────────────────────────────┐ │
│  │ claude.json (hooks 配置)                │ │
│  │   onTaskStart  → hooks/status-writer.sh │ │
│  │   onTaskComplete→ hooks/status-writer.sh│ │
│  │   onTaskError   → hooks/status-writer.sh│ │
│  └─────────────────────────────────────────┘ │
│                     │                         │
│                     ▼                         │
│  ┌─────────────────────────────────────────┐ │
│  │ hooks/status-writer.sh                 │ │
│  │ 写入 ~/.claude/agent-status/status.json │ │
│  └─────────────────────────────────────────┘ │
└──────────────────────┬──────────────────────┘
                       │ FileSystemWatcher
                       ▼
┌─────────────────────────────────────────────┐
│  AgentStatusIndicator.exe (WPF)              │
│  ┌─────────────────────────────────────────┐ │
│  │ StatusMonitorService                    │ │
│  │  - FileSystemWatcher 监控状态文件       │ │
│  │  - 超时兜底 (10秒无更新→空闲)          │ │
│  ├─────────────────────────────────────────┤ │
│  │ FloatingRingControl                     │ │
│  │  - 40px 圆环 UI                         │ │
│  │  - 呼吸脉冲动画                         │ │
│  │  - 可拖拽                               │ │
│  ├─────────────────────────────────────────┤ │
│  │ DetailCardControl                       │ │
│  │  - 状态文字 + 运行时长 + 退出按钮      │ │
│  └─────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
```

## 组件职责

### Hooks 脚本 (Shell)
- Claude Code 事件触发时写入 `~/.claude/agent-status/status.json`
- 由 `claude.json` 的 hooks 配置自动调用

### StatusMonitorService (WPF)
- 后台服务，通过 `FileSystemWatcher` 监控 status.json 变更
- 解析 JSON，通知 UI 更新
- 管理超时计时器：10 秒无更新 → 自动回到空闲

### FloatingRingControl (WPF)
- 40px 空心圆环 UI 控件
- 呼吸脉冲动画（放大 + 发光 + 透明度变化）
- 鼠标拖拽支持

### DetailCardControl (WPF)
- 点击圆环展开的详情卡片
- 显示状态文字、当前任务、运行时长、退出按钮

## 状态定义

| 状态 | 含义 | 触发条件 |
|------|------|---------|
| idle | 空闲中 | 启动时 / 完成/出错后超时回空闲 / Claude 未运行 |
| running | 运行中 | Claude Code `onTaskStart` 触发 |
| completed | 完成 | Claude Code `onTaskComplete` 触发 |
| error | 出错 | Claude Code `onTaskError` 触发 |

## 状态文件格式

状态文件路径：`~/.claude/agent-status/status.json`

```json
{
  "status": "idle | running | completed | error",
  "task": "当前任务描述（可选）",
  "started_at": "2026-06-16T09:00:00+08:00"
}
```

## UI 设计

### 主界面：圆环悬浮窗
- 形状：空心圆环，边框 4px
- 尺寸：40px × 40px
- 透明度：默认 70% 透明度，鼠标悬停 100%
- 行为：可自由拖拽，始终置顶（Topmost = true）

### 呼吸脉冲参数

| 状态 | 颜色 | 周期 | 缩放幅度 | 发光效果 |
|------|------|------|----------|----------|
| 🟢 空闲 | #4caf50 | 3s（舒缓） | 1.0 ↔ 1.05 | 无发光 |
| 🟡 运行中 | #ffc107 | 1.5s（急促） | 1.0 ↔ 1.18 | 黄色发光 (box-shadow) |
| 🔵 完成 | #2196f3 | 2s（渐缓） | 1.0 ↔ 1.12 | 微弱蓝色发光 |
| 🔴 出错 | #f44336 | 1s（急促） | 1.0 ↔ 1.15 | 红色发光 |

呼吸动效 = 同时进行：放大/缩小(ScaleTransform) + 明暗变化(Opacity) + 光晕扩散/收缩(DropShadowEffect)

### 详情卡片（点击展开）
- 大小：约 280px × 160px
- 内容：
  - 左上角：状态颜色圆点 + 状态文字 + 当前任务描述
  - 中间：运行时长实时计时器
  - 底部：退出插件按钮（红色）
- 交互：点击圆环展开，点击外部或再次点击圆环收起

## 状态流转

```
       ┌─── onTaskStart ───┐
  空闲 ──────────────────► 运行中
   ▲                        │
   │  10s 超时              │ onTaskComplete
   │  (Claude 崩溃/退出)    │
   │                        ▼
   │                      完成
   │                        │
   │                        10s 后自动
   │                        │
   │                        ▼
   │  60s 后自动           空闲
   │   ──── 出错 ◄── onTaskError
   │        ▲
   └────────┘
```

### 状态切换延迟规则
- **completed → idle**：10 秒延迟（展示完成反馈）
- **error → idle**：60 秒延迟（确保用户注意到错误）
- 其他状态切换：即时

## 边界情况处理

| 场景 | 处理方式 |
|------|---------|
| Claude 崩溃 | 10 秒超时 → 自动回到空闲 |
| 插件启动时 Claude 未运行 | 状态文件不存在 → 直接显示空闲 |
| 连续快速任务 | 每次读取时比较新旧 status，completed→running 无需等待直接切换 |
| 状态文件损坏 | 解析失败 → 保持当前状态，等待下次文件变更 |
| 文件写入冲突 | .NET FileSystemWatcher 防抖处理（合并高频变更） |

## 项目结构

```
Agent-Status-Indicator/
├── src/
│   ├── AgentStatusIndicator/           # WPF 主项目
│   │   ├── App.xaml                    # 应用入口
│   │   ├── MainWindow.xaml             # 悬浮窗宿主窗口 (透明置顶)
│   │   ├── Controls/
│   │   │   ├── FloatingRing.xaml       # 圆环控件
│   │   │   └── DetailCard.xaml         # 详情卡片控件
│   │   ├── Services/
│   │   │   ├── StatusMonitorService.cs  # 文件监控 + 超时
│   │   │   └── AnimationService.cs      # 呼吸动画管理
│   │   ├── Models/
│   │   │   └── AgentStatus.cs           # 状态数据模型
│   │   └── appsettings.json             # 配置
│   │
│   └── hooks/
│       └── status-writer.sh             # 状态写入脚本
│
├── docs/
│   └── superpowers/specs/
│       └── 2026-06-16-claude-code-status-indicator-design.md
│
└── README.md
```

## 技术栈

| 技术 | 用途 |
|------|------|
| .NET 10 / WPF | 桌面应用框架 |
| C# | 主要开发语言 |
| FileSystemWatcher | 文件变更监控 |
| DoubleAnimation / Storyboard | WPF 动画系统 |
| Shell Script (bash) | Claude Code hooks 脚本 |
| xUnit | 单元测试（可选） |

## 非功能需求

- 内存占用目标：≤ 30MB
- CPU 占用目标：空闲时 ≈ 0%
- 启动方式：双击运行 / 可配置开机自启
- 退出方式：详情卡片退出按钮 / 右键托盘退出
- 配置文件：`appsettings.json`（颜色、呼吸速度、状态文件路径可自定义）
