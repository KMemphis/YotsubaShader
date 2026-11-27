#!/usr/bin/env bash
# Local Discord test run. Secrets live in ~/.config/yotsuba-community/env (mode 600), never in the repo.
#   community.sh setup   asks (hidden) for any missing value
#   community.sh run     starts the backend on 127.0.0.1:5080 and the Hub pointed at it
set -euo pipefail
ENVF="$HOME/.config/yotsuba-community/env"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
mkdir -p "$(dirname "$ENVF")"; touch "$ENVF"; chmod 600 "$ENVF"

ask() { # name, label, secret?
  grep -q "^$1=." "$ENVF" && return
  local v; if [ "${3:-}" = secret ]; then read -r -s -p "$2: " v; echo; else read -r -p "$2: " v; fi
  [ -n "$v" ] && echo "$1=$v" >> "$ENVF"
}

case "${1:-}" in
  setup)
    ask Community__Discord__BotToken "Bot token (Discord > Bot > Reset Token)" secret
    ask Community__Discord__GuildId "Server id"
    ask Community__Discord__ForumChannelId "Forum channel id"
    ask Community__Discord__InviteUrl "Server invite link (optional, Enter to skip)"
    echo "Saved in $ENVF" ;;
  run)
    set -a; . "$ENVF"; set +a
    export ASPNETCORE_URLS=http://127.0.0.1:5080
    dotnet run --project "$ROOT/Apps/YotsubaShader.Community" &
    BACK=$!; trap 'kill -9 $BACK 2>/dev/null' EXIT
    sleep 6
    YOTSUBA_COMMUNITY_URL=http://127.0.0.1:5080 dotnet run --project "$ROOT/Apps/YotsubaShader.Hub" ;;
  *) echo "usage: $0 setup|run"; exit 1 ;;
esac
