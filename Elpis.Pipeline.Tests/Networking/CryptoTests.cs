using System;
using PandoraSharp;
using Xunit;

namespace Elpis.Pipeline.Tests.Networking
{
    public class CryptoTests
    {
        [Fact]
        public void DecryptSyncTime_returns_numeric_timestamp()
        {
            const long expected = 1_705_554_321L;
            var encrypted = Crypto.in_key.Encrypt("0000" + expected);

            var actual = Crypto.DecryptSyncTime(encrypted);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void DecryptSyncTime_throws_when_payload_missing_digits()
        {
            var encrypted = Crypto.in_key.Encrypt("0000abcd");

            Assert.Throws<FormatException>(() => Crypto.DecryptSyncTime(encrypted));
        }
    }
}
