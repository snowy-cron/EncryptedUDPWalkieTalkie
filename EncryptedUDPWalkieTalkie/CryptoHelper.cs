using System.Security.Cryptography;

namespace EncryptedUDPWalkieTalkie
{
    /// <summary>
    /// Shared cryptographic utilities used by both <see cref="VoiceSender"/> and <see cref="VoiceReceiver"/>.
    /// </summary>
    internal static class CryptoHelper
    {
        internal static byte[] CreateHmacSha256(byte[] key, byte[] message)
            => HMACSHA256.HashData(key, message);
    }
}