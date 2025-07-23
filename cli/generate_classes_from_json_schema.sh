#!/bin/sh
set -e

TOPIC_NAME="";
SCHEMA_PATH="";
OUTPUT_PATH="";
NAMESPACE="";
RUNNING_IN_PIPELINE=0;
USE_DOCKER=0;

while [ "$#" -gt 0 ]; do
  case "$1" in
    --topic_name) TOPIC_NAME=$2; shift 2;;
    --schema_path) SCHEMA_PATH=$2; shift 2;;
    --output_path) OUTPUT_PATH=$2; shift 2;;
    --namespace) NAMESPACE=$2; shift 2;;
    --cicd) RUNNING_IN_PIPELINE=1; USE_DOCKER=1; shift 1;;

    -*) echo "unknown option: $1" >&2; exit 1;;
  esac
done

IMAGE_NAME="kafkautils-generator";

PROJ_DIR="./setup/local/ClassGeneratorFromJsonSchema/";
docker build -t "${IMAGE_NAME}:latest" -f "${PROJ_DIR}Dockerfile" .;

mkdir -p "${SCHEMA_PATH}";
mkdir -p "${OUTPUT_PATH}";

docker run -it --rm \
  --user $(id -u):$(id -g) \
  -v "${SCHEMA_PATH}:/Schemas" \
  -v "${OUTPUT_PATH}:/Models" \
  -e KAFKA_TOPIC_NAME=${TOPIC_NAME} \
  -e SCHEMA_FILE_PATH=/Schemas \
  -e GENERATED_CLASSES_PATH=/Models \
  -e GENERATED_CLASSES_NAMESPACE=${NAMESPACE} \
  $IMAGE_NAME;
