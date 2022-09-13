#!/bin/bash

set -e
set -o pipefail

if [[ -z "$1" ]]
then
  echo "Usage: generate-history.sh <googleapis directory>"
  exit 1
fi

GOOGLEAPIS=$1

SCRIPT=$(readlink -f "$0")
SCRIPT_DIR=$(dirname "$SCRIPT")
REPO_ROOT=$(realpath "$SCRIPT_DIR/..")

cd $REPO_ROOT

source $SCRIPT_DIR/toolfunctions.sh
install_protoc

rm -rf tmp
mkdir tmp

dotnet run --project src/Google.Cloud.Tools.ApiHistory -- \
  $GOOGLEAPIS $PROTOC $PROTOBUF_ROOT/include tmp

echo "Done."
