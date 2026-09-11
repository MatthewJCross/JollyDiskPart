using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using JollyDiskPart.Interop;
using JollyDiskPart.Models;

namespace JollyDiskPart.Services
{
    public sealed class PartitionSectorMover
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;

        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;

        private const uint OpenExisting = 3;

        private const uint FileBegin = 0;
        private const uint FileCurrent = 1;

        private const int BufferSize = 16 * 1024 * 1024;

        public PartitionMoveRange VerifyMove(long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, long sectorSize = 512)
        {
            if (sourceOffsetBytes < 0)
                return PartitionMoveRange.Fail("Source offset cannot be negative.");

            if (destinationOffsetBytes < 0)
                return PartitionMoveRange.Fail("Destination offset cannot be negative.");

            if (lengthBytes <= 0)
                return PartitionMoveRange.Fail("Partition size must be greater than zero.");

            if (sectorSize <= 0)
                return PartitionMoveRange.Fail("Sector size must be greater than zero.");

            if (sourceOffsetBytes % sectorSize != 0)
                return PartitionMoveRange.Fail("Source offset is not sector aligned.");

            if (destinationOffsetBytes % sectorSize != 0)
                return PartitionMoveRange.Fail("Destination offset is not sector aligned.");

            if (lengthBytes % sectorSize != 0)
                return PartitionMoveRange.Fail("Partition size is not sector aligned.");

            if (destinationOffsetBytes == sourceOffsetBytes)
            {
                return PartitionMoveRange.Fail("Source and destination offsets are identical.");
            }

            long sourceEnd;

            try
            {
                sourceEnd = checked(sourceOffsetBytes + lengthBytes);
            }
            catch (OverflowException)
            {
                return PartitionMoveRange.Fail("Source range exceeds the supported address range.");
            }

            long destinationEnd;

            try
            {
                destinationEnd = checked(destinationOffsetBytes + lengthBytes);
            }
            catch (OverflowException)
            {
                return PartitionMoveRange.Fail("Destination range exceeds the supported address range.");
            }

            bool movingLeft = destinationOffsetBytes < sourceOffsetBytes;
            bool movingRight = destinationOffsetBytes > sourceOffsetBytes;
            bool overlaps = destinationOffsetBytes < sourceEnd && destinationEnd > sourceOffsetBytes;

            return PartitionMoveRange.Valid(sourceOffsetBytes, sourceEnd, destinationOffsetBytes, destinationEnd, lengthBytes, sectorSize, movingLeft, movingRight, overlaps);
        }

        public void VerifyRawCopy(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            string path = $@"\\.\PhysicalDrive{diskNumber}";
            using SafeFileHandle handle = CreateDiskHandle(path);
            byte[] sourceBuffer = new byte[BufferSize];
            byte[] destinationBuffer = new byte[BufferSize];
            long remaining = lengthBytes;
            long sourcePosition = sourceOffsetBytes;
            long destinationPosition = destinationOffsetBytes;

            while (remaining > 0)
            {
                int bytesThisPass = (int)Math.Min(BufferSize, remaining);
                bytesThisPass -= bytesThisPass % 512;
                if (bytesThisPass == 0)
                    bytesThisPass = 512;

                ReadAt(handle, sourceBuffer, sourcePosition, bytesThisPass);
                ReadAt(handle, destinationBuffer, destinationPosition, bytesThisPass);
                if (!sourceBuffer.AsSpan(0, bytesThisPass).SequenceEqual(destinationBuffer.AsSpan(0, bytesThisPass)))
                {
                    throw new IOException($"Raw copy verification failed at {sourcePosition:N0}.");
                }

                sourcePosition += bytesThisPass;
                destinationPosition += bytesThisPass;
                remaining -= bytesThisPass;
            }
        }

        public string GetMoveRangeDiagnostic(long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                return $"INVALID MOVE\n\n{range.Error}";

            return
                $"Partition Move Range\n\n" +
                $"Source offset:       {range.SourceOffsetBytes:N0} bytes\n" +
                $"Source end:          {range.SourceEndBytes:N0} bytes\n\n" +
                $"Destination offset:  {range.DestinationOffsetBytes:N0} bytes\n" +
                $"Destination end:     {range.DestinationEndBytes:N0} bytes\n\n" +
                $"Length:              {range.LengthBytes:N0} bytes\n" +
                $"Move distance:       {range.MoveDistanceBytes:N0} bytes\n" +
                $"Move distance:       {range.MoveDistanceMB:N0} MB\n" +
                $"Sector size:         {range.SectorSize:N0} bytes\n\n" +
                $"Moving left:         {range.MovingLeft}\n" +
                $"Moving right:        {range.MovingRight}\n" +
                $"Ranges overlap:      {range.Overlaps}";
        }

