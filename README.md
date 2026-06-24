# EncryptedUDPWalkieTalkie

A LAN walkie-talkie application for Windows that streams voice over UDP with AES encryption and HMAC message authentication. All users on the same local network share the channel — press to talk, and everyone hears you.

---

## How It Works

```
Microphone → WASAPI Capture → Resample to 48kHz mono
         → Opus Encode (64 kbps) → AES-256-CBC Encrypt
         → HMAC-SHA256 Sign → UDP Broadcast (:25565)

UDP Receive (:25565) → Verify HMAC-SHA256 → AES-256-CBC Decrypt
                    → Opus Decode → WASAPI Playback
```

Audio is captured from your default microphone, resampled to 48 kHz 16-bit mono, compressed with the Opus codec, encrypted with AES-256-CBC, signed with HMAC-SHA256, and broadcast over UDP to all devices on the local network. Receivers verify the HMAC before decrypting and playing back audio through the default output device.

---

## Features

- **Always-on broadcast** — no push-to-talk button; transmits continuously
- **LAN broadcast** — works automatically with all peers on the same subnet (no server or peer discovery needed)
- **AES-256-CBC encryption** — audio payload is encrypted before sending
- **HMAC-SHA256 authentication** — each packet is signed to detect tampering or replay from unknown sources
- **PBKDF2 key derivation** — the shared password is stretched via PBKDF2-SHA1 (10 000 iterations) into a 256-bit key
- **Opus audio codec** — low-latency VOIP-tuned compression at 64 kbps
- **10 ms audio frames** — 480 samples per frame at 48 kHz

---

## Requirements

- **OS:** Windows (uses WASAPI via NAudio)
- **Runtime:** .NET 6 or later
- **NuGet packages:**
  - [`NAudio`](https://github.com/naudio/NAudio) — WASAPI audio capture and playback
  - [`Concentus`](https://github.com/lostromb/concentus) — managed Opus encoder/decoder

---

## Getting Started

### 1. Clone and open

```bash
git clone <repo-url>
cd EncryptedUDPWalkieTalkie
```

Open the solution in Visual Studio or build from the CLI:

```bash
dotnet build
dotnet run
```

### 2. Set a shared password

All peers must use the **same password**. Open `Program.cs` and change the string in the `Pbkdf2` call:

```csharp
// Program.cs
Key = Rfc2898DeriveBytes.Pbkdf2("your-password-here", Salt, 10000, HashAlgorithmName.SHA1, 32);
```

> ⚠️ The Salt and IV are currently hardcoded constants. For a production deployment, generate random values and share them out-of-band alongside the password.

### 3. Run on each machine

Start the application on every computer that should participate in the channel. Each instance simultaneously sends and receives — there is no dedicated server.

```
Starting EncryptedUDPWalkieTalkie...
[Sender] Broadcasting started.
[Receiver] Listening on port 25565...
Press Enter to stop everything...
```

Press **Enter** to stop sending. The receiver continues in the background until the process exits.

---

## Configuration

All tunable constants live at the top of `Program.cs` and inside `VoiceSender.cs`.

| Setting | Location | Default | Notes |
|---|---|---|---|
| Password | `Program.cs` | `"hell"` | **Change this** before sharing builds |
| Salt | `Program.cs` | hardcoded 16 bytes | Change and share out-of-band |
| IV | `Program.cs` | hardcoded 16 bytes | Change and share out-of-band |
| UDP port | `VoiceReceiver.cs` / `VoiceSender.cs` | `25565` | Must match on all peers |
| Sample rate | both files | `48000 Hz` | Opus native rate; do not change |
| Channels | both files | `1` (mono) | |
| Bitrate | `VoiceSender.cs` | `64 000 bps` | Adjust via `encoder.Bitrate` |
| Frame size | `VoiceSender.cs` | `480 samples` (10 ms) | Valid Opus frame sizes: 120/240/480/960/1920/2880 |

---

## Security Notes

- Packets from peers using the wrong password are silently dropped — HMAC verification fails before decryption is attempted.
- The IV is **static and reused** across all packets. For stronger security, generate a random IV per packet and prepend it to the ciphertext.
- The hardcoded salt weakens PBKDF2 somewhat. A unique, random salt per deployment is recommended.
- UDP broadcast is limited to the local subnet. Traffic does not cross routers by default.
- There is no replay protection. An attacker on the LAN who records packets can retransmit them and they will be accepted.

---

## Project Structure

```
EncryptedUDPWalkieTalkie/
├── EncryptedUDPWalkieTalkie.slnx
└── EncryptedUDPWalkieTalkie/
    ├── EncryptedUDPWalkieTalkie.csproj
    ├── Program.cs          # Entry point; key derivation; starts sender and receiver
    ├── CryptoHelper.cs     # Shared HMAC-SHA256 utility
    ├── VoiceSender.cs      # Microphone capture, Opus encode, AES encrypt, UDP broadcast
    ├── VoiceReceiver.cs    # UDP receive, HMAC verify, AES decrypt, Opus decode, playback
    └── Models.cs           # DataWithHmac — the serialized packet structure
```

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
