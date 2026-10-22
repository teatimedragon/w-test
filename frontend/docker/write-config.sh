#!/bin/sh
# Writes the runtime config the React app reads (window.__APP_CONFIG__) from environment variables,
# so APP_BASE_URL can change without rebuilding the image.
set -eu
json_escape() { printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'; }
cat > /usr/share/nginx/html/config.js <<CONFIG
window.__APP_CONFIG__ = { appBaseUrl: "$(json_escape "${APP_BASE_URL:-}")", storeId: "$(json_escape "${STORE_ID:-}")" };
CONFIG
# Point the API proxy at the configured upstream.
sed -i "s#__API_UPSTREAM__#${API_UPSTREAM}#g" /etc/nginx/conf.d/default.conf
