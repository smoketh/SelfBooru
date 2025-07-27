using LiteDB;
//using SelfBooru.WinUI;
using SkiaSharp;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;

namespace SelfBooru
{

    internal class ImageScanner
    {
        private readonly LiteDatabase _db;
        internal readonly string _outputDir;
        private CancellationTokenSource? _cts;
        private static string thumbDir = "thumbs/";
        public bool IsScanning { get; private set; }
        public event Action<string>? Progress; // For status messages

        public ImageScanner(LiteDatabase db, string outputDir)
        {
            _db = db;
            _outputDir = outputDir;
            //Directory.CreateDirectory(thumbDir);
        }
        internal void Dispose()
        {
            Progress = null;
        }

        public async Task StartScanAsync()
        {
            if (IsScanning) return; // Already running
            System.Diagnostics.Debug.WriteLine($"Starting scanning");
            IsScanning = true;
            _cts = new CancellationTokenSource();

            try
            {
                await Task.Delay(5);
                await Task.Run(() => DoScan(_cts.Token));
            }
            finally
            {
                IsScanning = false;
            }
        }

        internal async Task StartBrokenScanAsync()
        {
            if (IsScanning) return; // Already running
            System.Diagnostics.Debug.WriteLine($"Starting scanning");
            IsScanning = true;
            _cts = new CancellationTokenSource();

            try
            {
                await Task.Run(() => DoBrokenScan(_cts.Token));
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void DoBrokenScan(CancellationToken ct)
        {
            var images = _db.GetCollection<TaggedDiskImage>("images");
            //var tags = _db.GetCollection<Tag>("tags");
            var query = images.FindAll().ToList();
            var total = query.Count;
            
            //query = query.Where(x => !File.Exists(x.filePath));
            //var bfinal = query.ToList();
            //foreach (var image in bfinal)

            for (int q=0; q<total; q++)
            {
                if (ct.IsCancellationRequested)
                {
                    break; //gracefully stop fucking the images and return what do we have
                }
                Progress?.Invoke($"[{q + 1}/{total}] {Path.GetFileName(query[q].filePath)}");
                if (!File.Exists(query[q].filePath))
                {
                    
                    images.Delete(query[q].Id);
                }
            }
            _db.Commit();
            _db.Checkpoint();
            Progress?.Invoke("Broken Scan complete.");
        }

        public void Cancel()
        {
            if (IsScanning)
                _cts?.Cancel();
        }

        private void DoScan(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_outputDir) || !Directory.Exists(_outputDir))
            {
                Progress?.Invoke("Output directory doesn't exist.");
                return;
            }

            var images = _db.GetCollection<TaggedDiskImage>("images");
            var tags = _db.GetCollection<Tag>("tags");
            var app = Application.Current as App;
            var thumbsDict= app?.thumbsDict ?? throw new InvalidOperationException("Application has no thumbs somehow");
            images.EnsureIndex(x => x.md5Hash, true);
            images.EnsureIndex(x => x.filePath);
            images.EnsureIndex("$.tags[*]");

            var files = Directory.EnumerateFiles(_outputDir, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) /*||
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)*/)
                .ToArray();

            Progress?.Invoke($"Found {files.Length} image files.");
            int total = files.Length;
            var nowUtc = DateTime.UtcNow;

