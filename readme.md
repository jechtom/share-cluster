# Share Cluster Project

This is experimental tool for decentralized sharing of immutable data packages across local area network. It is based on BitTorrent architecture.

## Features

- Every client can create package and every client can download any package created by other clients

- No need for centralized tracker or package registry

- Similar to BitTorrent all clients having at least one part of package can be automatically used to offload traffic from initial seeder

- Client can be operated from command line and/or web interface

- Packages are stored as immutable compressed data files

- Every package is identified with hash and it is used to verify all received data   

- Clients can be discovered using automatic UDP broadcast discovery or automatic "peers known to known peer" discovery or manual entry.

### Drawbacks

- Designed to work only on local area network

- Designed to work best with <100 peers

## User Manual

*TBD - user interface is not ready yet*

## Why?

- Easy sharing of large files to consumers across local area network

- Experimental hobby project based on .NET Core 2