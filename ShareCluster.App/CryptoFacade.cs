using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ShareCluster
{
    public class CryptoFacade
    {
        RandomNumberGenerator _randomGenerator;
        readonly Func<HashAlgorithm> _hashAlgFactory;
        
        public CryptoFacade(Func<HashAlgorithm> algorithmFactory)
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
        public Id EmptyHash { get; }

        public Id CreateRandom()
        {
            var result = new byte[BytesLength];
            _randomGenerator.GetBytes(result);
            return new Id(result);
        }

        public Id ComputeHash(byte[] bytes)
        {
            using (HashAlgorithm alg = CreateHashAlgorithm())
            {
                return new Id(alg.ComputeHash(bytes));
            }
        }

        public Id HashFromHashes(IEnumerable<Id> hashes)
        {
            using(HashAlgorithm hashAlg = CreateHashAlgorithm())
            using (var cryptoStream = new CryptoStream(Stream.Null, hashAlg, CryptoStreamMode.Write))
            {
                foreach (Id hash in hashes)
                {
                    cryptoStream.Write(hash);
                }

                // compute and return
                cryptoStream.FlushFinalBlock();
                return new Id(hashAlg.Hash);
            }
        }
    }
}
