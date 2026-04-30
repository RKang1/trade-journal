#!/bin/sh
set -eu

: "${TRADE_JOURNAL_API_BASE_URL:=http://localhost:5211}"
: "${TRADE_JOURNAL_GOOGLE_CLIENT_ID:=REPLACE_WITH_GOOGLE_OAUTH_CLIENT_ID.apps.googleusercontent.com}"

envsubst '${TRADE_JOURNAL_API_BASE_URL} ${TRADE_JOURNAL_GOOGLE_CLIENT_ID}' \
  < /usr/share/nginx/html/config.js.template \
  > /usr/share/nginx/html/config.js
