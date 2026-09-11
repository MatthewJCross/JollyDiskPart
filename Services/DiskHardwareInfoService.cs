using System.Management;
using JollyDiskPart.Models;

namespace JollyDiskPart.Services
{
    public class DiskHardwareInfoService
    {
        public List<DiskInfo> GetHardwareInfo()
        {
            var disks = new List<DiskInfo>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

            foreach (ManagementObject? drive in searcher.Get())
            {
                if (drive == null)
                    continue;

                var disk = new DiskInfo();

                if (disk == null)
                    continue;

                disk.Model = string.IsNullOrWhiteSpace(drive?["Model"]?.ToString()) ? "Unknown Disk" : drive["Model"]?.ToString()?.Trim() ?? "Unknown Disk";
                disk.Manufacturer = string.IsNullOrWhiteSpace(drive?["Manufacturer"]?.ToString()) ? "" : drive["Manufacturer"]?.ToString()?.Trim() ?? "";
                disk.SerialNumber = string.IsNullOrWhiteSpace(drive?["SerialNumber"]?.ToString()) ? "" : drive["SerialNumber"]?.ToString()?.Trim() ?? "";
                disk?.InterfaceType = drive?["InterfaceType"].ToString() ?? "";
                if (drive != null)
                    disk?.MediaType = DetectMediaType(drive);
                
                if (long.TryParse(drive?["Size"]?.ToString(), out long size))
                {
                    // Exact physical disk size.
                    disk?.SizeBytes = size;

                    // Display size in GB.
                    disk?.Size = size / 1024d / 1024d / 1024d;
                }

                if (int.TryParse(drive?["Index"]?.ToString(), out int index))
                {
                    disk?.Number = index;
                }

                // Extra hardware information
                disk?.PnpDeviceId = drive?["PNPDeviceID"]?.ToString() ?? "";
                disk?.FirmwareRevision = drive?["FirmwareRevision"]?.ToString() ?? "";
                disk?.IsUsb = disk?.InterfaceType?.Equals("USB", StringComparison.OrdinalIgnoreCase) == true;
                disk?.IsNvme = disk.Model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) || disk.PnpDeviceId.Contains("NVMe", StringComparison.OrdinalIgnoreCase);

                if (disk?.Model == "Unknown Disk")
                {
                    disk?.Model = $"Disk {disk.Number}";
                }

                if (disk != null)
                    disks.Add(disk);
            }

            return disks;
        }

        private MediaType DetectMediaType(ManagementObject disk)
        {
            string model = disk["Model"]?.ToString() ?? "";
            string interfaceType = disk["InterfaceType"]?.ToString() ?? "";
            string media = disk["MediaType"]?.ToString() ?? "";

            model = model.ToUpperInvariant();

            if (model.Contains("NVME"))
                return MediaType.NVMe;

            if (interfaceType.Equals("USB", StringComparison.OrdinalIgnoreCase))
                return MediaType.USB;

            if (media.Contains("CD") || media.Contains("DVD"))
                return MediaType.Optical;

            if (model.Contains("SSD"))
                return MediaType.SSD;

            return MediaType.HDD;
        }
    }
}
