using System.Security.Cryptography;

namespace EncryptedUDPWalkieTalkie
{
    internal class Program
    {
        public static readonly byte[] Salt = { 1, 1, 0, 2, 0, 1, 2, 4, 4, 2, 1, 0, 2, 0, 1, 1 };
        public static readonly byte[] Iv = { 5, 1, 0, 99, 0, 1, 255, 8, 20, 6, 1, 0, 10, 0, 7, 255 };
        public static byte[]? Key;

        static void Main(string[] args)
        {
            Key = Rfc2898DeriveBytes.Pbkdf2("hell", Salt, 10000, HashAlgorithmName.SHA1, 32);

            Console.WriteLine("Starting EncryptedUDPWalkieTalkie...");

            Task.Run(() =>
            {
                var receiver = new VoiceReceiver(Key, Iv);
                receiver.StartListening();
            });

            var sender = new VoiceSender(Key, Iv);
            sender.StartBroadcasting();

            Console.WriteLine("Press Enter to stop everything...");
            Console.ReadLine();

            sender.StopBroadcasting();
        }
    }
}
