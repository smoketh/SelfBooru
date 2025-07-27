using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace SelfBooru
{
    internal static class GenerationMetadataExtractor
    {
        public static string? ExtractMetadata(string filePath)
        {
            // First, try text chunks
            string? chunkMeta = PngChunkReader.ReadParameters(filePath);
            if (!string.IsNullOrEmpty(chunkMeta))
                return chunkMeta;

            // Fall back to stealth LSB
            return StealthPngInfoExtractor.Extract(filePath);
        }
    }

    internal static class StealthPngInfoExtractor
    {
        private const string SIG_ALPHA_INFO = "stealth_pnginfo";
        private const string SIG_ALPHA_COMP = "stealth_pngcomp";
        private const string SIG_RGB_INFO = "stealth_rgbinfo";
        private const string SIG_RGB_COMP = "stealth_rgbcomp";

        public static string? Extract(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var skStream = new SKManagedStream(stream);
            using var codec = SKCodec.Create(skStream);
            if (codec == null) return null;

            var info = codec.Info;
            using var bmp = new SKBitmap(info);
            codec.GetPixels(bmp.Info, bmp.GetPixels());

            return Extract(bmp);
        }

        public static string? Extract(SKBitmap bitmap)
        {
            // If you *also* want to respect existing PNG text chunks ("parameters"),
            // you can read chunks with another lib. SkiaSharp doesn't expose them directly.
            // So we go straight to stealth LSB data like your Python did.

            var width = bitmap.Width;
            var height = bitmap.Height;

            bool hasAlpha = bitmap.AlphaType != SKAlphaType.Opaque;

            bool sigConfirmed = false;
            bool confirmingSignature = true;
            bool readingParamLen = false;
            bool readingParam = false;
            bool readEnd = false;

            string? mode = null;
            bool compressed = false;

            // instead of string concatenation, use StringBuilder
            var bufferA = new StringBuilder();
            var bufferRGB = new StringBuilder();

            int indexA = 0;
            int indexRGB = 0;
            int paramLen = 0;
            string binaryData = string.Empty;

            for (int x = 0; x < width && !readEnd; x++)
            {
                for (int y = 0; y < height && !readEnd; y++)
                {
                    var color = bitmap.GetPixel(x, y);

                    if (hasAlpha)
                    {
                        int a = color.Alpha;
                        bufferA.Append((a & 1) == 1 ? '1' : '0');
                        indexA++;
                    }

                    int r = color.Red;
                    int g = color.Green;
                    int b = color.Blue;

                    bufferRGB.Append((r & 1) == 1 ? '1' : '0');
                    bufferRGB.Append((g & 1) == 1 ? '1' : '0');
                    bufferRGB.Append((b & 1) == 1 ? '1' : '0');
                    indexRGB += 3;

                    if (confirmingSignature)
                    {
                        // Try alpha channel signature first
                        if (hasAlpha && indexA == SIG_ALPHA_INFO.Length * 8)
                        {
                            var decodedSig = BitsToUtf8(bufferA.ToString());
                            if (decodedSig == SIG_ALPHA_INFO || decodedSig == SIG_ALPHA_COMP)
                            {
                                confirmingSignature = false;
                                sigConfirmed = true;
                                readingParamLen = true;
                                mode = "alpha";
                                compressed = decodedSig == SIG_ALPHA_COMP;
                                bufferA.Clear();
                                indexA = 0;
                                continue;
                            }
                            else
                            {
                                readEnd = true;
                                break;
                            }
                        }
                        // Try RGB signature
                        else if (indexRGB == SIG_RGB_INFO.Length * 8)
                        {
                            var decodedSig = BitsToUtf8(bufferRGB.ToString());
                            if (decodedSig == SIG_RGB_INFO || decodedSig == SIG_RGB_COMP)
                            {
                                confirmingSignature = false;
                                sigConfirmed = true;
                                readingParamLen = true;
                                mode = "rgb";
                                compressed = decodedSig == SIG_RGB_COMP;
                                bufferRGB.Clear();
                                indexRGB = 0;
                                continue;
                            }
                            else
                            {
                                readEnd = true;
                                break;
                            }
                        }
                    }
                    else if (readingParamLen)
                    {
                        if (mode == "alpha")
                        {
                            if (indexA == 32) // 32 bits integer
                            {
                                paramLen = Convert.ToInt32(bufferA.ToString(), 2);
                                readingParamLen = false;
                                readingParam = true;
                                bufferA.Clear();
                                indexA = 0;
                            }
                        }
                        else // rgb mode
                        {
                            if (indexRGB == 33) // python code reads 33 then adjust
                            {
                                // last bit popped & kept in buffer
                                char pop = bufferRGB[^1];
                                bufferRGB.Length -= 1;
                                paramLen = Convert.ToInt32(bufferRGB.ToString(), 2);
                                readingParamLen = false;
                                readingParam = true;

                                bufferRGB.Clear();
                                bufferRGB.Append(pop);
                                indexRGB = 1;
                            }
                        }
                    }
                    else if (readingParam)
                    {
                        if (mode == "alpha")
                        {
                            if (indexA == paramLen)
                            {
                                binaryData = bufferA.ToString();
                                readEnd = true;
                                break;
                            }
                        }
                        else // rgb
                        {
                            if (indexRGB >= paramLen)
                            {
                                int diff = paramLen - indexRGB;
                                var data = bufferRGB.ToString();
                                if (diff < 0)
                                {
                                    data = data[..diff]; // truncate extra bits
                                }
                                binaryData = data;
                                readEnd = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Shouldn't happen
                        readEnd = true;
                        break;
                    }
                }
            }

            if (sigConfirmed && !string.IsNullOrEmpty(binaryData))
            {
                try
                {
                    var bytes = BitsToBytes(binaryData);
                    if (compressed)
                    {
                        var decompressed = DecompressGZip(bytes);
                        return decompressed;
                    }
                    else
                    {
                        return Encoding.UTF8.GetString(bytes);
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static string BitsToUtf8(string bits)
        {
            try
            {
                var bytes = BitsToBytes(bits);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static byte[] BitsToBytes(string bits)
        {
            int byteCount = bits.Length / 8;
            var result = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                var b = bits.AsSpan(i * 8, 8);
                result[i] = Convert.ToByte(b.ToString(), 2);
            }
            return result;
        }

        private static string DecompressGZip(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return Encoding.UTF8.GetString(outMs.ToArray());
        }
    }

    internal static class PngChunkReader
    {
        public static string? ReadParameters(string filePath)
        {
            // Validate PNG signature
            byte[] signature = new byte[8];
            using (var fs = File.OpenRead(filePath))
            {
                if (fs.Read(signature, 0, 8) != 8 ||
                    !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                    return null;

                while (fs.Position < fs.Length)
                {
                    // Read length (4 bytes big-endian)
                    byte[] lengthBytes = new byte[4];
                    if (fs.Read(lengthBytes, 0, 4) != 4) break;
                    int length = (lengthBytes[0] << 24) |
                                 (lengthBytes[1] << 16) |
                                 (lengthBytes[2] << 8) |
                                  lengthBytes[3];

                    // Read chunk type (4 bytes)
                    byte[] typeBytes = new byte[4];
                    if (fs.Read(typeBytes, 0, 4) != 4) break;
                    string chunkType = Encoding.ASCII.GetString(typeBytes);

                    // Read chunk data
                    byte[] data = new byte[length];
                    if (fs.Read(data, 0, length) != length) break;

                    // Read and skip CRC
                    fs.Seek(4, SeekOrigin.Current);

                    if (chunkType == "tEXt" || chunkType == "iTXt")
                    {
                        string text = Encoding.UTF8.GetString(data);
                        // `tEXt` is typically "key\0value"
                        int idx = text.IndexOf('\0');
                        if (idx > 0)
                        {
                            string key = text.Substring(0, idx);
                            string value = text.Substring(idx + 1);
                            if (key.Equals("parameters", StringComparison.OrdinalIgnoreCase))
                            {
                                return value;
                            }
                        }
                    }
                    else if (chunkType == "IEND")
                    {
                        // End of PNG
                        break;
                    }
                }
            }
            return null;
        }
    }

    internal static class MetadataTagExtractor
    {
        /// <summary>
        /// Extract tags from metadata for search functionality.
        /// </summary>
        /// <param name="metadata">The metadata string containing generation parameters.</param>
        /// <returns>List of unique, sanitized tags.</returns>
        public static List<string> ExtractTags(string metadata)
        {
            var tags = new List<string>();
            if (string.IsNullOrEmpty(metadata))
                return tags;

            // Split into lines and process until Negative prompt or Steps
            var lines = metadata.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine;
                if (line.StartsWith("Negative prompt:", StringComparison.OrdinalIgnoreCase) )
                {
                    

                    break;
                }

                if(line.StartsWith("Steps:", StringComparison.OrdinalIgnoreCase))//special case - model
                {
                    var segments = line.Split(',');
                    foreach (var segment in segments)
                    {
                        if(segment.Contains("Model: "))
                        {
                            var fFinal = segment.Replace("Model: ", "Model:");
                            tags.Add(fFinal);
                        }
                    }
                    break;
                }

                // Preserve escaped parentheses markers
                line = line.Replace(@"\(", "^^%")
                           .Replace(@"\)", "^^$")
                           .Replace(@"\", "")
                           .Replace("BREAK", "");

                // 1) Extract parenthetical weighted tags
                var parentheticals = Regex.Matches(line, @"\([^)]*\)")
                                           .Cast<Match>()
                                           .Select(m => m.Value)
                                           .ToList();

                foreach (var section in parentheticals)
                {
                    var content = section.Trim('(', ')');
                    var items = content.Split(',')
                                       .Select(item => item.Trim());
                    foreach (var item in items)
                    {
                        // Remove weight suffix (:weight)
                        var tag = Regex.Replace(item, @":-?\d+\.?\d*$", string.Empty).Trim();
                        tag = SanitizeTag(tag);
                        if (!string.IsNullOrEmpty(tag))
                            tags.Add(tag);
                    }
                }

                string[] directives = { "lora", "lyco" };
                try
                {
                    var gLora = Regex.Matches(line, @"<[^>]*>");
                    if (gLora != null && gLora.Count > 0)
                    {
                        foreach (Match item in gLora)
                        {
                            var pProcess = item.Value; //our lora directive
                            bool served = false;
                            foreach (var directive in directives)
                            {
                                if (!pProcess.Contains($"<{directive}:"))
                                {
                                    //System.Diagnostics.Debug.WriteLine($"found a <> directive which is not lora {pProcess}, skipping");
                                    continue;//huh, non lora <> directive
                                }
                                else
                                {
                                    served = true;
                                }
                                pProcess = pProcess.Trim('<', '>');
                                pProcess = pProcess.Replace($"{directive}:", "");
                                var colonPos = pProcess.IndexOf(':');
                                if (colonPos >= 0)//standard lora directive
                                {
                                    pProcess = pProcess.Substring(0, colonPos);
                                }
                                tags.Add($"{directive}:{pProcess}");
                            }
                            if(!served)
                            {
                                System.Diagnostics.Debug.WriteLine($"found a <> directive which is not recognized {pProcess}, skipping");
                            }
                        }
                    }
                }
                catch (RegexMatchTimeoutException) { }

                // 2) Remove all parentheses and LORA directives
                line = Regex.Replace(line, @"\([^)]*\)", string.Empty);
                line = Regex.Replace(line, @"<[^>]*>", string.Empty);//loras should be processed now
                line = Regex.Replace(line, @" {2,}", " ");
                line = line.Trim();

                // Restore escaped parentheses
                line = line.Replace("^^%", "(")
                           .Replace("^^$", ")");

                // 3) Split remainder by commas
                var simpleTags = line.Split(',')
                                     .Select(ftag => SanitizeTag(ftag))
                                     .Where(t => !string.IsNullOrEmpty(t));

                tags.AddRange(simpleTags);
            }

            // Deduplicate (case-insensitive)
            var uniqueTags = tags.Distinct(StringComparer.OrdinalIgnoreCase)
                                 .ToList();
            return uniqueTags;
        }

        /// <summary>
        /// Sanitize a raw tag: exclude LORA directives, replace spaces with underscores, lowercase.
        /// </summary>
        internal static string SanitizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return string.Empty;

            // Exclude LORA tags
            if (tag.StartsWith("<lora", StringComparison.OrdinalIgnoreCase) ||
                tag.StartsWith(" <lora", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // Trim leading/trailing spaces
            tag = tag.Trim();

            // Replace spaces with underscores and lowercase
            tag = tag.Replace(' ', '_').ToLowerInvariant();

            return tag;
        }
    }
}
