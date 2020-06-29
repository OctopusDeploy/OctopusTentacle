#!/bin/bash
# Smoke test our apt and rpm feeds in various dockerized distros.

if [[ ! -e "$LPF_PATH" ]]; then
  echo 'This script requires the environment variable LPF_PATH - the location of "linux-package-feeds" tools to use.' >&2
  exit 1
fi
if [[ -z "$PUBLISH_LINUX_EXTERNAL" ]]; then
  echo 'This script requires the environment variable PUBLISH_LINUX_EXTERNAL - specify "true" to test the external public feed.' >&2
  exit 1
fi

SCRIPT_DIR="$(dirname "$(realpath "${BASH_SOURCE[0]}")")"


for DOCKER_IMAGE in $(cat "$LPF_PATH/test-env-docker-images.conf" | grep -o '^[^#]*' | tr -d '\r' | head -n2) # ZZDY HEAD ONLY
do
  echo "== Testing in '$DOCKER_IMAGE' =="
  docker pull "$DOCKER_IMAGE" >/dev/null || exit
  docker run --rm \
    --hostname "tentacletestfeedpkg$RANDOM" \
    --volume "$(pwd):/working" --volume "$SCRIPT_DIR/test-linux-package-from-feed.sh:/test-linux-package-from-feed.sh" \
    --volume "$(realpath "$LPF_PATH"):/opt/linux-package-feeds" \
    --env PUBLISH_LINUX_EXTERNAL \
    --env REDHAT_SUBSCRIPTION_USERNAME --env REDHAT_SUBSCRIPTION_PASSWORD \
    "$DOCKER_IMAGE" bash -c 'cd /working && bash /test-linux-package-from-feed.sh' || exit
done
