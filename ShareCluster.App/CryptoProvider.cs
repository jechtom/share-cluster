using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ShareCluster
{
    public class CryptoProvider
    {
        RandomNumberGenerator randomGenerator;
        Func<HashAlgorithm> hashAlgFactory;
        HashAlgorithm internalAlg;
        
        public CryptoProvider(Func<HashAlgorithm> algorithmFactory)
        {
            randomGenerator = new RNGCryptoServiceProvider();
            hashAlgFactory = algorithmFactory ?? throw new ArgumentNullException(nameof(algorithmFactory));
            internalAlg = algorithmFactory();
            BytesLength = (internalAlg.HashSize + 7) / 8;

            using (var hash = algorithmFactory())
            {
                EmptyHash = new Hash(hash.ComputeHash(new byte[0]));
            }
        }

        public HashAlgorithm CreateHashAlgorithm() => hashAlgFactory();

        public int BytesLength { get; }
        public Hash EmptyHash { get; }

        public Hash CreateRandom()
        {
            var result = new byte[BytesLength];
            randomGenerator.GetBytes(result);
            return new Hash(result);
        }
    }
}
