#!/bin/bash

ENVIRONMENT=$1

docker compose \
  -p demo-${ENVIRONMENT} \
  --env-file .env.${ENVIRONMENT} \
  up -d --build