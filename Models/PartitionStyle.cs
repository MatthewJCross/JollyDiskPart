namespace JollyDiskPart.Models
{
    public enum PartitionStyle
    {
        Unknown,
        Basic,
        RAW,
        MBR,
        GPT,
        EFI,
        MSR,
        Dynamic,
        Unallocated,
        Recovery
    }
}
