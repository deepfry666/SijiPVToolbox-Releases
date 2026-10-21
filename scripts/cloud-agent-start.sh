#!/usr/bin/env bash
# Start the virtual display used by WinForms layout tests. Safe to run again.
set -euo pipefail

if [[ -S /tmp/.X11-unix/X99 ]]; then
  exit 0
fi

mkdir -p /tmp/sijipv
setsid Xvfb :99 -screen 0 1920x1080x24 -nolisten tcp \
  >/tmp/sijipv/xvfb.log 2>&1 </dev/null &

for _ in $(seq 1 50); do
  if [[ -S /tmp/.X11-unix/X99 ]]; then
    exit 0
  fi
  sleep 0.1
done

echo "Xvfb :99 did not become ready. See /tmp/sijipv/xvfb.log" >&2
exit 1
