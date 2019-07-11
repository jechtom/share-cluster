# UDP Discovery

## Description

Provides discovery of local peers using UDP broadcasts.

## Use-case

Expected behavior is to be able to find other nodes running on LAN and to announce shutdown to other nodes.

## Listener Module

For every active [active interface](interfaces.md) listener modules listens for announce messages over UDP protocol.

When message is received then:

* Check if remote client version is compatible; if not, message is ignored
* [Peer registry module](peers-registry) is notified

## Announcer Module

For every [active interface](interfaces.md) announcer module sends UDP broadcast when:

* New active interface has been identified
* And then every 2 minutes
* On application exit - with shut-down flag set 

Also these rules applies:

* Minimum interval between announcments is 5 seconds (does not apply for shut-down message)

## Message

Message contains:

* Version of the client
* TCP communication port
* Current version of catalog
* [Instance ID](instance-id.md)
* Shut-down flag
