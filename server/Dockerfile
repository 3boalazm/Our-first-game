# Builds the upstream Rust game server (WallerTheDeveloper/rust-server, branch
# game/paperio) pinned to a known commit, so no upstream source is copied here.
FROM rust:1-bookworm AS build
RUN apt-get update && apt-get install -y --no-install-recommends protobuf-compiler git \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
RUN git clone https://github.com/WallerTheDeveloper/rust-server.git . \
    && git checkout fb7c1aea22cb42fb8dbd8f5e2d08fe98cc3a11b7
RUN cargo build --release --bin paperio_server

FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /src/target/release/paperio_server /usr/local/bin/paperio_server
# The server listens for WebSocket clients on 9001 (and UDP on 9000, unused for WebGL).
EXPOSE 9001
CMD ["paperio_server"]