            for (int i = 0; i < total; i++)
            {
                if(ct.IsCancellationRequested)
                {
                    break; //gracefully stop fucking the images and return what do we have
                }
                //ct.ThrowIfCancellationRequested();
                
                var file = files[i];
                //System.Diagnostics.Debug.WriteLine($"now scanning {file}");
                try
                {
                    Progress?.Invoke($"[{i + 1}/{total}] {Path.GetFileName(file)}");

                    var fi = new FileInfo(file);
                    //System.Diagnostics.Debug.WriteLine($"Grabbed file info");
                    var size = (int)fi.Length;

                    // Check if already in DB
                    var existing = images.FindOne(x => x.filePath == file);

                    string md5 = ComputeMd5(file); // Always re-check hash for now
                    //System.Diagnostics.Debug.WriteLine($"Computed md5");
                    //existingOrNew.metadata = meta ?? "{}";
                    TaggedDiskImage imageDoc;
                    var meta = "{}";
                    bool needThumb = false;
                    if (existing == null)
                    {
                        needThumb = true;
                        meta = GenerationMetadataExtractor.ExtractMetadata(file) ?? "{}";
                        if (string.IsNullOrWhiteSpace(meta))
                        {
                            System.Diagnostics.Debug.WriteLine($"{file} had no metadata, skipping");
                            //image with no meta. i think we should bailout from here
                            continue;
                        }
                        imageDoc = new TaggedDiskImage
                        {
                            md5Hash = md5,
                            filePath = file,
                            filesize = size,
                            created = fi.CreationTimeUtc,
                            lastSeen = nowUtc,
                            //thumbnail= MakeWebPThumb(file),
                            //thumbnailPath = GenerateThumbnail(file, thumbDir),
                            metadata = meta
                        };
                        try
                        {
                            images.Insert(imageDoc);
                        }
                        catch (LiteDB.LiteException e)
                        {
                            System.Diagnostics.Debug.WriteLine($"Caught litedb exception {e}");
                        }
                    }
                    else
                    {
                        if (existing.md5Hash != md5)
                        {
                            needThumb = true;
                            System.Diagnostics.Debug.WriteLine($"md5 hash mismatch, updating");
                            meta = GenerationMetadataExtractor.ExtractMetadata(file) ?? "{}";
                            if (string.IsNullOrWhiteSpace(meta))
                            {
                                System.Diagnostics.Debug.WriteLine($"{file} had no metadata, skipping");
                                //image with no meta. i think we should bailout from here
                                continue;
                            }
                            existing.md5Hash = md5;
                            existing.filesize = size;
                            
                            existing.metadata = meta;
                            //existing.thumbnail = MakeWebPThumb(file);


                        }
                        existing.lastSeen = nowUtc;
                        
                        imageDoc = existing;
                        try
                        {
                            images.Update(existing);
                        }
                        catch (LiteDB.LiteException e)
                        {
                            System.Diagnostics.Debug.WriteLine($"Caught litedb exception {e}");
                        }
                    }
                    if (!thumbsDict.ContainsKey(imageDoc.Id.ToString()))
                    {
                        needThumb = true;
                    }

                    if (needThumb)
                    {
                        var thumbnail = MakeWebPThumb(file);
                        thumbsDict.Add(imageDoc.Id.ToString(), thumbnail);
                    }
                    if (!string.IsNullOrWhiteSpace(meta) && meta != "{}")
                    {
                        var tagStrings = MetadataTagExtractor.ExtractTags(meta);
                        var tagIds = new List<ObjectId>(tagStrings.Count);

                        foreach (var text in tagStrings)
                        {
                            var tagEntry = tags.FindOne(x => x.text == text);
                            if (tagEntry == null)
                            {
                                tagEntry = new Tag { text = text };
                                tags.Insert(tagEntry);
                            }
                            tagIds.Add(tagEntry.Id);
                        }

                        // Assign and update image document
                        imageDoc.tags = tagIds;
                        try
                        {
                            images.Update(imageDoc);
                            //System.Diagnostics.Debug.WriteLine($"updated tags");
                        }
                        catch (LiteDB.LiteException e)
                        {
                            System.Diagnostics.Debug.WriteLine($"Caught litedb exception {e}");
                        }
                    }
                    _db.Commit();

                }
                catch (System.Exception ex)
                {
                    Progress?.Invoke($"Error scanning {file}: {ex.Message}");
                }
            }
            app!.SaveThumbs();
            _db.Checkpoint();
            Progress?.Invoke("Scan complete.");
        }

        

        private static string ComputeMd5(string path)
        {
            using var md5 = MD5.Create();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = md5.ComputeHash(fs);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        static SKSamplingOptions nOpt = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        private static byte[] MakeWebPThumb(string srcPath, int maxDim = 100)
        {
            using var input = File.OpenRead(srcPath);
            using var bmp = SKBitmap.Decode(input);

            float scale = Math.Min((float)maxDim / bmp.Width, (float)maxDim / bmp.Height);
            int w = Math.Max(1, (int)(bmp.Width * scale));
            int h = Math.Max(1, (int)(bmp.Height * scale));
            
            using var resized = bmp.Resize(new SKImageInfo(w, h), nOpt);
            using var img = SKImage.FromBitmap(resized);
            using var data = img.Encode(SKEncodedImageFormat.Jpeg, 80); // WebP 80% quality
            return data.ToArray();
        }

        private static string GenerateThumbnail(string sourcePath, string thumbDir)
        {
            Directory.CreateDirectory(thumbDir);

            string parentFolder = Path.GetFileName(Path.GetDirectoryName(sourcePath)) ?? "root";
            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string thumbFileName = $"{parentFolder}_{baseName}_thumb.jpg";

            string thumbPath = Path.Combine(thumbDir, thumbFileName);

            if (File.Exists(thumbPath)) return thumbPath; // Reuse existing

            using var input = File.OpenRead(sourcePath);
            using var original = SKBitmap.Decode(input);

            int maxDim = 256;
            float scale = Math.Min((float)maxDim / original.Width, (float)maxDim / original.Height);
            int newW = (int)(original.Width * scale);
            int newH = (int)(original.Height * scale);

            using var resized = original.Resize(new SKImageInfo(newW, newH), SKFilterQuality.Medium);
            using var image = SKImage.FromBitmap(resized);
            using var output = File.OpenWrite(thumbPath);
            image.Encode(SKEncodedImageFormat.Jpeg, 85).SaveTo(output);

            return thumbPath;
        }

        
    }
}