using Microsoft.Win32.SafeHandles;

namespace JollyDiskPart.Services
{
    public sealed class LockedVolume : IDisposable
    {
        private readonly SafeFileHandle _handle;

        internal LockedVolume(SafeFileHandle handle)
        {
            _handle = handle;
        }

        public SafeFileHandle Handle => _handle;

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
