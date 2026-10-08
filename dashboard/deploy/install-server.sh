#!/bin/zsh
# Einmalig: Dashboard auf den Hetzner-Server bringen (Verlauf vom Mac mitnehmen) und starten.
# Danach fuer Code-Aenderungen: deploy/install-server.sh code
set -e
cd "${0:A:h:h}"
K=~/.ssh/id_ed25519_radar; H=root@46.224.116.188
ssh -i $K $H 'mkdir -p /opt/gk2dash/data && touch /opt/gk2dash/.env && chmod 600 /opt/gk2dash/.env'
scp -q -i $K server.py index.html deploy/Dockerfile deploy/docker-compose.yml $H:/opt/gk2dash/
[ "$1" = code ] || scp -q -i $K data/history.jsonl $H:/opt/gk2dash/data/
ssh -i $K $H 'cd /opt/gk2dash && docker compose up -d --build 2>&1 | tail -2 && sleep 4 && docker ps --filter name=gk2-dashboard --format "{{.Names}}: {{.Status}}"'
