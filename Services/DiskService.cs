using JollyDiskPart.Builders;
using JollyDiskPart.Models;
using JollyDiskPart.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Media;

namespace JollyDiskPart.Services
{
    public sealed class PartitionLayoutItem
    {
        public PartitionInfo Partition { get; init; } = null!;
        public long OffsetBytes { get; init; }
    }

    public class DiskService
    {
        private readonly DiskPartService _diskPartService;
        private readonly DiskPartParser _parser;
        private readonly PartitionCorrelator _correlator;
        private readonly DiskHardwareInfoService _hardware;

        private readonly ConcurrentDictionary<int, Dictionary<int, int?>> _partitionVolumeCache = new();

        public DiskService()
        {
            _diskPartService = new DiskPartService();
            _parser = new DiskPartParser();
            _correlator = new PartitionCorrelator();
            _hardware = new DiskHardwareInfoService();
        }

        public async Task<List<DiskInfo>> ScanAsync()
        {
            var disks = await GetDisksAsync();
            var volumes = await GetVolumesAsync();
            var hardware = _hardware.GetHardwareInfo().ToDictionary(h => h.Number);
            var allGeometry = await GetAllPartitionGeometryAsync(disks);

            foreach (var disk in disks)
            {
                if (hardware.TryGetValue(disk.Number, out var info))
                {
                    disk.Name = $"Disk {info.Number}";
                    disk.Model = info.Model;
                    disk.Manufacturer = info.Manufacturer;
                    disk.SerialNumber = info.SerialNumber;
                    disk.InterfaceType = info.InterfaceType;
                    disk.MediaType = info.MediaType;

                    if (info.SizeBytes > 0)
                    {
                        disk.SizeBytes = info.SizeBytes;
                    }

                    if (disk.Size == 0 && info.Size > 0)
                    {
                        // DiskPart didn't provide a size, use hardware size
                        disk.Size = info.Size;
                        disk.SizeGB = $"{info.Size:0.##} GB";
                        disk.IsUninitialized = false;
                    }
                    else if (disk.Size == 0)
                    {
                        // No size from either source
                        disk.SizeGB = "Uninitialised";
                        disk.IsUninitialized = true;
                    }
                    else
                    {
                        // DiskPart already provided a valid size
                        disk.SizeGB = $"{disk.Size:0.##} GB";
                        disk.IsUninitialized = false;
                    }

                    disk.Icon = disk.MediaType switch
                    {
                        MediaType.SSD => IconHelper.Load("Resources/Icons/disk-ssd.png"),
                        MediaType.NVMe => IconHelper.Load("Resources/Icons/nvme.png"),
                        MediaType.USB => IconHelper.Load("Resources/Icons/disk-usb.png"),
                        MediaType.Optical => IconHelper.Load("Resources/Icons/dvd.png"),
                        MediaType.SDCard => IconHelper.Load("Resources/Icons/sdcard.png"),
                        _ => IconHelper.Load("Resources/Icons/disk-hdd.png")
                    };
                }

                var processed = await GetPartitionsAsync(disk.Number, disk.SizeBytes);

                if (disk.PartitionStyle == PartitionStyle.RAW && processed.Count > 0)
                {
                    disk.PartitionStyle = PartitionStyle.MBR;
                }

                var merged = _correlator.Merge(processed, volumes);

                CalculatePartitionWidths(disk, merged);
                ApplyPartitionColours(merged);

                disk.Partitions.ReplaceAll(merged);
            }
            
            return disks;
        }      

        private async Task BuildPartitionCacheAsync(int diskNumber, IList<PartitionInfo> partitions)
        {
            if (_partitionVolumeCache.TryGetValue(diskNumber, out var cache))
            {
                foreach (var p in partitions)
                {
                    if (cache.TryGetValue(p.Number, out var vol))
                        p.VolumeNumber = vol;
                }

                return;
            }

            var realPartitions = partitions.Where(p => !p.IsUnallocated && p.Number > 0).ToList();

            if (realPartitions.Count == 0)
                return;

            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber);

            foreach (var partition in realPartitions)
            {
                builder.SelectPartition(partition.Number).DetailPartition();
            }

            var result = await _diskPartService.ExecuteBuilderAsync(builder);

            var map = _parser.ParsePartitionVolumeMap(result.Output);

            _partitionVolumeCache[diskNumber] = map;

            foreach (var p in partitions)
            {
                if (map.TryGetValue(p.Number, out var vol))
                    p.VolumeNumber = vol;
            }
        }

