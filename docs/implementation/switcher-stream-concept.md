
# Switcher Stream TODO DRAFT

## Data File Splitting
```text
Input Stream
|
V
Switch Controller
|      |      |
File   File   File
```

## Hashing - HashStreamComputeBehavior
```text
[Input Stream]
|
V
[HashStreamController with HashStreamComputeBehavior]
|                |
V                V
[Output Stream]  [Hashes Id[]]
```

## Hashing - HashStreamVerifyBehavior
```text
[Input Stream]   [Package Definition]   [Parts to Verify]
|                |                      |
V                V                      V
[HashStreamController with HashStreamVerifyBehavior]
|                |
V                V
[Output Stream]  [exception if verification failed]
```

# Flows

## Create New Package Flow

```text
     string                      stream
[path]
|
| string
V
[FolderStreamSerializer]
|
| stream
V
[HashStreamController with HashStreamComputeBehavior]
|                                   |
| stream                            | Id[]
V                                   |
[CreatePackageFolderController]     |
|                                   |
| file streams                      |
V                                   V
[data files]                        [package definition]
```

* Folder is serialized as stream
* Stream is splitted to segments, hashes are computed for segments
* Stream is splitted to data files