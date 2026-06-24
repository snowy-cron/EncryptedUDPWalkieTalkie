using System.Text.Json.Serialization;

namespace EncryptedUDPWalkieTalkie.Data
{
    internal class DataWithHmac
    {
        public byte[] data { get; set; }
        public byte[] hmac { get; set; }

        [JsonConstructor]
        public DataWithHmac(byte[] data, byte[] hmac)
        {
            this.data = data;
            this.hmac = hmac;
        }
    }
}
