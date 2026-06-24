using Concentus;
using Concentus.Enums;
using EncryptedUDPWalkieTalkie.Data;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace EncryptedUDPWalkieTalkie
{
    internal class VoiceSender
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private WasapiCapture _wasapiCap;

        public VoiceSender(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
        }

        public void StartBroadcasting()
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.EnableBroadcast = true;

            IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, 25565);

            _wasapiCap = new WasapiCapture();
            WaveFormat globalWave = new WaveFormat(48000, 16, 1);

            OpusCodecFactory.AttemptToUseNativeLibrary = true;
            var encoder = OpusCodecFactory.CreateEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP, Console.Out);
            encoder.Bitrate = 64000;

            _wasapiCap.DataAvailable += (sender, e) =>
            {
                byte[] data = CompressAudioToStandart(e.Buffer, 0, e.BytesRecorded, _wasapiCap.WaveFormat, globalWave);

                int cursor = 0;
                while (cursor < data.Length)
                {
                    var bytes = new byte[1000];
                    var frame = new byte[480 * 2];

                    if (cursor + frame.Length > data.Length) break;

                    Array.Copy(data, cursor, frame, 0, frame.Length);
                    cursor += frame.Length;

                    var frameAsShorts = BytesToShorts(frame, 0, frame.Length);

                    var encodedBytes = encoder.Encode(frameAsShorts, frameAsShorts.Length, bytes, bytes.Length);
                    var encoded = bytes.SkipLast(1000 - encodedBytes).ToArray();
                    var encrypted_AES = EncryptString(encoded, 0, encoded.Length, _key, _iv);

                    DataWithHmac dataWithHmac = new DataWithHmac(encrypted_AES, CreateHMAC_SHA256(_key, encrypted_AES));
                    var serializedData = JsonSerializer.SerializeToUtf8Bytes(dataWithHmac);

                    s.SendTo(serializedData, ep);
                }
            };

            _wasapiCap.StartRecording();
            Console.WriteLine("[Sender] Broadcasting started.");
        }

        public void StopBroadcasting()
        {
            _wasapiCap?.StopRecording();
            _wasapiCap?.Dispose();
            Console.WriteLine("[Sender] Broadcasting stopped.");
        }

        private static byte[] CreateHMAC_SHA256(byte[] key, byte[] message)
        {
            byte[] dest = new byte[2000];
            HMACSHA256.TryHashData(key, message, dest, out int bytesWritten);
            return dest.SkipLast(2000 - bytesWritten).ToArray();
        }

        private static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }
            return processedValues;
        }

        private static byte[] CompressAudioToStandart(byte[] byteStream, int offset, int count, WaveFormat inputFormat, WaveFormat targetFormat)
        {
            using (var memoryStream = new MemoryStream(byteStream, offset, count))
            using (var inputStream = new RawSourceWaveStream(memoryStream, inputFormat))
            using (var conversionStream = new MediaFoundationResampler(inputStream, targetFormat))
            {
                conversionStream.ResamplerQuality = 60;
                using (var outputStream = new MemoryStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = conversionStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        outputStream.Write(buffer, 0, bytesRead);
                    }
                    return outputStream.ToArray();
                }
            }
        }

        private static byte[] EncryptString(byte[] buffer, int offset, int count, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new BinaryWriter(cs))
                    {
                        sw.Write(buffer, offset, count);
                    }
                    return ms.ToArray();
                }
            }
        }
    }
}
