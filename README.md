# 🎵 MusicScanner

**MusicScanner** is a high-performance C# .NET console application for organizing and cleaning up large music libraries (>50GB). It scans through a given music folder, computes hashes to identify duplicate files, renames files to proper casing with special characters removed, and optionally deletes duplicates—retaining the most logically organized copy.

---

## 🚀 Features

- ✅ Fast, parallel processing using async and concurrent collections
- ✅ Hash-based duplicate detection using SHA-256
- ✅ Retains files in deeper directory structures (assumed to be better organized)
- ✅ Renames files to title case and strips invalid/special characters
- ✅ Provides real-time progress updates and speed metrics
- ✅ Minimal external dependencies (uses only .NET built-in libraries)
- ✅ Optionally deletes duplicate files after confirmation
- ✅ Removes empty directories and normalizes folder casing (planned enhancement)

---

## 📦 Requirements

- .NET 6.0 SDK or later
- Windows OS (console UI tested on Windows Terminal)

---

## 🧰 Usage

```bash
MusicScanner.exe "D:\Path\To\Your\MusicFolder"
```

- If no folder path is passed as an argument, the program will prompt you to enter one.
- The application will scan recursively, process supported audio files, and prompt before deleting duplicates.

---

## 🗂 Supported File Types

- `.mp3`
- `.flac`
- `.wav`
- `.aac`
- `.ogg`
- `.m4a`

---

## 🧠 How It Works

1. **Scan & Hash**  
   Computes SHA-256 hash of each supported file using parallelized async I/O.

2. **Detect Duplicates**  
   Compares hashes and retains one version (preferably deeper directory), marks others for deletion.

3. **Rename Files**  
   Applies proper casing and removes invalid/special characters.

4. **Show Progress**  
   Continuously displays progress, speed, and statistics with color-coded UI.

5. **Clean Up**  
   After user confirmation, deletes duplicates and (optionally) removes empty directories.

---

## 🧪 Sample Output

```bash
🎵 Music File Processor 🎵
=========================

Starting processing at: 12:01:23
Files scanned: 1,204
  Duplicates found: 128
  Files to rename: 213
  Processing speed: 25.3 files/sec (19.1 MB/sec)
  Elapsed time: 00:00:47
```

---

## ⚠️ Warnings

- **Always back up your music library before running the tool.**
- **Duplicate deletion is permanent once confirmed.**

---

## 🧱 Planned Enhancements

- [x] Empty directory removal after duplicate deletion
- [x] Normalize casing for remaining folder names
- [ ] Optionally move duplicates to a recycle bin instead of deleting

---

## 👨‍💻 License

MIT License — feel free to fork, modify, and contribute.

---

## 🤝 Contributions

Pull requests are welcome! If you find bugs or have suggestions, feel free to open an issue or start a discussion.
