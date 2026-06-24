using Concentus;
using EncryptedUDPWalkieTalkie.Data;
using NAudio.Wave;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace EncryptedUDPWalkieTalkie
{
    internal class VoiceReceiver
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public VoiceReceiver(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
        }

        public void StartListening()
        {
            var wasapiOUT = new WasapiOut();
            var globalWave = new WaveFormat(48000, 16, 1);
            var buffer = new BufferedWaveProvider(globalWave);
            wasapiOUT.Init(buffer);

            int listenPort = 25565;
            UdpClient listener = new UdpClient(listenPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, listenPort);

            wasapiOUT.Play();

            OpusCodecFactory.AttemptToUseNativeLibrary = true;
            var opusDecoder = OpusCodecFactory.CreateDecoder(48000, 1, Console.Out);

            Console.WriteLine($"[Receiver] Listening on port {listenPort}...");

            while (true)
            {
                try
                {
                    var info_encoded_AES = listener.Receive(ref groupEP);

                    var formatted = JsonSerializer.Deserialize<DataWithHmac>(info_encoded_AES);
                    if (formatted == null) continue;

                    if (!VerifyHMAC_SHA256(_key, formatted.data, formatted.hmac)) continue;

                    var info = DecryptString(_key, _iv, formatted.data);

                    var shorts = new short[globalWave.SampleRate / 1000 * 10 * globalWave.Channels];
                    var decodedBytes = opusDecoder.Decode(info, shorts, shorts.Length);

                    var decodedShorts = shorts.SkipLast(shorts.Length - decodedBytes).ToArray();
                    var decoded = ShortsToBytes(decodedShorts, 0, decodedShorts.Length);

                    buffer.AddSamples(decoded, 0, decoded.Length);
                }
                catch (Exception) { }
            }
        }

        private static byte[] CreateHMAC_SHA256(byte[] key, byte[] message)
        {
            byte[] dest = new byte[2000];
            HMACSHA256.TryHashData(key, message, dest, out int bytesWritten);
            return dest.SkipLast(2000 - bytesWritten).ToArray();
        }

        private static bool VerifyHMAC_SHA256(byte[] key, byte[] message, byte[] new_hmac)
        {
            var old_hmac = CreateHMAC_SHA256(key, message);
            return old_hmac.SequenceEqual(new_hmac);
        }

        private static byte[] DecryptString(byte[] key, byte[] iv, byte[] cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream(cipherText))
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new BinaryReader(cs))
                {
                    return sr.ReadBytes((int)ms.Length);
                }
            }
        }

        private static byte[] ShortsToBytes(short[] input, int offset, int length)
        {
            byte[] processedValues = new byte[length * 2];
            for (int c = 0; c < length; c++)
            {
                processedValues[c * 2] = (byte)(input[c + offset] & 0xFF);
                processedValues[c * 2 + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }
            return processedValues;
        }
    }
}
