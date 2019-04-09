FROM ubuntu:bionic

RUN apt-get update && apt-get install -y \
  ruby-dev \
  gcc \
  make \
  ruby \
  && rm -rf /var/lib/apt/lists/*

RUN gem install fpm --no-ri --no-rdoc

RUN gem install pleaserun

COPY . /build
WORKDIR /build
VOLUME [ "/app" ]

ENV VERSION "0.0.0"