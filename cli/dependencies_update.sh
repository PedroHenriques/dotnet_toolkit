#!/bin/sh
set -e;

UPDATE=0;

while [ "$#" -gt 0 ]; do
  case "$1" in
    -u|--update) UPDATE=1; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
  esac
done

OPTS="";
if [ $UPDATE -eq 1 ]; then
  OPTS="${OPTS} -u:Prompt";
fi

dotnet outdated $OPTS ./*.sln;