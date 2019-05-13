FROM ubuntu:bionic

RUN apt-get update && apt-get install -y gnupg1 wget

RUN echo "deb http://repo.aptly.info/ squeeze main" > /etc/apt/sources.list.d/aptly.list
RUN wget -qO - https://www.aptly.info/pubkey.txt | apt-key add -

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

VOLUME [ "/app", "/certs", "/out" ]

ENV VERSION "0.0.0"
ENV AWS_ACCESS_KEY_ID="XXXXXXXXXXXXXXXXXXX"
ENV AWS_SECRET_ACCESS_KEY="YYYYYYYYYYYYYYYYYYYY"
ENV GPG_PASSPHRASE="Password01!"
ENV GPG_PRIVATEKEYFILE="octopus-privkey.asc"