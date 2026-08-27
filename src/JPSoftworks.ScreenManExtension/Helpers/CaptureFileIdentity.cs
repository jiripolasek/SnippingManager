using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace JPSoftworks.ScreenManExtension.Helpers;

internal static partial class CaptureFileIdentity
{
    private const uint OpenExisting = 3;
    private const int FileIdInfoClass = 18;
    private const int FileBasicInfoClass = 0;

    internal static string? TryGet(string path)
    {
        // Query attributes only; do not read, hash, or modify the capture's contents.
        using var handle = CreateFile(
            path,
            desiredAccess: 0,
            FileShare.ReadWrite | FileShare.Delete,
            securityAttributes: IntPtr.Zero,
            creationDisposition: OpenExisting,
            flagsAndAttributes: 0,
            templateFile: IntPtr.Zero);
        if (handle.IsInvalid ||
            GetFileId(handle, FileIdInfoClass, out var identity, (uint)Marshal.SizeOf<FileIdInfo>()) == 0 ||
            GetBasicInfo(handle, FileBasicInfoClass, out var basicInfo, (uint)Marshal.SizeOf<FileBasicInfo>()) == 0 ||
            identity.FileId == Guid.Empty)
        {
            return null;
        }

        // Creation time also guards against a deleted file's identifier being reused later.
        return $"{identity.VolumeSerialNumber:X16}:{identity.FileId:N}:{basicInfo.CreationTime:X16}";
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx")]
    private static partial int GetFileId(SafeFileHandle handle, int informationClass, out FileIdInfo info, uint bufferSize);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx")]
    private static partial int GetBasicInfo(SafeFileHandle handle, int informationClass, out FileBasicInfo info, uint bufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        internal ulong VolumeSerialNumber;
        internal Guid FileId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileBasicInfo
    {
        internal long CreationTime;
        internal long LastAccessTime;
        internal long LastWriteTime;
        internal long ChangeTime;
        internal uint FileAttributes;
    }
}
