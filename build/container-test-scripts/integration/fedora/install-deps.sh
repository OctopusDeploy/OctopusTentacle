#!/bin/bash
set -o xtrace
set -e

sudo dnf install krb5-libs libicu openssl-libs zlib iputils wget -y

# https://github.com/OctopusDeploy/BuildAgentAutomation/blob/main/Fedora/scripts/install-openssl.sh
sudo dnf install openssl make -y || exit 1
# DotNet on Fedora OS requires compat-openssl-10 however this is not available in Fedoras repositories
# https://learn.microsoft.com/en-us/dotnet/core/install/linux-fedora#dependencies
# Issue raised https://github.com/dotnet/sdk/issues/27360
# We are using the CentOS repository pkg instead until an official workaround is documented
sudo rpm --import https://www.centos.org/keys/RPM-GPG-KEY-CentOS-Official
sudo wget https://vault.centos.org/centos/8/AppStream/x86_64/os/Packages/compat-openssl10-1.0.2o-3.el8.x86_64.rpm
sudo rpm -i compat-openssl10-1.0.2o-3.el8.x86_64.rpm
rm -f compat-openssl10-1.0.2o-3.el8.x86_64.rpm
