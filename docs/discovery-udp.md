# UDP Discovery

## Description

Provides discovery of local peers using UDP broadcasts.

## Listener Module

For every active [active interface](interfaces.md) listener modules listens for announce messages over UDP protocol.

When message is received then:

* Check if remote client version is compatible; if not, message is ignored
* [Peer registry module](peers-registry) is notified

## Announcer Module

For every [active interface](interfaces.md) announcer module sends UDP broadcast when:

* New active interface has been identified
* And then every 5 minutes

## Message

Message contains:

* Version of the client
* TCP communication port
* Hash identification of the client