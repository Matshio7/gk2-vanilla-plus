#!/bin/zsh
# Doppelklick: startet das Workshop-Dashboard und oeffnet es im Browser. Fenster offen lassen, solange es laufen soll.
cd "${0:A:h}"
if lsof -iTCP:8765 -sTCP:LISTEN >/dev/null 2>&1; then
  open http://localhost:8765
  exit 0
fi
(sleep 2; open http://localhost:8765) &
exec python3 server.py
