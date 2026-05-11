#!/usr/bin/env bash
set -euo pipefail

WORKER_DLL="${CRYPTO_SIGNAL_BOT_WORKER_DLL:-/app/worker/CryptoSignalBot.Worker.dll}"
ANALYZE_SECONDS="${SCHEDULE_ANALYZE_SECONDS:-900}"
REPORT_SECONDS="${SCHEDULE_REPORT_SECONDS:-21600}"
CLEANUP_SECONDS="${SCHEDULE_CLEANUP_SECONDS:-86400}"

last_analyze=0
last_report=0
last_cleanup=0

run_worker() {
  echo "[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] dotnet $WORKER_DLL $*"
  dotnet "$WORKER_DLL" "$@" || true
}

echo "CryptoSignalBot scheduler started."
echo "Analyze every ${ANALYZE_SECONDS}s, report every ${REPORT_SECONDS}s, cleanup every ${CLEANUP_SECONDS}s."

while true; do
  now="$(date +%s)"

  if (( now - last_analyze >= ANALYZE_SECONDS )); then
    run_worker --report-watchlist --force-report --send-empty-report
    last_analyze="$now"
  fi

  if (( now - last_report >= REPORT_SECONDS )); then
    run_worker --paper-trade-report
    last_report="$now"
  fi

  if (( now - last_cleanup >= CLEANUP_SECONDS )); then
    run_worker --cleanup-db
    last_cleanup="$now"
  fi

  sleep 30
done
