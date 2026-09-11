namespace JollyDiskPart.Models
{
    public sealed class VolumeBitmap
    {
        public long StartingLcn { get; init; }
        public long BitmapSize { get; init; }
        public byte[] Bitmap { get; init; } = [];
        public long AllocatedClusters { get; init; }
        public long FreeClusters => BitmapSize - AllocatedClusters;

        public bool IsAllocated(long lcn)
        {
            if (lcn < StartingLcn)
                return false;

            long index = lcn - StartingLcn;

            if (index >= BitmapSize)
                return false;

            int byteIndex = (int)(index / 8);
            int bitIndex = (int)(index % 8);

            if (byteIndex >= Bitmap.Length)
                return false;

            return (Bitmap[byteIndex] & (1 << bitIndex)) != 0;
        }

        public bool IsFree(long lcn)
        {
            return !IsAllocated(lcn);
        }
    }
}