        public async Task<List<PartitionInfo>> GetMergedPartitionsAsync(DiskInfo disk)
        {
            var partitions = await GetPartitionsAsync(disk.Number, disk.SizeBytes);
            await BuildPartitionCacheAsync(disk.Number, partitions);
            var volumes = await GetVolumesAsync();
            return _correlator.Merge(partitions, volumes);
        }

        public async Task<List<DiskInfo>> GetDisksAsync()
        {
            var result = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().Add("list disk"));
            var disks = _parser.ParseDisks(result.Output);
            return disks;
        }

        public async Task<List<DiskLayoutItem>> GetDiskLayoutAsync(int diskNumber, long diskSizeBytes)
        {
            var result = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).ListPartitions().Exit());
            var partitions = _parser.ParsePartitions(result.Output);

            // Get the physical offsets of the real partitions.
            var layout = await PopulatePartitionDetailsAsync(diskNumber, partitions);

            layout = layout.OrderBy(p => p.OffsetBytes).ToList();

            // InsertUnallocatedPartitions gives us the same PartitionInfo
            // objects that the normal disk view uses.
            var resultPartitions = InsertUnallocatedPartitions(layout, diskSizeBytes);
            var resultLayout = new List<DiskLayoutItem>();

            foreach (var partition in resultPartitions)
            {
                if (partition.IsUnallocated)
                {
                    // InsertUnallocatedPartitions stores the physical
                    // position here.
                    resultLayout.Add(new DiskLayoutItem
                    {
                        DiskNumber = diskNumber,
                        Partition = partition,
                        OffsetBytes = partition.UnallocatedOffsetBytes
                    });

                    continue;
                }

                // Find the corresponding real partition in the physical layout.
                var physical = layout.FirstOrDefault(p => p.Partition.Number == partition.Number);

                if (physical == null)
                    continue;

                resultLayout.Add(new DiskLayoutItem
                {
                    DiskNumber = diskNumber,
                    Partition = partition,
                    OffsetBytes = physical.OffsetBytes
                });
            }

            return resultLayout.OrderBy(p => p.OffsetBytes).ToList();
        }

        public async Task<List<PartitionInfo>> GetPartitionsAsync(int diskNumber, long diskSizeBytes)
        {
            var result = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).ListPartitions().Exit());
            var partitions = _parser.ParsePartitions(result.Output);
            var layout = await PopulatePartitionDetailsAsync(diskNumber, partitions);

            layout = layout.OrderBy(p => p.OffsetBytes).ToList();

            var resultPartitions = InsertUnallocatedPartitions(layout, diskSizeBytes);

            for (int i = 0; i < resultPartitions.Count - 1; i++)
            {
                var partition = resultPartitions[i];
                partition.CanExtend = !partition.IsUnallocated && !partition.IsProtected && resultPartitions[i + 1].IsUnallocated;
                partition.CanMove = i > 0 && !partition.IsUnallocated && !partition.IsProtected && resultPartitions[i - 1].IsUnallocated;
            }

            return resultPartitions;
        }

        private async Task<List<PartitionLayoutItem>> PopulatePartitionDetailsAsync(int diskNumber, List<PartitionInfo> partitions)
        {
            await BuildPartitionCacheAsync(diskNumber, partitions);
            var result = new List<PartitionLayoutItem>();
            var geometries = await GetPartitionGeometryAsync(diskNumber);
            var geometryMap = geometries.ToDictionary(x => x.PartitionNumber);

            foreach (var partition in partitions)
            {
                if (geometryMap.TryGetValue(partition.Number, out var geometry))
                {
                    partition.SizeBytes = geometry.SizeBytes;
                    partition.Size = FormatSize(geometry.SizeBytes);
                    result.Add(new PartitionLayoutItem
                    {
                        Partition = partition,
                        OffsetBytes = geometry.OffsetBytes
                    });
                }
                else
                {
                    result.Add(new PartitionLayoutItem
                    {
                        Partition = partition,
                        OffsetBytes = 0
                    });
                }
            }

            return result;
        }

        public static List<PartitionInfo> InsertUnallocatedPartitions(IEnumerable<PartitionLayoutItem> partitions, long diskSizeBytes)
        {
            const long minimumUnallocatedBytes = 4L * 1024 * 1024;
            var ordered = partitions.OrderBy(p => p.OffsetBytes).ToList();
            var result = new List<PartitionInfo>();
            long currentOffset = 0;

            foreach (var item in ordered)
            {
                var partition = item.Partition;
                long partitionOffset = item.OffsetBytes;

                // Gap before this partition.
                long gap = partitionOffset - currentOffset;

                if (gap >= minimumUnallocatedBytes)
                {
                    result.Add(new PartitionInfo
                    {
                        Name = "Unallocated",
                        Type = "Free Space",
                        PartitionType = PartitionStyle.Unallocated,
                        IsUnallocated = true,
                        SizeBytes = gap,
                        Size = FormatSize(gap),
                        UnallocatedOffsetBytes = currentOffset
                    });
                }               
                
                result.Add(partition);

                // The partition's actual physical end.
                currentOffset = partitionOffset + partition.SizeBytes;
            }

            // Space after the final partition.
            long remaining = diskSizeBytes - currentOffset;

            if (remaining >= minimumUnallocatedBytes)
            {
                result.Add(new PartitionInfo
                {
                    Name = "Unallocated",
                    Type = "Free Space",
                    PartitionType = PartitionStyle.Unallocated,
                    IsUnallocated = true,
                    SizeBytes = remaining,
                    Size = FormatSize(remaining),
                    UnallocatedOffsetBytes = currentOffset
                });
            }

            return result;
        }

        private static string FormatSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;

            if (bytes >= TB)
                return $"{bytes / (double)TB:0.##} TB";

            if (bytes >= GB)
                return $"{bytes / (double)GB:0.##} GB";

            if (bytes >= MB)
                return $"{bytes / (double)MB:0.##} MB";

            return $"{bytes / (double)KB:0.##} KB";
        }

        public async Task<List<VolumeInfo>> GetVolumesAsync()
        {
            var volumes = new List<VolumeInfo>();
            var listResult = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().Add("list volume"));

            var volumeList = _parser.ParseVolumeList(listResult.Output);

            foreach (var volume in volumeList)
            {
                var detailResult = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectVolume(volume.Number).Add("detail volume"));
                var detailed = _parser.ParseVolumeDetail(detailResult.Output);

                detailed.Number = volume.Number;
                detailed.DriveLetter = volume.DriveLetter;
                detailed.Label = volume.Label;
                detailed.FileSystem = volume.FileSystem;
                detailed.Status = volume.Status;

                if (detailed.SizeBytes == 0)
                    detailed.SizeBytes = volume.SizeBytes;

                volumes.Add(detailed);
            }

            return volumes;
        }

        public async Task<long> GetPartitionDetailsAsync(int diskNumber, PartitionInfo partition)
        {
            var result = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partition.Number).Add("detail partition"));
            return _parser.ParsePartitionDetails(result.Output, partition);
        }

        public async Task<DiskInfo> GetDiskDetailsAsync(int diskNumber)
        {
            var result = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).Add("detail disk"));
            var disk = _parser.ParseDiskDetails(result.Output);
            disk.Number = diskNumber;
            return disk;
        }

        public async Task<DiskOperationResult> CleanDiskAsync(int diskNumber)
        {
            return await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).Add("clean"));
        }

        public async Task<DiskOperationResult> CreatePrimaryPartitionAsync(int diskNumber)
        {
            return await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).Add("create partition primary"));
        }

        public async Task<DiskOperationResult> FormatPartitionAsync(int partitionNumber, string filesystem = "ntfs")
        {
            return await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectPartition(partitionNumber).Add($"format fs={filesystem} quick"));
        }

        public void CalculatePartitionWidths(DiskInfo disk, IList<PartitionInfo> partitions)
        {
            const double totalWidth = 900;
            const double minWidth = 25;
            const double exponent = 0.15;    // <- tweak this

            if (disk.SizeBytes <= 0)
                return;

            var weights = new Dictionary<PartitionInfo, double>();
            double totalWeight = 0;

            foreach (var p in partitions)
            {
                double fraction = (double)p.SizeBytes / disk.SizeBytes;

                // Exaggerate small partitions
                double weight = Math.Pow(fraction, exponent);

                weights[p] = weight;
                totalWeight += weight;
            }

            foreach (var p in partitions)
            {
                p.DisplayWidth = Math.Max(minWidth, totalWidth * weights[p] / totalWeight);
            }

            // Normalize to exactly total width
            double used = partitions.Sum(p => p.DisplayWidth);
            double scale = totalWidth / used;

            foreach (var p in partitions)
                p.DisplayWidth *= scale;

            // Fix rounding
            var largest = partitions.OrderByDescending(p => p.DisplayWidth).First();
            largest.DisplayWidth += totalWidth - partitions.Sum(p => p.DisplayWidth);
        }

        public void ApplyPartitionColours(IList<PartitionInfo> partitions)
        {
            foreach (var partition in partitions)
            {
                partition.Colour = partition.FileSystem switch
                {
                    "NTFS" => Brushes.SteelBlue,
                    "FAT32" => Brushes.DarkOrange,
                    "exFAT" => Brushes.MediumPurple,
                    _ => Brushes.Gray
                };
            }
        }

        public async Task<CreatePartitionResult> CreatePartitionAsync(int diskNumber, long sizeMB, long offsetKB, string fileSystem = "NTFS", string? label = null, char? driveLetter = null, bool quickFormat = true, bool useMaximum = false)
        {
            DiskOperationResult? createResult = null;

            if (useMaximum)
            {
                createResult = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).CreatePrimaryPartition().Exit());
            }
            else
            {
                createResult = await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).CreatePrimaryPartition(sizeMB, offsetKB).Exit());
            }

            if (!createResult.Success)
            {
                return new CreatePartitionResult
                {
                    Error = createResult.Error ?? "Failed to create partition",
                    Output = createResult.Output
                };
            }

            var disks = await GetDisksAsync();
            var disk = disks.FirstOrDefault(d => d.Number == diskNumber);

            if (disk == null)
            {
                return new CreatePartitionResult
                {
                    Error = $"Disk {diskNumber} could not be found after partition creation",
                    Output = createResult.Output
                };
            }

            var partitions = await GetPartitionsAsync(diskNumber, disk.SizeBytes);
            var newPartition = partitions.OrderByDescending(p => p.Number).FirstOrDefault();

            if (newPartition == null)
            {
                return new CreatePartitionResult
                {
                    Error = "Partition was created but could not be found",
                    Output = createResult.Output
                };
            }

            var configureBuilder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(newPartition.Number).Format(fileSystem, label, quickFormat);

            if (driveLetter.HasValue)
            {
                configureBuilder.AssignLetter(char.ToUpper(driveLetter.Value));
            }

            configureBuilder.Exit();

            var formatResult = await _diskPartService.ExecuteBuilderAsync(configureBuilder);

            if (!formatResult.Success)
            {
                return new CreatePartitionResult
                {
                    Partition = newPartition,
                    Error = formatResult.Error ?? "Partition created but configuration failed",
                    Output = formatResult.Output
                };
            }

            return new CreatePartitionResult
            {
                Partition = newPartition,
                Output = formatResult.Output
            };
        }

        public async Task<List<PartitionGeometry>> GetPartitionGeometryAsync(int diskNumber)
        {
            string script =
                $"Get-Partition -DiskNumber {diskNumber} " +
                "| Select-Object PartitionNumber,Offset,Size | ConvertTo-Json -Compress";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = psi
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return new List<PartitionGeometry>();

            try
            {
                using var document = JsonDocument.Parse(output);
                var root = document.RootElement;

                var result = new List<PartitionGeometry>();

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        result.Add(new PartitionGeometry
                        {
                            PartitionNumber = item.GetProperty("PartitionNumber").GetInt32(),
                            OffsetBytes = item.GetProperty("Offset").GetInt64(),
                            SizeBytes = item.GetProperty("Size").GetInt64()
                        });
                    }
                }
                else
                {
                    // Get-Partition returns a single object if there is only one partition.
                    result.Add(new PartitionGeometry
                    {
                        PartitionNumber = root.GetProperty("PartitionNumber").GetInt32(),
                        OffsetBytes = root.GetProperty("Offset").GetInt64(),
                        SizeBytes = root.GetProperty("Size").GetInt64()
                    });
                }

                return result;
            }
            catch
            {
                return new List<PartitionGeometry>();
            }
        }

        public async Task<Dictionary<int, List<PartitionGeometry>>> GetAllPartitionGeometryAsync(IList<DiskInfo> disks)
        {
            var diskNumbers = string.Join(",", disks.Select(d => d.Number));

            string script =
                $"Get-Partition -DiskNumber {diskNumbers} " +
                "| Select-Object DiskNumber,PartitionNumber,Offset,Size | " +
                "ConvertTo-Json -Compress";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = psi
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var result = new Dictionary<int, List<PartitionGeometry>>();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return result;

            try
            {
                using var document = JsonDocument.Parse(output);
                var root = document.RootElement;

                IEnumerable<JsonElement> items;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    items = root.EnumerateArray();
                }
                else
                {
                    items = new[] { root };
                }

                foreach (var item in items)
                {
                    int diskNumber = item.GetProperty("DiskNumber").GetInt32();

                    var geometry = new PartitionGeometry
                    {
                        PartitionNumber = item.GetProperty("PartitionNumber").GetInt32(),
                        OffsetBytes = item.GetProperty("Offset").GetInt64(),
                        SizeBytes = item.GetProperty("Size").GetInt64()
                    };

                    if (!result.TryGetValue(diskNumber, out var list))
                    {
                        list = new List<PartitionGeometry>();
                        result[diskNumber] = list;
                    }

                    list.Add(geometry);
                }
            }
            catch
            {
                return new Dictionary<int, List<PartitionGeometry>>();
            }

            return result;
        }

        public async Task<PartitionGeometry?> GetPartitionGeometryAsync(int diskNumber, int partitionNumber)
        {
            string script = $"Get-Partition -DiskNumber {diskNumber} -PartitionNumber {partitionNumber} " + "| Select-Object PartitionNumber,Offset,Size | ConvertTo-Json -Compress";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = psi
            };

            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(output);
                var root = document.RootElement;

                return new PartitionGeometry
                {
                    PartitionNumber = root.GetProperty("PartitionNumber").GetInt32(),
                    OffsetBytes = root.GetProperty("Offset").GetInt64(),
                    SizeBytes = root.GetProperty("Size").GetInt64()
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<DiskOperationResult> DeletePartitionAsync(int diskNumber, int partitionNumber)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partitionNumber).DeletePartition().Exit();
            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> FormatPartitionAsync(int diskNumber, int partitionNumber, string fileSystem, string label, bool quickFormat)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partitionNumber).Format(fileSystem, label, quickFormat).Exit();
            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> ExtendPartitionAsync(int diskNumber, int partitionNumber, long sizeMB)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partitionNumber).ExtendPartition(sizeMB).Exit();
            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> ShrinkPartitionAsync(int diskNumber, int partitionNumber, long sizeMB)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partitionNumber).ShrinkPartition(sizeMB).Exit();
            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<long> QueryMaximumShrinkAsync(int diskNumber, int partitionNumber)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partitionNumber).QueryMaximumShrink().Exit();
            var result = await _diskPartService.ExecuteBuilderAsync(builder);

            if (!result.Success)
                return 0;

            return DiskPartParser.ParseMaximumShrink(result.Output);
        }

        public async Task<DiskOperationResult> ChangeDriveLetterAsync(int diskNumber, int partitionNumber, string? currentLetter, char? driveLetter)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).SelectPartition(partitionNumber);

            if (!string.IsNullOrWhiteSpace(currentLetter))
                builder.RemoveDriveLetter();

            if (driveLetter.HasValue)
                builder.AssignDriveLetter(driveLetter.Value);

            builder.Exit();

            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> InitializeDiskAsync(int diskNumber, bool useGpt)
        {
            var disk = (await GetDisksAsync()).FirstOrDefault(d => d.Number == diskNumber);
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber);//.OnlineDisk();

            // Only bring the disk online if it is offline
            if (disk?.Status.Equals("Offline", StringComparison.OrdinalIgnoreCase) == true)
            {
                builder.OnlineDisk();
            }

            if (useGpt)
                builder.ConvertGpt();
            else
                builder.ConvertMbr();

            builder.Exit();

            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> CleanDiskAsync(int diskNumber, bool cleanAll)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).CleanDisk(cleanAll).Exit();
            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> ConvertDiskAsync(int diskNumber, bool gpt)
        {
            var builder = new DiskPartScriptBuilder().SelectDisk(diskNumber).ConvertDisk(gpt).Exit();
            return await _diskPartService.ExecuteBuilderAsync(builder);
        }

        public async Task<DiskOperationResult> TakeOfflineAsync(int diskNumber, CancellationToken cancellationToken = default)
        {
            return await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).OfflineDisk().Exit());
        }

        public async Task<DiskOperationResult> BringOnlineAsync(int diskNumber, CancellationToken cancellationToken = default)
        {
            return await _diskPartService.ExecuteBuilderAsync(new DiskPartScriptBuilder().SelectDisk(diskNumber).OnlineDisk().Exit());
        }
    }
}
