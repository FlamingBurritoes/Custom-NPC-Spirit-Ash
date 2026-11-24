using System.Numerics;
using System.Security.Cryptography;
using static SpiritCreator.RegulationBinary;

namespace SpiritCreator;

internal sealed class StockMessageArchive : IDisposable
{
    // Public Data0 archive format key, also documented by UXM.
    private const string PublicKey = """
        -----BEGIN RSA PUBLIC KEY-----
        MIIBCwKCAQEA9Rju2whruXDVQZpfylVEPeNxm7XgMHcDyaaRUIpXQE0qEo+6Y36L
        P0xpFvL0H0kKxHwpuISsdgrnMHJ/yj4S61MWzhO8y4BQbw/zJehhDSRCecFJmFBz
        3I2JC5FCjoK+82xd9xM5XXdfsdBzRiSghuIHL4qk2WZ/0f/nK5VygeWXn/oLeYBL
        jX1S8wSSASza64JXjt0bP/i6mpV2SLZqKRxo7x2bIQrR1yHNekSF2jBhZIgcbtMB
        xjCywn+7p954wjcfjxB5VWaZ4hGbKhi1bhYPccht4XnGhcUTWO3NmJWslwccjQ4k
        sutLq3uRjLMM0IeTkQO6Pv8/R7UNFtdCWwIERzH8IQ==
        -----END RSA PUBLIC KEY-----
        """;

    private readonly byte[] _header;
    private readonly FileStream _data;
    private readonly string _archive;

    internal StockMessageArchive(string folder) : this(folder, "Data0", PublicKey, 1107200,
        "0686D2B966774A63772347D49EE84CE63C98A528865F6FCA783023BB1D6059D7") { }

    private StockMessageArchive(string folder, string archive, string publicKey, int headerSize, string headerHash)
    {
        _archive = archive;
        var path = Path.Combine(folder, archive + ".bhd");
        var data = Path.Combine(folder, archive + ".bdt");
        Require(File.Exists(path) && File.Exists(data),
            $"Missing {archive} archive. Locate regulation.bin in the installed Elden Ring Game folder.");
        using var header = File.OpenRead(path);
        Require(header.Length == headerSize, $"Unexpected Steam 1.17.3 {archive} archive header.");
        var encrypted = new byte[header.Length];
        header.ReadExactly(encrypted);
        Require(Convert.ToHexString(SHA256.HashData(encrypted)) == headerHash,
            $"Unexpected Steam 1.17.3 {archive} archive header.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKey);
        var key = rsa.ExportParameters(false);
        var modulus = new BigInteger(key.Modulus!, true, true);
        var exponent = new BigInteger(key.Exponent!, true, true);
        const int block = 256, rawBlock = block - 1;
        _header = new byte[encrypted.Length / block * rawBlock];
        for (var i = 0; i < encrypted.Length / block; i++)
        {
            var value = new BigInteger(encrypted.AsSpan(i * block, block), true, true);
            var decoded = BigInteger.ModPow(value, exponent, modulus).ToByteArray(true, true);
            decoded.CopyTo(_header, (i + 1) * rawBlock - decoded.Length);
        }
        Require(_header.AsSpan(0, 4).SequenceEqual("BHD5"u8) && _header[4] == 255,
            $"Invalid {archive} archive header.");
        _data = File.OpenRead(data);
    }

    internal byte[] ReadEnglishMessage(string file) => ReadAsset("/msg/engus/" + file, 4_000_000);

    internal byte[] ReadAsset(string path, int maxBytes) => TryReadAsset(path, maxBytes)
        ?? throw new InvalidDataException($"Couldn't find {path} in the stock {_archive} archive.");

    internal byte[]? TryReadAsset(string path, int maxBytes)
    {
        ulong hash = 0;
        foreach (var c in path)
            hash = unchecked(hash * 133 + c);
        // BHD5 buckets are indexed by the path hash modulo bucket count.
        var at = I32(_header, 20) + (int)(hash % (uint)I32(_header, 16)) * 8;
        for (int i = 0, entries = I32(_header, at), offset = I32(_header, at + 4); i < entries; i++)
        {
            var pos = offset + 40 * i;
            if (U64(_header, pos) != hash)
                continue;
            var size = I32(_header, pos + 8);
            Require(size >= 76 && size <= maxBytes, $"Invalid asset size in {_archive}.");
            var bytes = new byte[size];
            _data.Position = checked((long)U64(_header, pos + 16));
            _data.ReadExactly(bytes);
            var crypto = Offset(U64(_header, pos + 32), _header.Length);
            if (crypto == 0)
                return bytes;
            using var aes = Aes.Create();
            aes.Key = _header.AsSpan(crypto, 16).ToArray();
            for (int range = 0, ranges = I32(_header, crypto + 16); range < ranges; range++)
            {
                var p = crypto + 20 + 16 * range;
                var lo = U64(_header, p);
                var hi = U64(_header, p + 8);
                if (lo == ulong.MaxValue || hi == ulong.MaxValue || lo == hi)
                    continue;
                var span = bytes.AsSpan(Offset(lo, size), Offset(hi, size) - Offset(lo, size));
                aes.DecryptEcb(span, span, PaddingMode.None);
            }
            return bytes;
        }
        return null;
    }

    public void Dispose() => _data.Dispose();
}
