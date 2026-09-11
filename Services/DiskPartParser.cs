using JollyDiskPart.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace JollyDiskPart.Services
{
    public class DiskPartParser
    {
        private sealed class ParsedPartition
        {
            public PartitionInfo Partition { get; init; } = null!;
            public long OffsetBytes { get; init; }
        }
        
        public List<DiskInfo> ParseDisks(string output)
        {
            var disks = new List<DiskInfo>();

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (!line.TrimStart().StartsWith("Disk "))
                    continue;

                var match = Regex.Match(line, @"Disk\s+(\d+)\s+(\w+)\s+([\d\.]+)\s*(GB|TB|MB|KB|B)?", RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                double size = 0;

                if (double.TryParse(match.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                {
                    var unit = match.Groups[4].Value.ToUpperInvariant();

                    size = unit switch
                    {
                        "TB" => value * 1024 * 1024 * 1024 * 1024,
                        "GB" => value * 1024 * 1024 * 1024,
                        "MB" => value * 1024 * 1024,
                        "KB" => value * 1024,
                        _ => value
                    };
                }

                var isGpt = line.TrimEnd().EndsWith("*");

                disks.Add(new DiskInfo
                {
                    Number = int.Parse(match.Groups[1].Value),
                    Status = match.Groups[2].Value,
                    IsOnline = match.Groups[2].Value.Equals("Online", StringComparison.OrdinalIgnoreCase),
                    SizeBytes = (long)size,
                    PartitionStyle = isGpt ? PartitionStyle.GPT : PartitionStyle.RAW
                    //Partitions = new ObservableCollection<PartitionInfo>()
                });
            }

            return disks;
        }

        public List<PartitionInfo> ParsePartitions(string output)
        {
            var partitions = new List<PartitionInfo>();

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();               
                
                if (!Regex.IsMatch(trimmed, @"^Partition\s+\d+\s+"))
                    continue;
                
                var match = Regex.Match(trimmed, @"^Partition\s+(\d+)\s+(.+?)\s+(\d+(?:\.\d+)?)\s*(KB|MB|GB|TB)\s+(\d+(?:\.\d+)?)\s*(KB|MB|GB|TB)$", RegexOptions.IgnoreCase);

                if (!match.Success)
                    continue;

                int number = int.Parse(match.Groups[1].Value);
                string type = match.Groups[2].Value.Trim();
                double size = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                string unit = match.Groups[4].Value;
                double offset = double.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
                string offsetUnit = match.Groups[6].Value;
                long sizeBytes = ConvertToBytes(size, unit);
                long offsetBytes = ConvertToBytes(offset, offsetUnit);

                partitions.Add(new PartitionInfo
                {
                    Number = number,
                    Type = type,
                    Size = $"{size:0.##} {unit}",
                    SizeBytes = sizeBytes,
                    //OffsetBytes = offsetBytes,
                    Name = $"Partition {number}",
                    Icon = "▣",
                    Status = "Healthy"
                });
            }

            return partitions;
        }

        public Dictionary<int, List<PartitionInfo>> ParseAllPartitions(string output)
        {
            var result = new Dictionary<int, List<PartitionInfo>>();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            int? currentDisk = null;
            var currentLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // DiskPart outputs a line such as:
                //
                // Disk 0
                //
                // when the disk is selected.
                var diskMatch = Regex.Match(trimmed, @"^Disk\s+(\d+)\s*$", RegexOptions.IgnoreCase);
                if (diskMatch.Success)
                {
                    // Save the previous disk's partition output.
                    if (currentDisk.HasValue)
                    {
                        result[currentDisk.Value] = ParsePartitions(string.Join(Environment.NewLine, currentLines));
                    }

                    currentDisk = int.Parse(diskMatch.Groups[1].Value);
                    currentLines.Clear();
                    continue;
                }

                if (currentDisk.HasValue)
                {
                    currentLines.Add(line);
                }
            }

            // Save the final disk.
            if (currentDisk.HasValue)
            {
                result[currentDisk.Value] = ParsePartitions(string.Join(Environment.NewLine, currentLines));
            }

            return result;
        }

        private long ConvertToBytes(double size, string unit)
        {
            return unit.ToUpperInvariant() switch
            {
                "KB" => (long)(size * 1024),
                "MB" => (long)(size * 1024 * 1024),
                "GB" => (long)(size * 1024 * 1024 * 1024),
                "TB" => (long)(size * 1024 * 1024 * 1024 * 1024),
                _ => (long)size
            };
        }

        public VolumeInfo ParseVolumeDetail(string output)
        {
            var volume = new VolumeInfo();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var text = line.Trim();
                if (text.StartsWith("Volume Capacity", StringComparison.OrdinalIgnoreCase))
                {
                    volume.SizeBytes = ParseSize(GetValue(text));
                }
                else if (text.StartsWith("Volume Free Space", StringComparison.OrdinalIgnoreCase))
                {
                    volume.FreeBytes = ParseSize(GetValue(text));
                }
            }

            return volume;
        }

        private static string GetValue(string line)
        {
            int index = line.IndexOf(':');

            if (index < 0)
                return string.Empty;

            return line[(index + 1)..].Trim();
        }

        private static long ParseSize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            text = text.Trim().ToUpperInvariant();
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return 0;

            if (!double.TryParse(parts[0], out double value))
                return 0;

            return parts[1] switch
            {
                "KB" => (long)(value * 1024),
                "MB" => (long)(value * 1024 * 1024),
                "GB" => (long)(value * 1024 * 1024 * 1024),
                "TB" => (long)(value * 1024L * 1024 * 1024 * 1024),

                _ => 0
            };
        }

        public DiskInfo ParseDiskDetails(string output)
        {
            var disk = new DiskInfo();

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("Read-only"))
                {
                    disk.ReadOnly = line.Contains("Yes");
                }

                if (line.Contains("Current Read-only"))
                {
                    disk.CurrentReadOnly = line.Contains("Yes");
                }

                if (line.Contains("Boot Disk"))
                {
                    disk.BootDisk = line.Contains("Yes");
                }

                if (line.Contains("Pagefile Disk"))
                {
                    disk.PageFileDisk = line.Contains("Yes");
                }

                if (line.Contains("Crashdump Disk"))
                {
                    disk.CrashDumpDisk = line.Contains("Yes");
                }            
            }

            return disk;
        }

        public List<VolumeInfo> ParseVolumeList(string output)
        {
            var volumes = new List<VolumeInfo>();

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            int headerIndex = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Volume ###", StringComparison.OrdinalIgnoreCase))
                {
                    headerIndex = i;
                    break;
                }
            }

            if (headerIndex == -1 || headerIndex + 2 >= lines.Length)
                return volumes;

            string header = lines[headerIndex];

            int volStart = header.IndexOf("Volume ###", StringComparison.Ordinal);
            int ltrStart = header.IndexOf("Ltr", StringComparison.Ordinal);
            int labelStart = header.IndexOf("Label", StringComparison.Ordinal);
            int fsStart = header.IndexOf("Fs", StringComparison.Ordinal);
            int typeStart = header.IndexOf("Type", StringComparison.Ordinal);
            int sizeStart = header.IndexOf("Size", StringComparison.Ordinal);
            int statusStart = header.IndexOf("Status", StringComparison.Ordinal);
            int infoStart = header.IndexOf("Info", StringComparison.Ordinal);

            string Slice(string line, int start, int end)
            {
                if (start < 0 || start >= line.Length)
                    return "";

                if (end < 0)
                    end = line.Length;

                if (end > line.Length)
                    end = line.Length;

                if (end <= start)
                    return "";

                return line.Substring(start, end - start).Trim();
            }

            for (int i = headerIndex + 2; i < lines.Length; i++)
            {
                string line = lines[i];

                if (!line.TrimStart().StartsWith("Volume", StringComparison.OrdinalIgnoreCase))
                    continue;

                string volumeField = Slice(line, volStart, ltrStart);

                string numberText = volumeField.Replace("Volume", "", StringComparison.OrdinalIgnoreCase).Trim();

                if (!int.TryParse(numberText, out int number))
                    continue;

                var volume = new VolumeInfo
                {
                    Number = number,
                    DriveLetter = Slice(line, ltrStart, labelStart),
                    Label = Slice(line, labelStart, fsStart),
                    FileSystem = Slice(line, fsStart, typeStart),
                    Type = Slice(line, typeStart, sizeStart),
                    Status = Slice(line, statusStart, infoStart),
                    Info = Slice(line, infoStart, -1)
                };

                string sizeText = Slice(line, sizeStart, statusStart);

                if (!string.IsNullOrWhiteSpace(sizeText))
                    volume.SizeBytes = ParseSize(sizeText);

                volumes.Add(volume);
            }

            return volumes;
        }
        

        private static bool IsFileSystem(string value)
        {
            return value.Equals("NTFS", StringComparison.OrdinalIgnoreCase)
                || value.Equals("FAT32", StringComparison.OrdinalIgnoreCase)
                || value.Equals("FAT", StringComparison.OrdinalIgnoreCase)
                || value.Equals("exFAT", StringComparison.OrdinalIgnoreCase)
                || value.Equals("RAW", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSizeUnit(string value)
        {
            return value.Equals("KB", StringComparison.OrdinalIgnoreCase)
                || value.Equals("MB", StringComparison.OrdinalIgnoreCase)
                || value.Equals("GB", StringComparison.OrdinalIgnoreCase)
                || value.Equals("TB", StringComparison.OrdinalIgnoreCase);
        }

        public Dictionary<int, int?> ParsePartitionVolumeMap(string output)
        {
            var result = new Dictionary<int, int?>();
            int currentPartition = -1;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                //
                // Detect:
                // Partition 1
                //
                if (line.StartsWith("Partition ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int partitionNumber))
                    {
                        currentPartition = partitionNumber;
                        if (!result.ContainsKey(currentPartition))
                            result[currentPartition] = null;
                    }

                    continue;
                }

                //
                // Detect:
                // * Volume 8     D   1TB ...
                //
                if (currentPartition > 0 && line.Contains("Volume ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Equals("Volume", StringComparison.OrdinalIgnoreCase))
                        {
                            if (i + 1 < parts.Length && int.TryParse(parts[i + 1], out int volumeNumber))
                            {
                                result[currentPartition] = volumeNumber;
                            }

                            break;
                        }
                    }
                }
            }

            return result;
        }

        public string ParsePartitionTypeGuid(string output)
        {
            var match = Regex.Match(output, @"Type\s*:\s*([0-9a-fA-F\-]{36})");

            if (match.Success)
                return match.Groups[1].Value;

            return string.Empty;
        }

        public long ParsePartitionDetails(string output, PartitionInfo partition)
        {
            if (partition == null || string.IsNullOrWhiteSpace(output))
                return 0;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            long offsetBytes = 0;

            foreach (var raw in lines)
            {
                var line = raw.Trim();

                if (line.StartsWith("Type", StringComparison.OrdinalIgnoreCase))
                {
                    SetValue(line, value =>
                    {
                        partition.TypeId = value;
                        partition.PartitionType = GetPartitionType(value);
                    });
                }
                else if (line.StartsWith("Partition Size", StringComparison.OrdinalIgnoreCase))
                {
                    int index = line.IndexOf(':');

                    if (index > 0)
                    {
                        partition.SizeBytes = ParseSize(line[(index + 1)..].Trim());
                    }
                }
                else if (line.StartsWith("Offset in Bytes", StringComparison.OrdinalIgnoreCase))
                {
                    SetLongValue(line, value =>
                    {
                        offsetBytes = value;
                    });
                }
                else if (line.StartsWith("Hidden", StringComparison.OrdinalIgnoreCase))
                {
                    partition.Hidden = GetYesNo(line);
                }
                else if (line.StartsWith("Required", StringComparison.OrdinalIgnoreCase))
                {
                    partition.Required = GetYesNo(line);
                }
                else if (line.StartsWith("Read-only", StringComparison.OrdinalIgnoreCase))
                {
                    partition.ReadOnly = GetYesNo(line);
                }
                else if (line.StartsWith("* Volume", StringComparison.OrdinalIgnoreCase))
                {
                    ParseVolumeLine(line, partition);
                }
            }

            return offsetBytes;
        }

        private void SetValue(string line, Action<string> action)
        {
            int index = line.IndexOf(':');

            if (index > 0)
            {
                action(line[(index + 1)..].Trim());
            }
        }

        private bool GetYesNo(string line)
        {
            return line.EndsWith("Yes", StringComparison.OrdinalIgnoreCase);
        }

        private void SetLongValue(string line, Action<long> action)
        {
            int index = line.IndexOf(':');

            if (index > 0 &&
                long.TryParse(line[(index + 1)..].Trim(),
                out long value))
            {
                action(value);
            }
        }

        private void ParseVolumeLine(string line, PartitionInfo partition)
        {
            // Remove "* Volume"
            line = line.TrimStart('*').Trim();

            var parts = Regex.Split(line, @"\s{2,}").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            /*
               Example:
               Volume 2
               C
               System
               NTFS
               Partition
               1862 GB
               Healthy
            */

            if (parts.Length < 5)
                return;


            if (int.TryParse(Regex.Match(parts[0], @"Volume\s+(\d+)").Groups[1].Value, out int volume))
            {
                partition.VolumeNumber = volume;
            }


            int index = 1;

            // Drive letter
            if (parts[index].Length == 1 && char.IsLetter(parts[index][0]))
            {
                partition.DriveLetter = parts[index];
                index++;
            }

            // Label
            if (index < parts.Length)
            {
                partition.Label = parts[index];
                index++;
            }

            // Filesystem
            if (index < parts.Length)
            {
                partition.FileSystem = parts[index];
            }
        }

        public static long ParseMaximumShrink(string output)
        {
            foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(line, @"maximum.*?(\d+)\s*MB", RegexOptions.IgnoreCase); 
                if (match.Success)
                    return long.Parse(match.Groups[1].Value);
            }

            return 0;
        }

        private static PartitionStyle GetPartitionType(string typeId)
        {
            return typeId.Trim().ToLowerInvariant() switch
            {
                "c12a7328-f81f-11d2-ba4b-00a0c93ec93b" => PartitionStyle.EFI,
                "e3c9e316-0b5c-4db8-817d-f92df00215ae" => PartitionStyle.MSR,
                "de94bba4-06d1-4d40-a16a-bfd50179d6ac" => PartitionStyle.Recovery,
                "ebd0a0a2-b9e5-4433-87c0-68b6b72699c7" => PartitionStyle.Basic,
                "07" => PartitionStyle.Basic,
                "27" => PartitionStyle.Recovery,
                "42" => PartitionStyle.Dynamic,
                "ee" => PartitionStyle.GPT,
                _ => PartitionStyle.Unknown
            };
        }
    }
}