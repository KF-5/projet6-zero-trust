# Image commune des postes (client-01, client-03, admin, client-ext, dmz-pivot)
FROM debian:trixie-slim
RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      ca-certificates curl dnsutils iproute2 iputils-ping netcat-openbsd \
      postgresql-client iperf3 tcpdump python3 python3-requests procps \
 && rm -rf /var/lib/apt/lists/*
CMD ["sleep", "infinity"]
