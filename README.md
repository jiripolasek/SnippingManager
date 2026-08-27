<div align="center">

<img src="./art/StoreLogo.png" alt="Snipping Manager logo" width="200" height="200">

<h1 align="center">Snipping Manager</h1>
<p><q>Your latest snip, a few keystrokes away.</q></p>

</div>

Browse, organize, and share your screenshots and screen recordings from Command Palette.

## Features

- A gallery grouped by date, with the newest captures first and automatic updates as your folders change.
- Quick commands to copy or open the latest screenshot or recording, plus drag and drop into other apps.
- Favorites, labels, and tags to keep captures organized.
- Choose between opening captures in your default app or previewing them in Command Palette with a details pane. Recordings use still previews.
- Search by filename, folder, label, tag, or date, with filters for capture type and organization.

Open **Screenshots and recordings** in Command Palette to browse your captures, or run a quick command directly.

## Quick commands

| Command | Action |
| --- | --- |
| **Copy latest screenshot** | Copy the newest screenshot as an image and file, even if a recording is newer. |
| **Copy latest recording** | Copy the newest screen recording as a file, even if a screenshot is newer. |
| **Open latest screenshot** | Open the newest screenshot in your default app. |
| **Open latest recording** | Open the newest screen recording in your default app. |
| **Copy latest capture** | Copy whichever screenshot or recording is newest. |
| **Open latest capture** | Open whichever screenshot or recording is newest in your default app. |

Latest means the most recently modified matching file in the discovered capture folders. Quick commands use the current catalog when invoked, independently of gallery search and filters. They report when captures are still loading or when no matching type exists; they never substitute the other type. The open commands always use the default app; the gallery's opening preference is unchanged.

## Capture folders

By default, Snipping Manager watches the standard Snipping Tool folders, including redirected Pictures and Videos locations:

- `Pictures\Screenshots`
- `Videos\Screen Recordings`

Add other locations in **Capture folders** in Snipping Manager settings, one folder per line. Subfolders are included by default. Environment variables such as `%USERPROFILE%` and paths beginning with `~\` are supported.

Supported image formats: PNG, JPEG, GIF, BMP, WebP, TIFF, HEIC/HEIF, and AVIF. Supported recording formats: MP4, M4V, MOV, MKV, WebM, AVI, and WMV.

## Storage and privacy

Labels, tags, and favorites are stored locally, separately from your media. No capture contents or metadata leave the machine.

On filesystems with stable Windows file IDs, such as NTFS, metadata follows file and folder renames and moves on the same volume, including across restarts. The destination must be in a configured capture folder to appear in the gallery. Copies and moves to another volume are treated as separate files.

Existing metadata is upgraded when its capture is next discovered at the original path. If a file ID is unavailable, metadata remains tied to the path; Snipping Manager does not guess matches from names, sizes, or dates.

## License

Snipping Manager is licensed under the [Apache License 2.0](LICENSE.txt).

## Notices

Third-party software acknowledgements and license information are available in [NOTICE.md](NOTICE.md).

## Security

See [SECURITY.md](SECURITY.md) for the supported-version policy and private vulnerability reporting instructions.

## Author

[Jiří Polášek](https://jiripolasek.com)
