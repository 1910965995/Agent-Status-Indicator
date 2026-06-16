---
name: Agent-Status-Indicator-init
description: 初始化 Agent-Status-Indicator，告诉 WPF 悬浮灯当前项目路径
---

运行 `bash tools/init-status.sh` 来初始化状态指示器。
这会在 `~/.claude/agent-status/project.json` 写入当前项目路径，
WPF 应用读取后就知道要监控哪个项目的 Claude Code 会话。

每次在新的项目目录使用 Claude Code 时，先调用此 skill 初始化。
