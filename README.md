# JollyDiskPart

A modern WPF disk and partition management application for Windows, built with C# and .NET 10.

JollyDiskPart provides a graphical interface around Windows `DiskPart.exe`, allowing users to inspect disks and partitions and perform common partition-management operations without having to work directly from the command line.

> **Warning:** Disk and partition operations can cause permanent data loss. Use JollyDiskPart at your own risk and always maintain a current backup of important data.

## Features

### Disk Management

- Detect physical and virtual disks using Windows DiskPart.
- Display disk number, model, capacity and status.
- Identify common disk types:
  - HDD
  - SSD
  - USB/removable storage
  - SD cards
  - Virtual disks
  - Optical drives
- Detect GPT, MBR and RAW disks.
- Display online/offline state.
- Refresh disk information from the system.

### Partition Management

- Enumerate partitions on each disk.
- Display partition number, type, filesystem and size.
- Detect partition offsets and physical layout.
- Identify unallocated space between partitions.
- Display partitions graphically using a disk-map style interface.
- Track partition geometry independently from display information.
- Identify partitions that can potentially be extended or moved.

### Disk Map

JollyDiskPart includes a graphical disk layout similar to commercial partition-management applications.

The disk map represents:

- Existing partitions
- Unallocated space
- Partition sizes
- Partition ordering
- Partition boundaries
- Relative disk position

The layout is calculated from the actual byte offsets and sizes reported by Windows rather than relying solely on the textual representation returned by DiskPart.

### DiskPart Integration

JollyDiskPart uses a dedicated DiskPart service and script builder to execute DiskPart commands.

Typical operations are constructed through `DiskPartScriptBuilder`, keeping command generation separate from the UI and service layers.

Examples include:

```text
select disk
list partition
list volume
create partition
format
assign
delete partition
```

## Architecture

JollyDiskPart is built around a relatively simple MVVM architecture.

```text
┌───────────────────────────────┐
│           WPF UI              │
│                               │
│  Disk List / Disk Map / UI    │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│         ViewModels            │
│                               │
│       DiskViewModel           │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│           Services            │
│                               │
│ DiskService                   │
│ DiskPartService               │
│ PartitionMoveService          │
│ NtfsMoveService               │
│ PartitionSectorMover          │
└───────────────┬───────────────┘
                │
        ┌───────┴────────┐
        ▼                ▼
   DiskPart.exe      Windows APIs
```

The project deliberately avoids unnecessary abstraction and dependency-injection complexity. Services are kept focused on their specific responsibilities.

## Core Components

### `DiskPartService`

Responsible for executing DiskPart scripts and returning the resulting output.

### `DiskPartScriptBuilder`

Builds DiskPart command sequences programmatically.

This avoids scattering raw DiskPart command strings throughout the application.

### `DiskService`

Responsible for higher-level disk and partition operations, including:

- Enumerating disks
- Enumerating partitions
- Retrieving volume information
- Creating partitions
- Taking disks offline/online
- Populating partition geometry
- Correlating partition and volume information

### `DiskInfo`

Represents a physical or virtual disk.

Typical information includes:

- Disk number
- Model
- Disk ID
- Status
- Type
- Size
- Size in bytes
- Free space
- GPT/MBR state
- RAW state
- USB/removable state
- SSD/HDD state
- Optical state
- Partition collection

### `PartitionInfo`

Represents a partition discovered from DiskPart.

Information includes:

- Partition number
- Name
- Type
- Partition type ID
- Filesystem information
- Size
- Size in bytes
- Associated volume information

### `VolumeInfo`

Contains filesystem and volume-level information where Windows makes it available.

For NTFS volumes this can include information obtained through native Windows filesystem APIs.

### `PartitionMoveService`

Provides validation and orchestration for partition movement operations.

Before a move is attempted, the service validates conditions such as:

- Source partition
- Destination offset
- Available unallocated space
- Protected partitions
- Alignment
- Partition ordering

### `NtfsMoveService`

Provides NTFS-specific functionality required when preparing a partition for movement.

This includes querying filesystem information and NTFS volume allocation data.

### `PartitionSectorMover`

Handles low-level sector copying used by the partition movement engine.

The implementation operates directly against disk sectors and includes verification functionality to ensure copied data matches the source.

## Partition Movement

Partition movement is one of the more complex parts of JollyDiskPart.

Moving a partition is fundamentally different from resizing one.

A resize changes the boundaries of a partition. Moving a partition requires the actual data within the partition to be relocated to a different physical disk offset.

