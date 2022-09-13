#!/bin/bash

set -e
set -o pipefail

if [[ -z "$1" ]]
then
  echo "Usage: process-history.sh <history directory>"
  exit 1
fi

dotnet run --project src/Google.Cloud.Tools.ApiHistoryReader -- \
  $1

echo "Done."
