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
CaptureManagerPage      Copy latest capture
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

Discovery and file I/O run outside the Command Palette UI thread. The page reads an immutable catalog snapshot, filters it in memory, and creates items in pages of 40. Thumbnail work is independently limited to four concurrent requests. Cached list items are reused while their capture file and metadata remain unchanged, allowing Command Palette to retain its existing host view models across refreshes.

## Key boundaries

- `ICaptureSettings` exposes normalized folders and sends reactive source-change notifications.
- `ICaptureSource` owns file enumeration and watcher lifetime.
- `CaptureCatalog` debounces watcher bursts, preserves a recency-sorted snapshot, and isolates the page from disk enumeration.
- `CaptureMetadataStore` persists favorites, labels, and tags without writing into capture files.
- `CaptureSearch` is the single matching point for filenames, paths, favorites, labels, tags, type terms, and date terms.

A future analysis feature should enrich the cached capture model or a separate local index, then extend `CaptureSearch`. It should not put image decoding or model inference in `GetItems()` or on the host UI thread.
