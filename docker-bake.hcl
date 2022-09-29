variable "BUILD_DATE" {
    default="2022-09-27T13:37:01+00:00"
}

variable "BUILD_NUMBER" {
    default="6.2.195-ankith-fm-testmain"
}

target "octopusdeploy-tentacle-linux" {
  dockerfile = "./docker/linux/Dockerfile"
  tags = ["docker.packages.octopushq.com/octopusdeploy/tentacle:${BUILD_NUMBER}-linux"]
  platforms = ["linux/amd64", "linux/arm64"]
  args = {
    BUILD_DATE = "${BUILD_DATE}"
    BUILD_NUMBER = "${BUILD_NUMBER}"
  }
}

target "octopusdeploy-tentacle-windows-2019" {
  dockerfile = "./docker/linux/Dockerfile"
  tags = ["docker.packages.octopushq.com/octopusdeploy/tentacle:${BUILD_NUMBER}-windows-2019"]
  platforms = ["windows/amd64"]
  args = {
    BUILD_DATE = "${BUILD_DATE}"
    BUILD_NUMBER = "${BUILD_NUMBER}"
  }
}