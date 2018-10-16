using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ShareCluster
{
    public class CryptoProvider
    {
        RandomNumberGenerator _randomGenerator;
        readonly Func<HashAlgorithm> _hashAlgFactory;
        
        public CryptoProvider(Func<HashAlgorithm> algorithmFactory)
        {
            _randomGenerator = new RNGCryptoServiceProvider();
            _hashAlgFactory = algorithmFactory ?? throw new ArgumentNullException(nameof(algorithmFactory));
            using (HashAlgorithm alg = algorithmFactory())
            {
                BytesLength = (alg.HashSize + 7) / 8;
            }

            EmptyHash = ComputeHash(new byte[0]);
        }

        public HashAlgorithm CreateHashAlgorithm() => _hashAlgFactory();

        public int BytesLength { get; }
        public PackageId EmptyHash { get; }

        public PackageId CreateRandom()
        {
            var result = new byte[BytesLength];
            _randomGenerator.GetBytes(result);
            return new PackageId(result);
        }

        public PackageId ComputeHash(byte[] bytes)
        {
            using (HashAlgorithm alg = CreateHashAlgorithm())
            {
                return new PackageId(alg.ComputeHash(bytes));
            }
        }

        public PackageId HashFromHashes(IEnumerable<PackageId> hashes)
        {
            using(HashAlgorithm hashAlg = CreateHashAlgorithm())
            using (var cryptoStream = new CryptoStream(Stream.Null, hashAlg, CryptoStreamMode.Write))
            {
                foreach (PackageId hash in hashes)
                {
                    cryptoStream.Write(hash);
                }

                // compute and return
                cryptoStream.FlushFinalBlock();
                return new PackageId(hashAlg.Hash);
            }
        }
    }
}
