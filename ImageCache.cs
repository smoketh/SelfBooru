using System;
using Microsoft.Extensions.Caching.Memory;
namespace SelfBooru
{
    public sealed class ImageCache
    {
        private readonly IMemoryCache _bytes;

        public ImageCache(long bytesLimit = 2L * 1024 * 1024 * 1024) // 2 GB
        {
            _bytes = new MemoryCache(new MemoryCacheOptions { SizeLimit = bytesLimit });
        }

        public byte[] GetOrAdd(string key, Func<byte[]> factory)
        {
            return _bytes.GetOrCreate(key, e =>
                    {
                        var data = factory();
                        e.Size = data.LongLength;
                        e.SlidingExpiration = TimeSpan.FromMinutes(30);

                        return data;
                    });
        }
    }

}