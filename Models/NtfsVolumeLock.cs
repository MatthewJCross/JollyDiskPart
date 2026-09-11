using Microsoft.Win32.SafeHandles;

namespace JollyDiskPart.Models
{
    public sealed class NtfsVolumeLock : IDisposable
    {
        public SafeFileHandle Handle { get; }
        public string Letter { get; }

        public NtfsVolumeLock(SafeFileHandle handle, string letter)
        {
            Handle = handle;
            Letter = letter;
        }

        public void Dispose()
        {
            Handle.Dispose();
        }
    }
}
