FROM mcr.microsoft.com/dotnet/core/runtime:2.2-bionic

RUN apt-get update && apt-get install -y \
  ruby-dev \
  gcc \
  make \
  ruby \
  && rm -rf /var/lib/apt/lists/*

RUN gem install fpm -v 1.11.0 --no-ri --no-rdoc

RUN gem install pleaserun

COPY . /build
WORKDIR /build
VOLUME [ "/app" ]

EXPOSE 10933