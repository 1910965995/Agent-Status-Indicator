---
name: Agent-Status-Indicator-init
description: 初始化 Agent-Status-Indicator，配置项目 hooks 并告知 WPF 悬浮灯当前项目路径
---

运行 `bash ~/.claude/tools/init-status.sh` 来初始化状态指示器。
该脚本会：
1. 在 `~/.claude/agent-status/project.json` 写入当前项目路径，WPF 应用读取后即知监控目标
2. 在当前项目的 `.claude/settings.json` 中配置 Claude Code hooks（PreToolUse/PostToolUse/Stop）

每次在新的项目目录使用 Claude Code 时，先调用此 skill 初始化。
