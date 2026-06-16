#!/bin/bash
# status-writer.sh — Called by Claude Code hooks to write current status
# Usage: status-writer.sh <status> [message]

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
