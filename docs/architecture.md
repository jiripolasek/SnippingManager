# Snipping Manager architecture

Snipping Manager is a packaged, out-of-process Command Palette command provider. The package supplies COM/app-extension registration; the application executable owns capture discovery, metadata, commands, and pages.

## Data flow

```text
multiline folder settings
        |
        v
FolderCaptureSource -- FileSystemWatcher notifications
        |
        v
CaptureCatalog -- debounced background refresh and recency cache
        |
        +----------------------+
        |                      |
        v                      v
CaptureManagerPage      Copy/open latest commands
        |
        v
search + organization filters + paging + counted date sections
        |
        v
CaptureListItemCache -- stable card identity and thumbnail lifetime
        |
        v
CaptureListItem -- thumbnail, lazy drag payload, open/copy/favorite/reveal/edit commands
        |
        v
CaptureMetadataStore -- local favorite/label/tag JSON
```

Discovery and file I/O run outside the Command Palette UI thread. The page reads an immutable catalog snapshot, filters it in memory, and creates items in pages of 40. Background updates retain the loaded range, extending it when new captures push previously loaded captures farther down the list. Search and filter changes reset paging. Each refresh publishes a complete replacement without exposing an intermediate empty list.

Thumbnail work is independently limited to four concurrent requests. Cached list items are reused while their capture file remains unchanged. Metadata edits update the existing card, details, preview, and commands without replacing the card or reloading its thumbnail, allowing Command Palette to retain its existing host view models and selection. The host owns the actual selection and scroll position; captures removed from the results can no longer retain selection.

## Key boundaries

- `ICaptureSettings` exposes normalized folders and sends reactive source-change notifications.
- `ICaptureSource` owns file enumeration and watcher lifetime.
- `CaptureCatalog` debounces watcher bursts, preserves a recency-sorted snapshot, and isolates the page from disk enumeration.
- `CaptureMetadataStore` persists favorites, labels, and tags without writing into capture files.
- `CaptureSearch` is the single matching point for filenames, paths, favorites, labels, tags, type terms, and date terms.

## Quick commands

The copy and open quick commands resolve the latest matching `CaptureMediaKind` from the catalog when invoked. The mixed copy and open commands omit the kind filter. Selection follows the catalog's newest-modified-first order, uses no additional filesystem scan, and is independent of gallery search and filters. Commands report loading separately from an empty matching result and never fall back to the other media kind.

Each action/type combination has a stable, distinct command ID. Copy variants reuse `CopyCaptureCommand` (image plus file for screenshots, file for recordings). Open variants reuse `OpenCaptureCommand` to launch the default app regardless of the gallery's preview setting. Tests substitute these final actions so selection and refresh behavior can be verified without changing the user's clipboard or launching an app.

## Metadata identity

Background discovery reads the Windows volume serial number, file ID, and creation time into `CaptureFile.FileIdentity`. These attribute queries do not read or modify media contents. Creation time helps distinguish a later file if a deleted file's ID is reused. See Microsoft's [FILE_ID_INFO contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info).

`CaptureMetadataStore` keeps identity-based records in `identifiedItems` alongside the legacy path-based `items` dictionary in the same JSON file. The catalog reconciles the discovered identities and upgrades matching legacy entries before publishing a new snapshot. Unobserved legacy entries are retained. Multiple paths identifying the same file share metadata; during legacy migration an existing identity label takes precedence, while tags and favorites are merged.

Page lookups remain in memory. Capture commands and metadata editors carry the capture's identity so an editor opened before a rename still targets the original file if its old path is reused. Renames need no metadata rewrite once a file is identified, and a new scan reconnects renamed files after a restart. Watcher notifications include directory changes so renaming a parent folder also refreshes the catalog.

This does not identify copies or moves to another volume. Filesystems without stable IDs retain path-based behavior, and legacy metadata cannot be recovered by identity if the file moved before its first identity-aware scan.

A future analysis feature should enrich the cached capture model or a separate local index, then extend `CaptureSearch`. It should not put image decoding or model inference in `GetItems()` or on the host UI thread.
