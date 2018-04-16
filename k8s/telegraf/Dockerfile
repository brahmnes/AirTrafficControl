FROM alpine:3.6

RUN echo 'hosts: files dns' >> /etc/nsswitch.conf
RUN apk add --no-cache iputils ca-certificates net-snmp-tools procps && \
    update-ca-certificates
    
ENV TELEGRAF_VERSION 1.5.3
COPY ./telegraf /usr/bin

EXPOSE 8125/udp 8092/udp 8094

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /usr/bin/telegraf && \
    chmod +x /entrypoint.sh
    
ENTRYPOINT ["/entrypoint.sh"]
CMD ["telegraf"]
