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

Discovery and file I/O run outside the Command Palette UI thread. The page reads an immutable catalog snapshot, filters it in memory, and creates items in pages of 40. Background updates retain the loaded range, extending it when new captures push previously loaded captures farther down the list. Search and filter changes reset paging. Each refresh publishes a complete replacement without exposing an intermediate empty list.

Thumbnail work is independently limited to four concurrent requests. Cached list items are reused while their capture file remains unchanged. Metadata edits update the existing card, details, preview, and commands without replacing the card or reloading its thumbnail, allowing Command Palette to retain its existing host view models and selection. The host owns the actual selection and scroll position; captures removed from the results can no longer retain selection.

## Key boundaries

- `ICaptureSettings` exposes normalized folders and sends reactive source-change notifications.
- `ICaptureSource` owns file enumeration and watcher lifetime.
- `CaptureCatalog` debounces watcher bursts, preserves a recency-sorted snapshot, and isolates the page from disk enumeration.
- `CaptureMetadataStore` persists favorites, labels, and tags without writing into capture files.
- `CaptureSearch` is the single matching point for filenames, paths, favorites, labels, tags, type terms, and date terms.

A future analysis feature should enrich the cached capture model or a separate local index, then extend `CaptureSearch`. It should not put image decoding or model inference in `GetItems()` or on the host UI thread.