The current implementation therefore separates:

1. Move validation
2. Filesystem preparation
3. Sector movement
4. Verification
5. Partition-table updates

The low-level copy engine has been tested using raw copy-and-verify operations.

For example, a test move can copy a large range of sectors to a new location and subsequently read the destination data back to verify that it matches the source.

> Partition movement is an advanced operation and should be considered experimental until all filesystem and boot-critical scenarios have been thoroughly tested.

## Alignment

Disk operations are alignment-sensitive.

JollyDiskPart uses alignment calculations when determining valid partition boundaries and available space.

The application takes into account:

- Sector size
- Partition offsets
- Disk boundaries
- Alignment requirements
- Existing unallocated regions

This is particularly important because DiskPart may reject operations that appear to fit mathematically but do not satisfy the underlying storage alignment requirements.

## Unallocated Space

Unallocated space is represented internally as a layout item rather than relying solely on DiskPart to report it as a partition.

When partition geometry is known, JollyDiskPart calculates gaps between partitions and inserts corresponding unallocated regions.

Conceptually:

```text
Disk
│
├── Partition 1
│
├── Unallocated
│
├── Partition 2
│
└── Unallocated
```

This allows the disk map to represent the complete physical layout of the disk.

## Disk and Volume Correlation

DiskPart reports partitions and volumes through separate commands.

JollyDiskPart therefore correlates the information gathered from:

```text
list partition
```

and:

```text
list volume
```

Where possible, volume information is associated with the corresponding partition using physical characteristics such as size and disk geometry.

Filesystem information is treated as optional because not every partition has an accessible Windows volume.

When filesystem information cannot be obtained, the UI displays appropriate unknown or unavailable values rather than assuming the filesystem is empty or has zero values.

## Technology Stack

- **C#**
- **.NET 10**
- **WPF**
- **MVVM**
- **Windows DiskPart**
- **Windows Native APIs**
- **NTFS filesystem APIs**
- **Visual Studio**
- **Git**

## Requirements

JollyDiskPart is intended for modern Windows systems and requires administrative privileges for operations that modify disks or partitions.

Recommended environment:

- Windows 10/11
- .NET 10 Desktop Runtime
- Administrator privileges

Some functionality may depend on the capabilities and configuration of the underlying Windows storage stack.

For .NET 10, System.Management is not included automatically. Add the NuGet package.

In the JollyDiskPart project directory run:
    dotnet add package System.Management
    
## Building

Clone the repository:

```bash
git clone <repository-url>
```

Open the solution in Visual Studio and build the project.

Alternatively:

```bash
dotnet restore
dotnet build
```

Run the application with administrator privileges when performing operations that require elevated access.

## Development

The project is intentionally kept straightforward.

The main design goals are:

- Keep the UI responsive.
- Keep DiskPart interaction isolated.
- Keep disk parsing deterministic.
- Keep physical disk geometry separate from visual layout.
- Avoid unnecessary dependencies.
- Prefer native Windows functionality where appropriate.
- Make destructive operations explicit.
- Verify low-level operations wherever possible.

## Safety

JollyDiskPart interacts directly with physical storage devices.

Incorrect use of partition-management software can result in:

- Data loss
- Filesystem corruption
- Unbootable operating systems
- Loss of partition tables
- Permanent destruction of data

Always:

1. Back up important data.
2. Confirm the selected disk before performing an operation.
3. Ensure the correct partition is selected.
4. Do not interrupt disk operations.
5. Avoid performing experimental move operations on irreplaceable data.
6. Verify backups before testing destructive functionality.

## Project Status

JollyDiskPart is an active development project.

The disk discovery, parsing, layout and DiskPart integration functionality is being developed toward a complete graphical partition-management application.

Advanced partition movement functionality is still under development and testing.

Current development areas include:

- Partition movement
- NTFS filesystem preparation
- Raw sector movement
- Move verification
- Partition boundary updates
- Improved error handling
- UI refinement
- Additional filesystem support
- More comprehensive safety checks

## Philosophy

JollyDiskPart aims to provide the power of command-line disk management with a clean graphical interface.

The goal is not simply to wrap DiskPart in a window, but to build a proper visual representation of the underlying disk geometry while retaining direct access to Windows storage functionality.

The project favours:

> **Simple code, accurate disk geometry, explicit operations and thorough verification.**

## License

License information will be added when the project is published.

---

**JollyDiskPart** — graphical disk and partition management for Windows.
