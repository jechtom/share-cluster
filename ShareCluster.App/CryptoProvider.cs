using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster
{
    public class CryptoProvider
    {
        RandomNumberGenerator _randomGenerator;
        readonly Func<HashAlgorithm> _hashAlgFactory;
        HashAlgorithm _internalAlg;
        
        public CryptoProvider(Func<HashAlgorithm> algorithmFactory)
        {
            _randomGenerator = new RNGCryptoServiceProvider();
            _hashAlgFactory = algorithmFactory ?? throw new ArgumentNullException(nameof(algorithmFactory));
            _internalAlg = algorithmFactory();
            BytesLength = (_internalAlg.HashSize + 7) / 8;

            using (HashAlgorithm hash = algorithmFactory())
            {
                EmptyHash = new Id(hash.ComputeHash(new byte[0]));
            }
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
