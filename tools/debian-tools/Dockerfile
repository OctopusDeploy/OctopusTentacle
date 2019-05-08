FROM ubuntu:bionic

RUN apt-get update && apt-get install -y gnupg1

RUN echo "deb http://repo.aptly.info/ squeeze main" > /etc/apt/sources.list.d/aptly.list
RUN apt-key adv --keyserver pool.sks-keyservers.net --recv-keys ED75B5A4483DA07C

RUN apt-get update && apt-get install -y \
  ruby-dev \
  gcc \
  make \
  ruby \
  gpgv1 \
  aptly \
  && rm -rf /var/lib/apt/lists/*

RUN gem install fpm --no-ri --no-rdoc

RUN gem install pleaserun

COPY settings/aptly.conf /root/.aptly.conf

COPY *.sh /build/
WORKDIR /build

VOLUME [ "/app", "/certs" ]

ENV VERSION "0.0.0"
ENV AWS_ACCESS_KEY_ID="YYYYYYYYYYYYYYYYYYYY"
ENV AWS_SECRET_ACCESS_KEY="XXXXXXXXXXXXXXXXXXX"