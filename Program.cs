using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a" };
    private static readonly ConcurrentDictionary<string, HashSet<string>> FileHashes = new();
    private static readonly ConcurrentBag<string> FilesToDelete = [];
    private static readonly ConcurrentBag<(string OldPath, string NewPath)> FilesToRename = [];
    private static readonly ConcurrentBag<(string OldPath, string NewPath)> DirectoriesToRename = [];

    private static int _totalFilesScanned;
    private static int _totalDuplicatesFound;
    private static int _totalFilesRenamed;
    private static long _totalBytesProcessed;
    private static readonly Stopwatch Timer = new();

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        try
        {
            Console.WriteLine("\ud83c\udfb5 Music File Processor \ud83c\udfb5");
            Console.WriteLine("=========================");
            Console.WriteLine();

            string rootFolder = args.Length > 0 ? args[0] : string.Empty;
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                PrintColor("Please provide the root music folder path.", ConsoleColor.Green);
                rootFolder = Console.ReadLine()?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                PrintColor("Valid root folder path is required.", ConsoleColor.Red);
                return;
            }

            Timer.Start();
            PrintColor($"Starting processing at: {DateTime.Now:HH:mm:ss}", ConsoleColor.Cyan);

            var progressTask = Task.Run(ReportProgress);
            await ProcessDirectoryAsync(rootFolder);
            ProcessDuplicates();
            await RenameFilesAsync();
            await RenameDirectoriesAsync(rootFolder);
            RemoveEmptyDirectories(rootFolder);

            Timer.Stop();
            await progressTask;
            PrintSummary();
        }
        catch (Exception ex)
        {
            PrintColor($"\nError: {ex.Message}", ConsoleColor.Red);
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    private static async Task ProcessDirectoryAsync(string directory)
    {
        try
        {
            var files = Directory.EnumerateFiles(directory)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            await Parallel.ForEachAsync(files, async (file, _) =>
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    Interlocked.Add(ref _totalBytesProcessed, fileInfo.Length);

                    var hash = await ComputeFileHashAsync(file);
                    FileHashes.AddOrUpdate(hash,
                        _ => new HashSet<string> { file },
                        (_, set) => { set.Add(file); return set; });

                    Interlocked.Increment(ref _totalFilesScanned);

                    var normalizedFilename = NormalizeFilename(Path.GetFileNameWithoutExtension(file)) + Path.GetExtension(file).ToLowerInvariant();
                    if (!string.Equals(Path.GetFileName(file), normalizedFilename, StringComparison.Ordinal))
                    {
                        var newPath = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, normalizedFilename);
                        FilesToRename.Add((file, newPath));
                    }
                }
                catch (Exception ex)
                {
                    PrintColor($"Error processing {file}: {ex.Message}", ConsoleColor.Yellow);
                }
            });

            var subDirs = Directory.EnumerateDirectories(directory);
            foreach (var subDir in subDirs)
            {
                var normalizedName = NormalizeFilename(Path.GetFileName(subDir));
                var newPath = Path.Combine(Path.GetDirectoryName(subDir) ?? string.Empty, normalizedName);
                if (!string.Equals(subDir, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    DirectoriesToRename.Add((subDir, newPath));
                }

                await ProcessDirectoryAsync(subDir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            PrintColor($"Access denied to directory: {directory}", ConsoleColor.Yellow);
        }
        catch (Exception ex)
        {
            PrintColor($"Error processing directory {directory}: {ex.Message}", ConsoleColor.Yellow);
        }
    }

    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        const int bufferSize = 81920;
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static void ProcessDuplicates()
    {
        foreach (var hashEntry in FileHashes.Where(x => x.Value.Count > 1))
        {
            var sortedFiles = hashEntry.Value
                .OrderByDescending(f => f.Split(Path.DirectorySeparatorChar).Length)
                .ThenBy(f => f.Length)
                .ToList();
            var fileToKeep = sortedFiles[0];
            foreach (var duplicateFile in sortedFiles.Skip(1))
            {
                FilesToDelete.Add(duplicateFile);
                Interlocked.Increment(ref _totalDuplicatesFound);
            }
        }
    }

    private static async Task RenameFilesAsync()
    {
        foreach (var (oldPath, newPath) in FilesToRename)
        {
            try
            {
                var finalNewPath = GetUniqueFilename(newPath);
                if (oldPath != finalNewPath)
                {
                    File.Move(oldPath, finalNewPath);
                }
                Interlocked.Increment(ref _totalFilesRenamed);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                PrintColor($"Error renaming {oldPath}: {ex.Message}", ConsoleColor.Yellow);
            }
        }
    }

    private static async Task RenameDirectoriesAsync(string rootFolder)
    {
        foreach (var (oldPath, newPath) in DirectoriesToRename.OrderByDescending(d => d.OldPath.Length))
        {
            try
            {
                if (!Directory.Exists(newPath))
                {
                    Directory.Move(oldPath, newPath);
                }
            }
            catch (Exception ex)
            {
                PrintColor($"Error renaming directory {oldPath}: {ex.Message}", ConsoleColor.Yellow);
            }
            await Task.CompletedTask;
        }
    }

    private static void RemoveEmptyDirectories(string startLocation)
    {
        foreach (var directory in Directory.GetDirectories(startLocation))
        {
            RemoveEmptyDirectories(directory);

            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                try
                {
                    Directory.Delete(directory);
                    PrintColor($"Removed empty directory: {directory}", ConsoleColor.DarkGray);
                }
                catch (Exception ex)
                {
                    PrintColor($"Could not remove {directory}: {ex.Message}", ConsoleColor.Yellow);
                }
            }
        }
    }

    private static string GetUniqueFilename(string desiredPath)
    {
        if (!File.Exists(desiredPath)) return desiredPath;

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var filenameWithoutExt = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        var counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{filenameWithoutExt}_{counter}{extension}");
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }

    private static string NormalizeFilename(string filename)
    {
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidRegEx = new Regex($"[{invalidChars}]");
        var cleaned = invalidRegEx.Replace(filename, "");

        var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
        cleaned = textInfo.ToTitleCase(cleaned.ToLower());
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = Regex.Replace(cleaned, @"[^\w\s\-_.,']", "");

        return cleaned.Trim();
    }

    private static async Task ReportProgress()
    {
        var startLeft = Console.CursorLeft;
        var startTop = Console.CursorTop;

        while (!Console.KeyAvailable && Timer.IsRunning)
        {
            try
            {
                Console.SetCursorPosition(startLeft, startTop);

                var elapsed = Timer.Elapsed;
                var filesPerSecond = _totalFilesScanned / Math.Max(elapsed.TotalSeconds, 1);
                var mbPerSecond = _totalBytesProcessed / Math.Max(elapsed.TotalSeconds * 1024 * 1024, 1);

                PrintColor($"Files scanned: {_totalFilesScanned:N0}", ConsoleColor.Green);
                PrintColor($"  Duplicates found: {_totalDuplicatesFound:N0}", ConsoleColor.Yellow);
                PrintColor($"  Files to rename: {FilesToRename.Count:N0}", ConsoleColor.Cyan);
                PrintColor($"  Processing speed: {filesPerSecond:N1} files/sec ({mbPerSecond:N1} MB/sec)", ConsoleColor.Magenta);
                PrintColor($"  Elapsed time: {elapsed:hh\\:mm\\:ss}", ConsoleColor.White);

                await Task.Delay(500);
            }
            catch { }
        }
    }

    private static void PrintSummary()
    {
        Console.WriteLine();
        PrintColor("=== Processing Complete ===", ConsoleColor.Cyan);
        PrintColor($"Total files scanned: {_totalFilesScanned:N0}", ConsoleColor.Green);
        PrintColor($"Total duplicates found: {_totalDuplicatesFound:N0}", ConsoleColor.Yellow);
        PrintColor($"Total files renamed: {_totalFilesRenamed:N0}", ConsoleColor.Cyan);
        PrintColor($"Total data processed: {_totalBytesProcessed / (1024 * 1024):N0} MB", ConsoleColor.Magenta);
        PrintColor($"Total time: {Timer.Elapsed:hh\\:mm\\:ss}", ConsoleColor.White);

        if (FilesToDelete.Count > 0)
        {
            Console.WriteLine();
            PrintColor("The following duplicate files will be deleted:", ConsoleColor.Yellow);
            foreach (var file in FilesToDelete.Take(5))
            {
                Console.WriteLine($"  {file}");
            }
            if (FilesToDelete.Count > 5)
            {
                Console.WriteLine($"  ... and {FilesToDelete.Count - 5} more");
            }

            Console.WriteLine();
            PrintColor("Press 'Y' to confirm deletion of duplicates, or any other key to exit without deleting.", ConsoleColor.Red);
            if (Console.ReadKey().Key == ConsoleKey.Y)
            {
                DeleteDuplicateFiles();
            }
        }
    }

    private static void DeleteDuplicateFiles()
    {
        Console.WriteLine();
        PrintColor("Deleting duplicates...", ConsoleColor.Red);
        var deletedCount = 0;
        foreach (var file in FilesToDelete)
        {
            try
            {
                File.Delete(file);
                deletedCount++;
                Console.Write(".");
                if (deletedCount % 50 == 0) Console.WriteLine();
            }
            catch (Exception ex)
            {
                PrintColor($"\nError deleting {file}: {ex.Message}", ConsoleColor.Yellow);
            }
        }
        Console.WriteLine();
        PrintColor($"Deleted {deletedCount:N0} duplicate files.", ConsoleColor.Green);
    }

    private static void PrintColor(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}