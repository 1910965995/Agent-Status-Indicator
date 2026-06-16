#!/bin/bash
# init-status.sh — Initialize Agent-Status-Indicator for the current project
# Run this in each Claude Code project directory to tell the WPF app
# which project's session to monitor.

CONFIG_DIR="$HOME/.claude/agent-status"
CONFIG_FILE="$CONFIG_DIR/project.json"

mkdir -p "$CONFIG_DIR"

# Get Windows path (Claude Code uses Windows path encoding)
WIN_PATH=$(cygpath -w "$(pwd)" 2>/dev/null || cmd //c echo //c cd 2>/dev/null || pwd)

# Encode like Claude Code: D:\WorkSpace\... → D--WorkSpace-...
# Replace : with -, then \ with -
ENCODED=$(echo "$WIN_PATH" | sed 's/:/-/g' | sed 's/\\/-/g')

cat > "$CONFIG_FILE" << EOF
{
  "project_dir": "$WIN_PATH",
  "encoded_name": "$ENCODED",
  "initialized_at": "$(date -Iseconds)"
}
EOF

echo "✅ Agent-Status-Indicator initialized for project:"
echo "   $WIN_PATH"
echo ""
echo "   Session dir: ~/.claude/projects/$ENCODED/"
