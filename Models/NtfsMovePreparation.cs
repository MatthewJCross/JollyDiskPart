namespace JollyDiskPart.Models
{
    public sealed class NtfsMovePreparation
    {
        public bool IsValid { get; init; }
        public string? Error { get; init; }
        public VolumeInfo? VolumeInfo { get; init; }
        public VolumeBitmap? Bitmap { get; init; }

        public static NtfsMovePreparation Success(VolumeInfo volumeInfo, VolumeBitmap bitmap)
        {
            return new NtfsMovePreparation
            {
                IsValid = true,
                VolumeInfo = volumeInfo,
                Bitmap = bitmap
            };
        }

        public static NtfsMovePreparation Fail(string error)
        {
            return new NtfsMovePreparation
            {
                IsValid = false,
                Error = error
            };
        }
    }
}