        public async Task MoveLeftAsync(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, bool verifyAfterCopy = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            if (!range.MovingLeft)
                throw new InvalidOperationException("MoveLeftAsync can only move a partition towards a lower disk offset.");

            if (lengthBytes % 512 != 0)
                throw new InvalidOperationException("Partition length must be sector aligned.");

            if (verifyAfterCopy)
            {
                await FastRawCopyAndVerifyLeftAsync(diskNumber, sourceOffsetBytes, destinationOffsetBytes, lengthBytes, progress, cancellationToken);
            }
            else
            {
                await FastRawCopyLeftAsync(diskNumber, sourceOffsetBytes, destinationOffsetBytes, lengthBytes, progress, cancellationToken);
            }
        }

        public async Task MoveRightAsync(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, bool verifyAfterCopy = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            if (range.MovingLeft)
                throw new InvalidOperationException("MoveRightAsync can only move a partition towards a higher disk offset.");

            if (lengthBytes % 512 != 0)
                throw new InvalidOperationException("Partition length must be sector aligned.");

            if (verifyAfterCopy)
                await FastRawCopyAndVerifyRightAsync(diskNumber, sourceOffsetBytes, destinationOffsetBytes, lengthBytes, progress, cancellationToken);
            else
                await FastRawCopyRightAsync(diskNumber, sourceOffsetBytes, destinationOffsetBytes, lengthBytes, progress, cancellationToken);
        }

        private static SafeFileHandle CreateDiskHandle(string path)
        {
            SafeFileHandle handle = VolumeNative.CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Unable to open {path} for raw disk access. Win32 error {error} (0x{error:X8}).");
            }

