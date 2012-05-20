using System.Security.Cryptography;
using System.Threading;
using BTDB.Buffer;
using BTDB.ChunkCache;
using BTDB.KV2DBLayer;
using NUnit.Framework;

namespace BTDBTest
{
    [TestFixture]
    public class DiskChunkCacheTest
    {
        readonly ThreadLocal<HashAlgorithm> _hashAlg = new ThreadLocal<HashAlgorithm>(() => new SHA1CryptoServiceProvider());

        ByteBuffer CalcHash(byte[] bytes)
        {
            return ByteBuffer.NewAsync(_hashAlg.Value.ComputeHash(bytes));
        }

        [Test]
        public void CreateEmptyCache()
        {
            using (var fileCollection = new InMemoryFileCollection())
            using (new DiskChunkCache(fileCollection, 20, 1000))
            {
            }
        }

        [Test]
        public void GetFromEmptyCacheReturnsEmptyByteBuffer()
        {
            using (var fileCollection = new InMemoryFileCollection())
            using (var cache = new DiskChunkCache(fileCollection, 20, 1000))
            {
                Assert.AreEqual(0, cache.Get(CalcHash(new byte[] { 0 })).Result.Length);
            }
        }

        [Test]
        public void WhatIPutICanGet()
        {
            using (var fileCollection = new InMemoryFileCollection())
            using (var cache = new DiskChunkCache(fileCollection, 20, 1000))
            {
                cache.Put(CalcHash(new byte[] { 0 }), ByteBuffer.NewAsync(new byte[] { 1 }));
                Assert.AreEqual(new byte[] { 1 }, cache.Get(CalcHash(new byte[] { 0 })).Result.ToByteArray());
            }
        }
    }
}