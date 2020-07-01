#!/bin/bash
# Test that .deb and .rpm packages in the working directory install a Tentacle command that runs successfully.

if [[ ! -e "$LPF_PATH" ]]; then
  echo "This script requires the environment variable LPF_PATH - the location of 'linux-package-feeds' scripts to use." >&2
  echo "They come from https://github.com/OctopusDeploy/linux-package-feeds, distributed in TeamCity" >&2
  echo "  via 'Infrastructure / Linux Package Feeds'." >&2
  exit 1
fi

which docker >/dev/null || {
  echo 'This script requires docker.' >&2
  exit 1
}
SCRIPT_DIR="$(dirname "$(realpath "${BASH_SOURCE[0]}")")"


for DOCKER_IMAGE in $(cat "$LPF_PATH/test-env-docker-images.conf" | grep -o '^[^#]*' | tr -d '\r')
do
  if [[ "$DOCKER_IMAGE" == *rhel* ]]; then
    RHEL_OPTS='--env REDHAT_SUBSCRIPTION_USERNAME --env REDHAT_SUBSCRIPTION_PASSWORD'
  else
    RHEL_OPTS=''
  fi

  echo "== Testing in '$DOCKER_IMAGE' =="
  docker pull "$DOCKER_IMAGE" >/dev/null || exit
  docker run --rm \
    --hostname "tentacletestpkg$RANDOM" \
    --volume "$(pwd):/working" --volume "$SCRIPT_DIR/test-linux-package.sh:/test-linux-package.sh" \
    --volume "$(realpath "$LPF_PATH"):/opt/linux-package-feeds" \
    $RHEL_OPTS \
    "$DOCKER_IMAGE" bash -c 'cd /working && bash /test-linux-package.sh' || exit
done
