#!/bin/bash
# init-status.sh — Initialize Agent-Status-Indicator for the current project
# 1. Writes project config for the WPF app
# 2. Sets up Claude Code hooks in .claude/settings.json

set -e

CONFIG_DIR="$HOME/.claude/agent-status"
CONFIG_FILE="$CONFIG_DIR/project.json"

mkdir -p "$CONFIG_DIR"

# Get Windows path (Claude Code uses Windows path encoding)
WIN_PATH=$(cygpath -w "$(pwd)" 2>/dev/null || cmd //c echo //c cd 2>/dev/null || pwd)

# Encode like Claude Code: D:\WorkSpace\... → D--WorkSpace-...
ENCODED=$(echo "$WIN_PATH" | sed 's/:/-/g' | sed 's/\\/-/g')

# ─── Step 1: Write project config for WPF app ───
cat > "$CONFIG_FILE" << EOF
{
  "project_dir": "$WIN_PATH",
  "encoded_name": "$ENCODED",
  "initialized_at": "$(date -Iseconds)"
}
EOF

echo "✅ Agent-Status-Indicator initialized for project:"
echo "   $WIN_PATH"
echo "   Session dir: ~/.claude/projects/$ENCODED/"
echo ""

# ─── Step 2: Setup Claude Code hooks ───
PROJECT_CLAUDE_DIR="$(pwd)/.claude"
HOOKS_SETTINGS="$PROJECT_CLAUDE_DIR/settings.json"

mkdir -p "$PROJECT_CLAUDE_DIR"

# Hooks configuration
HOOKS_JSON=$(cat << 'HOOKS_EOF'
{
  "hooks": {
    "PreToolUse": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash ~/.claude/tools/status-writer.sh running \"$CLAUDE_TOOL_NAME\""
          }
        ]
      }
    ],
    "PostToolUse": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash ~/.claude/tools/status-writer.sh running \"$CLAUDE_TOOL_NAME\""
          }
        ]
      }
    ],
    "Notification": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash ~/.claude/tools/status-writer.sh running \"notification\""
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash ~/.claude/tools/status-writer.sh completed"
          }
        ]
      }
    ]
  }
}
HOOKS_EOF
)

# Update .claude/settings.json — merge hooks with existing settings
STATUS_WRITER="$HOME/.claude/tools/status-writer.sh"

if [ ! -f "$STATUS_WRITER" ]; then
  # Create the global status-writer.sh
  mkdir -p "$(dirname "$STATUS_WRITER")"
  cat > "$STATUS_WRITER" << 'WRITER_EOF'
#!/bin/bash
STATUS_DIR="$HOME/.claude/agent-status"
STATUS_FILE="$STATUS_DIR/status.json"
STATUS="${1:-idle}"
MESSAGE="${2:-}"
mkdir -p "$STATUS_DIR"
cat > "$STATUS_FILE" << EOF
{
  "status": "$STATUS",
  "task": "$MESSAGE",
  "started_at": "$(date -Iseconds)"
}
EOF
WRITER_EOF
  chmod +x "$STATUS_WRITER"
fi

if [ -f "$HOOKS_SETTINGS" ]; then
  # Merge: keep existing keys, add/update hooks
  python3 -c "
import json, sys
with open('$HOOKS_SETTINGS') as f:
    try:
        existing = json.load(f)
    except:
        existing = {}
existing['hooks'] = json.loads('''$HOOKS_JSON''')['hooks']
with open('$HOOKS_SETTINGS', 'w') as f:
    json.dump(existing, f, indent=2, ensure_ascii=False)
    f.write('\n')
" 2>/dev/null || {
    echo "⚠️  Python not available, overwriting .claude/settings.json with hooks config"
    echo "$HOOKS_JSON" > "$HOOKS_SETTINGS"
  }
else
  echo "$HOOKS_JSON" > "$HOOKS_SETTINGS"
fi

echo "✅ Claude Code hooks configured:"
echo "   .claude/settings.json"
echo ""
echo "   🟡 PreToolUse/PostToolUse → running"
echo "   🔵 Stop → completed"
echo ""
echo "Now restart Claude Code or start a new session."
