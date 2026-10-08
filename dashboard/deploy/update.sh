#!/bin/zsh
# Geaenderten Dashboard-Code auf den Hetzner-Server bringen und neu starten (Verlauf in /opt/gk2dash/data bleibt).
# Adresse: https://gk2.46-224-116-188.sslip.io (Benutzer mats). Push-Meldungen: NTFY_TOPIC=... in /opt/gk2dash/.env
set -e
cd "${0:A:h:h}"
K=~/.ssh/id_ed25519_radar; H=root@46.224.116.188
scp -q -i $K server.py index.html deploy/Dockerfile deploy/docker-compose.yml $H:/opt/gk2dash/
ssh -i $K $H 'cd /opt/gk2dash && docker compose up -d --build 2>&1 | tail -1'
