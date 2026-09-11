using JollyDiskPart.Models;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;

namespace JollyDiskPart.Services
{
    public static class VolumeBitmapParser
    {
        public static VolumeBitmap Parse(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length < 16)
                throw new InvalidDataException("Volume bitmap response is too small.");

            long startingLcn = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
            long bitmapSize = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(8, 8));

            long bitmapBytes = (bitmapSize + 7) / 8;
            long availableBytes = buffer.Length - 16;

            if (bitmapBytes < 0 || bitmapBytes > availableBytes)
            {
                bitmapBytes = availableBytes;
            }

            var bitmap = new byte[bitmapBytes];
            Buffer.BlockCopy(buffer, 16, bitmap, 0, (int)bitmapBytes);

            long allocated = 0;
            for (int i = 0; i < bitmap.Length; i++)
            {
                allocated += BitOperations.PopCount(bitmap[i]);
            }

            return new VolumeBitmap
            {
                StartingLcn = startingLcn,
                BitmapSize = bitmapSize,
                Bitmap = bitmap,
                AllocatedClusters = allocated
            };
        }
    }
}