            return handle;
        }

        private static void ReadAt(SafeFileHandle handle, byte[] buffer, long offset, int count)
        {
            if (!SetFilePointer(handle, offset))
                ThrowLastError("Unable to seek to source position.");

            if (!ReadFile(handle, buffer, count, out uint bytesRead, IntPtr.Zero))
            {
                ThrowLastError("Unable to read source sectors.");
            }

            if (bytesRead != count)
            {
                throw new IOException($"Short disk read. Expected {count:N0} bytes, received {bytesRead:N0}.");
            }
        }

        private static void WriteAt(SafeFileHandle handle, byte[] buffer, long offset, int count)
        {
            if (!SetFilePointer(handle, offset))
                ThrowLastError($"Unable to seek to destination position {offset:N0}.");

            if (!WriteFile(handle, buffer, count, out uint bytesWritten, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Unable to write destination sectors. Win32 error: {error} (0x{error:X8}), offset: {offset:N0}, count: {count:N0}.");
            }

            if (bytesWritten != count)
            {
                throw new IOException($"Short disk write. Expected {count:N0} bytes, received {bytesWritten:N0}.");
            }
        }

        public async Task FastRawCopyLeftAsync(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            if (!range.MovingLeft)
                throw new InvalidOperationException("FastRawCopyAsync only supports moving left.");

            if (lengthBytes % 512 != 0)
                throw new InvalidOperationException("Partition length must be sector aligned.");

            const int bufferSize = 16 * 1024 * 1024;

            using SafeFileHandle handle = CreateDiskHandle($@"\\.\PhysicalDrive{diskNumber}");
            using var stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize, isAsync: false);

            byte[] buffer = new byte[bufferSize];

            long remaining = lengthBytes;
            long sourcePosition = sourceOffsetBytes;
            long destinationPosition = destinationOffsetBytes;
            long totalCopied = 0;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesThisPass = (int)Math.Min(buffer.Length, remaining);
                bytesThisPass -= bytesThisPass % 512;

                if (bytesThisPass == 0)
                    bytesThisPass = 512;

                // READ SOURCE
                stream.Position = sourcePosition;

                int bytesRead = 0;

                while (bytesRead < bytesThisPass)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, bytesThisPass - bytesRead), cancellationToken);

                    if (read == 0)
                        throw new IOException($"Unexpected end of disk while reading at {sourcePosition + bytesRead:N0}.");

                    bytesRead += read;
                }

                // WRITE DESTINATION
                stream.Position = destinationPosition;

                int bytesWritten = 0;

                while (bytesWritten < bytesThisPass)
                {
                    await stream.WriteAsync(buffer.AsMemory(bytesWritten, bytesThisPass - bytesWritten), cancellationToken);
                    bytesWritten += bytesThisPass - bytesWritten;
                }

                sourcePosition += bytesThisPass;
                destinationPosition += bytesThisPass;
                remaining -= bytesThisPass;
                totalCopied += bytesThisPass;

                double percent = (double)totalCopied / lengthBytes * 100.0;
                progress?.Report(percent);
            }

            await stream.FlushAsync(cancellationToken);
            progress?.Report(100.0);
        }

        public async Task FastRawCopyAndVerifyLeftAsync(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            if (!range.MovingLeft)
                throw new InvalidOperationException("FastRawCopyAndVerifyAsync only supports moving left.");

            if (lengthBytes % 512 != 0)
                throw new InvalidOperationException("Partition length must be sector aligned.");

            const int bufferSize = 16 * 1024 * 1024;

            using SafeFileHandle handle = CreateDiskHandle($@"\\.\PhysicalDrive{diskNumber}");
            using var stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize, isAsync: false);

            byte[] buffer = new byte[bufferSize];
            byte[] verifyBuffer = new byte[bufferSize];

            long remaining = lengthBytes;
            long sourcePosition = sourceOffsetBytes;
            long destinationPosition = destinationOffsetBytes;
            long totalCopied = 0;

            var totalStopwatch = Stopwatch.StartNew();
            var readStopwatch = new Stopwatch();
            var writeStopwatch = new Stopwatch();
            var verifyStopwatch = new Stopwatch();

            long totalReadBytes = 0;
            long totalWriteBytes = 0;
            long totalVerifyBytes = 0;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesThisPass = (int)Math.Min(buffer.Length, remaining);
                bytesThisPass -= bytesThisPass % 512;

                if (bytesThisPass == 0)
                    bytesThisPass = 512;

                // READ SOURCE
                readStopwatch.Start();

                stream.Position = sourcePosition;

                int bytesRead = 0;

                while (bytesRead < bytesThisPass)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, bytesThisPass - bytesRead), cancellationToken);

                    if (read == 0)
                        throw new IOException($"Unexpected end of disk while reading at {sourcePosition + bytesRead:N0}.");

                    bytesRead += read;
                }

                readStopwatch.Stop();
                totalReadBytes += bytesRead;

                // WRITE DESTINATION
                writeStopwatch.Start();

                stream.Position = destinationPosition;

                int bytesWritten = 0;

                while (bytesWritten < bytesThisPass)
                {
                    await stream.WriteAsync(buffer.AsMemory(bytesWritten, bytesThisPass - bytesWritten), cancellationToken);
                    bytesWritten += bytesThisPass - bytesWritten;
                }

                writeStopwatch.Stop();
                totalWriteBytes += bytesThisPass;

                // VERIFY DESTINATION
                verifyStopwatch.Start();

                stream.Position = destinationPosition;

                int verifyBytesRead = 0;

                while (verifyBytesRead < bytesThisPass)
                {
                    int read = await stream.ReadAsync(verifyBuffer.AsMemory(verifyBytesRead, bytesThisPass - verifyBytesRead), cancellationToken);

                    if (read == 0)
                        throw new IOException($"Unexpected end of disk while verifying at {destinationPosition + verifyBytesRead:N0}.");

                    verifyBytesRead += read;
                }

                verifyStopwatch.Stop();
                totalVerifyBytes += verifyBytesRead;

                // FIND EXACT MISMATCH
                int mismatch = FindFirstMismatch(buffer.AsSpan(0, bytesThisPass), verifyBuffer.AsSpan(0, bytesThisPass));

                if (mismatch >= 0)
                {
                    long failedOffset = totalCopied + mismatch;
                    long failedSource = sourcePosition + mismatch;
                    long failedDestination = destinationPosition + mismatch;

                    throw new IOException($"RAW COPY VERIFICATION FAILED.\n\nOffset: {failedOffset:N0} bytes ({failedOffset / (1024.0 * 1024.0):N2} MB)\nSource: {failedSource:N0}\nDestination: {failedDestination:N0}\nExpected: 0x{buffer[mismatch]:X2}\nActual: 0x{verifyBuffer[mismatch]:X2}");
                }

                sourcePosition += bytesThisPass;
                destinationPosition += bytesThisPass;
                remaining -= bytesThisPass;
                totalCopied += bytesThisPass;

                double percent = (double)totalCopied / lengthBytes * 100.0;
                progress?.Report(percent);

                Debug.WriteLine($"Verified: {totalCopied / (1024.0 * 1024.0):N0} MB / {lengthBytes / (1024.0 * 1024.0):N0} MB");
            }

            await stream.FlushAsync(cancellationToken);

            totalStopwatch.Stop();

            progress?.Report(100.0);

            double totalMB = lengthBytes / (1024.0 * 1024.0);
            double totalSeconds = totalStopwatch.Elapsed.TotalSeconds;
            double readSeconds = readStopwatch.Elapsed.TotalSeconds;
            double writeSeconds = writeStopwatch.Elapsed.TotalSeconds;
            double verifySeconds = verifyStopwatch.Elapsed.TotalSeconds;

            double totalSpeed = totalSeconds > 0 ? totalMB / totalSeconds : 0;
            double readMB = totalReadBytes / (1024.0 * 1024.0);
            double writeMB = totalWriteBytes / (1024.0 * 1024.0);
            double verifyMB = totalVerifyBytes / (1024.0 * 1024.0);

            double readSpeed = readSeconds > 0 ? readMB / readSeconds : 0;
            double writeSpeed = writeSeconds > 0 ? writeMB / writeSeconds : 0;
            double verifySpeed = verifySeconds > 0 ? verifyMB / verifySeconds : 0;

            Debug.WriteLine("");
            Debug.WriteLine("========================================");
            Debug.WriteLine("RAW COPY + VERIFY PERFORMANCE");
            Debug.WriteLine("========================================");
            Debug.WriteLine($"Buffer:          {bufferSize / (1024 * 1024)} MB");
            Debug.WriteLine($"Length:          {totalMB:N0} MB");
            Debug.WriteLine($"Total time:      {totalSeconds:F2} seconds");
            Debug.WriteLine($"Overall speed:   {totalSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine($"Read time:       {readSeconds:F2} seconds");
            Debug.WriteLine($"Read speed:      {readSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine($"Write time:      {writeSeconds:F2} seconds");
            Debug.WriteLine($"Write speed:     {writeSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine($"Verify time:     {verifySeconds:F2} seconds");
            Debug.WriteLine($"Verify speed:    {verifySpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine("Verification:     PASS");
            Debug.WriteLine("========================================");
        }

        public async Task FastRawCopyRightAsync(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            if (range.MovingLeft)
                throw new InvalidOperationException("FastRawCopyRightAsync only supports moving right.");

            if (lengthBytes % 512 != 0)
                throw new InvalidOperationException("Partition length must be sector aligned.");

            const int bufferSize = 16 * 1024 * 1024;

            using SafeFileHandle handle = CreateDiskHandle($@"\\.\PhysicalDrive{diskNumber}");
            using var stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize, isAsync: false);

            byte[] buffer = new byte[bufferSize];

            long remaining = lengthBytes;
            long sourcePosition = sourceOffsetBytes + lengthBytes;
            long destinationPosition = destinationOffsetBytes + lengthBytes;
            long totalCopied = 0;

            var stopwatch = Stopwatch.StartNew();

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesThisPass = (int)Math.Min(buffer.Length, remaining);
                bytesThisPass -= bytesThisPass % 512;

                if (bytesThisPass == 0)
                    bytesThisPass = 512;

                sourcePosition -= bytesThisPass;
                destinationPosition -= bytesThisPass;

                stream.Position = sourcePosition;

                int bytesRead = 0;

                while (bytesRead < bytesThisPass)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, bytesThisPass - bytesRead), cancellationToken);

                    if (read == 0)
                        throw new IOException($"Unexpected end of disk while reading at {sourcePosition + bytesRead:N0}.");

                    bytesRead += read;
                }

                stream.Position = destinationPosition;

                int bytesWritten = 0;

                while (bytesWritten < bytesThisPass)
                {
                    await stream.WriteAsync(buffer.AsMemory(bytesWritten, bytesThisPass - bytesWritten), cancellationToken);
                    bytesWritten += bytesThisPass - bytesWritten;
                }

                remaining -= bytesThisPass;
                totalCopied += bytesThisPass;

                double percent = (double)totalCopied / lengthBytes * 100.0;
                progress?.Report(percent);

                Debug.WriteLine($"Copied: {totalCopied / (1024.0 * 1024.0):N0} MB / {lengthBytes / (1024.0 * 1024.0):N0} MB");
            }

            await stream.FlushAsync(cancellationToken);

            stopwatch.Stop();

            double totalMB = lengthBytes / (1024.0 * 1024.0);
            double totalSeconds = stopwatch.Elapsed.TotalSeconds;
            double totalSpeed = totalSeconds > 0 ? totalMB / totalSeconds : 0;

            progress?.Report(100.0);

            Debug.WriteLine("");
            Debug.WriteLine("========================================");
            Debug.WriteLine("RAW COPY RIGHT PERFORMANCE");
            Debug.WriteLine("========================================");
            Debug.WriteLine($"Buffer:          {bufferSize / (1024 * 1024)} MB");
            Debug.WriteLine($"Length:          {totalMB:N0} MB");
            Debug.WriteLine($"Total time:      {totalSeconds:F2} seconds");
            Debug.WriteLine($"Overall speed:   {totalSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine("Verification:     NOT PERFORMED");
            Debug.WriteLine("========================================");
        }

        public async Task FastRawCopyAndVerifyRightAsync(int diskNumber, long sourceOffsetBytes, long destinationOffsetBytes, long lengthBytes, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var range = VerifyMove(sourceOffsetBytes, destinationOffsetBytes, lengthBytes);

            if (!range.IsValid)
                throw new InvalidOperationException(range.Error);

            if (range.MovingLeft)
                throw new InvalidOperationException("FastRawCopyAndVerifyRightAsync only supports moving right.");

            if (lengthBytes % 512 != 0)
                throw new InvalidOperationException("Partition length must be sector aligned.");

            const int bufferSize = 16 * 1024 * 1024;

            using SafeFileHandle handle = CreateDiskHandle($@"\\.\PhysicalDrive{diskNumber}");
            using var stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize, isAsync: false);

            byte[] buffer = new byte[bufferSize];
            byte[] verifyBuffer = new byte[bufferSize];

            long remaining = lengthBytes;
            long sourcePosition = sourceOffsetBytes + lengthBytes;
            long destinationPosition = destinationOffsetBytes + lengthBytes;
            long totalCopied = 0;

            var totalStopwatch = Stopwatch.StartNew();
            var readStopwatch = new Stopwatch();
            var writeStopwatch = new Stopwatch();
            var verifyStopwatch = new Stopwatch();

            long totalReadBytes = 0;
            long totalWriteBytes = 0;
            long totalVerifyBytes = 0;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesThisPass = (int)Math.Min(buffer.Length, remaining);
                bytesThisPass -= bytesThisPass % 512;

                if (bytesThisPass == 0)
                    bytesThisPass = 512;

                sourcePosition -= bytesThisPass;
                destinationPosition -= bytesThisPass;

                readStopwatch.Start();

                stream.Position = sourcePosition;

                int bytesRead = 0;

                while (bytesRead < bytesThisPass)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, bytesThisPass - bytesRead), cancellationToken);

                    if (read == 0)
                        throw new IOException($"Unexpected end of disk while reading at {sourcePosition + bytesRead:N0}.");

                    bytesRead += read;
                }

                readStopwatch.Stop();
                totalReadBytes += bytesRead;

                writeStopwatch.Start();

                stream.Position = destinationPosition;

                int bytesWritten = 0;

                while (bytesWritten < bytesThisPass)
                {
                    await stream.WriteAsync(buffer.AsMemory(bytesWritten, bytesThisPass - bytesWritten), cancellationToken);
                    bytesWritten += bytesThisPass - bytesWritten;
                }

                writeStopwatch.Stop();
                totalWriteBytes += bytesThisPass;

                verifyStopwatch.Start();

                stream.Position = destinationPosition;

                int verifyBytesRead = 0;

                while (verifyBytesRead < bytesThisPass)
                {
                    int read = await stream.ReadAsync(verifyBuffer.AsMemory(verifyBytesRead, bytesThisPass - verifyBytesRead), cancellationToken);

                    if (read == 0)
                        throw new IOException($"Unexpected end of disk while verifying at {destinationPosition + verifyBytesRead:N0}.");

                    verifyBytesRead += read;
                }

                verifyStopwatch.Stop();
                totalVerifyBytes += verifyBytesRead;

                int mismatch = FindFirstMismatch(buffer.AsSpan(0, bytesThisPass), verifyBuffer.AsSpan(0, bytesThisPass));

                if (mismatch >= 0)
                {
                    long failedSource = sourcePosition + mismatch;
                    long failedDestination = destinationPosition + mismatch;
                    long failedOffset = failedSource - sourceOffsetBytes;

                    throw new IOException($"RAW COPY VERIFICATION FAILED.\n\nOffset: {failedOffset:N0} bytes ({failedOffset / (1024.0 * 1024.0):N2} MB)\nSource: {failedSource:N0}\nDestination: {failedDestination:N0}\nExpected: 0x{buffer[mismatch]:X2}\nActual: 0x{verifyBuffer[mismatch]:X2}");
                }

                remaining -= bytesThisPass;
                totalCopied += bytesThisPass;

                double percent = (double)totalCopied / lengthBytes * 100.0;
                progress?.Report(percent);

                Debug.WriteLine($"Verified: {totalCopied / (1024.0 * 1024.0):N0} MB / {lengthBytes / (1024.0 * 1024.0):N0} MB");
            }

            await stream.FlushAsync(cancellationToken);

            totalStopwatch.Stop();

            progress?.Report(100.0);

            double totalMB = lengthBytes / (1024.0 * 1024.0);
            double totalSeconds = totalStopwatch.Elapsed.TotalSeconds;
            double readSeconds = readStopwatch.Elapsed.TotalSeconds;
            double writeSeconds = writeStopwatch.Elapsed.TotalSeconds;
            double verifySeconds = verifyStopwatch.Elapsed.TotalSeconds;

            double totalSpeed = totalSeconds > 0 ? totalMB / totalSeconds : 0;
            double readMB = totalReadBytes / (1024.0 * 1024.0);
            double writeMB = totalWriteBytes / (1024.0 * 1024.0);
            double verifyMB = totalVerifyBytes / (1024.0 * 1024.0);

            double readSpeed = readSeconds > 0 ? readMB / readSeconds : 0;
            double writeSpeed = writeSeconds > 0 ? writeMB / writeSeconds : 0;
            double verifySpeed = verifySeconds > 0 ? verifyMB / verifySeconds : 0;

            Debug.WriteLine("");
            Debug.WriteLine("========================================");
            Debug.WriteLine("RAW COPY + VERIFY RIGHT PERFORMANCE");
            Debug.WriteLine("========================================");
            Debug.WriteLine($"Buffer:          {bufferSize / (1024 * 1024)} MB");
            Debug.WriteLine($"Length:          {totalMB:N0} MB");
            Debug.WriteLine($"Total time:      {totalSeconds:F2} seconds");
            Debug.WriteLine($"Overall speed:   {totalSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine($"Read time:       {readSeconds:F2} seconds");
            Debug.WriteLine($"Read speed:      {readSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine($"Write time:      {writeSeconds:F2} seconds");
            Debug.WriteLine($"Write speed:     {writeSpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine($"Verify time:     {verifySeconds:F2} seconds");
            Debug.WriteLine($"Verify speed:    {verifySpeed:F1} MB/s");
            Debug.WriteLine("");
            Debug.WriteLine("Verification:     PASS");
            Debug.WriteLine("========================================");
        }

        private static int FindFirstMismatch(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
        {
            int length = Math.Min(expected.Length, actual.Length);

            for (int i = 0; i < length; i++)
            {
                if (expected[i] != actual[i])
                    return i;
            }

            return -1;
        }

        private static bool SetFilePointer(SafeFileHandle handle, long offset)
        {
            return SetFilePointerEx(handle, offset, out _, FileBegin);
        }

        private static void ThrowLastError(string message)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), message);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle hFile, [Out] byte[] lpBuffer, int nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFilePointerEx(SafeFileHandle hFile, long liDistanceToMove, out long lpNewFilePointer, uint dwMoveMethod);
    }
}
