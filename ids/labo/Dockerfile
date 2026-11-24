FROM debian:stable-slim
RUN apt-get update && apt-get install -y --no-install-recommends nmap hping3 curl iproute2 \
    && rm -rf /var/lib/apt/lists/*
CMD ["sleep", "infinity"]
