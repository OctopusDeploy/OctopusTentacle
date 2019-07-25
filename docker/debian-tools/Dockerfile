FROM ubuntu:bionic

RUN apt-get update && apt-get install -y gnupg1 wget

RUN apt-get update && apt-get install -y \
  ruby-dev \
  gcc \
  make \
  ruby \
  gpgv1 \
  && rm -rf /var/lib/apt/lists/*

RUN gem install fpm --no-ri --no-rdoc

RUN gem install pleaserun

WORKDIR /build

VOLUME [ "/app", "/build", "/certs", "/out" ]

ENV VERSION "0.0.0"