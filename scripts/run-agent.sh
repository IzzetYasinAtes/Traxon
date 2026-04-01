#!/bin/bash
# Traxon Agent Runner — log + terminal visibility
# Usage: ./scripts/run-agent.sh <agent-name> <claude-args...>
# Example: ./scripts/run-agent.sh architect -p "Plan yap..." --append-system-prompt-file agents/architect/CLAUDE.md

AGENT_NAME=$1
shift

LOGS_DIR="workspace/logs"
mkdir -p "$LOGS_DIR"

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
LOG_FILE="$LOGS_DIR/${AGENT_NAME}-${TIMESTAMP}.log"
LATEST_LOG="$LOGS_DIR/${AGENT_NAME}-latest.log"

# Run claude with tee for both terminal and log
claude "$@" 2>&1 | tee "$LOG_FILE"
EXIT_CODE=${PIPESTATUS[0]}

# Update latest symlink
cp "$LOG_FILE" "$LATEST_LOG"

exit $EXIT_CODE
