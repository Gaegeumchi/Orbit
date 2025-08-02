using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using gaegeumchi.Orbit.Utils;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace gaegeumchi.Orbit.Server
{
    public class Server
    {
        private TcpListener listener;
        private int port;
        private bool isRunning;

        private const int SupportedProtocolVersion = 771;
        private const string ServerVersionName = "1.20.6";

        public Server(int port)
        {
            this.port = port;
            this.isRunning = false;
        }

        public void Start()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            isRunning = true;
            Console.WriteLine($"Orbit Server started on port {port}. Waiting for connections...");

            while (isRunning)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Socket error occurred: {ex.Message}");
                    break;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                Console.WriteLine($"New client connected from {client.Client.RemoteEndPoint}");
                NetworkStream stream = client.GetStream();

                int packetLength = PacketUtils.ReadVarInt(stream);
                int packetId = PacketUtils.ReadVarInt(stream);

                if (packetId == 0x00) // Handshake 패킷
                {
                    int protocolVersion = PacketUtils.ReadVarInt(stream);
                    string serverAddress = PacketUtils.ReadString(stream);
                    ushort serverPort = PacketUtils.ReadUShort(stream);
                    int nextState = PacketUtils.ReadVarInt(stream);

                    Console.WriteLine($"[Handshake] Protocol Version: {protocolVersion}, Server Address: {serverAddress}, Next State: {nextState}");

                    if (protocolVersion != SupportedProtocolVersion)
                    {
                        Console.WriteLine($"[Handshake] Protocol version mismatch. Client: {protocolVersion}, Server: {SupportedProtocolVersion}. Disconnecting...");
                    }
                    else
                    {
                        if (nextState == 1) // Status 요청
                        {
                            HandleStatusRequest(stream);
                        }
                        else if (nextState == 2) // Login 요청
                        {
                            HandleLoginRequest(stream);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[Handshake] Invalid first packet. Expected ID 0x00, but got 0x{packetId:X2}. Disconnecting...");
                }
            }
            catch (Exception ex)
            {
                string endpoint = client?.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
                Console.WriteLine($"Error handling client {endpoint}: {ex.Message}");
            }
            finally
            {
                if (client != null)
                {
                    try
                    {
                        Console.WriteLine($"Client disconnected from {client.Client.RemoteEndPoint}");
                    }
                    catch
                    {
                        Console.WriteLine("Client disconnected.");
                    }
                    client.Close();
                }
            }
        }

        private void HandleStatusRequest(NetworkStream stream)
        {
            Console.WriteLine("Client requested server status.");

            int packetLength = PacketUtils.ReadVarInt(stream);
            int packetId = PacketUtils.ReadVarInt(stream);

            if (packetId == 0x00)
            {
                string jsonResponse = GetStatusResponseJson();

                using (MemoryStream buffer = new MemoryStream())
                {
                    PacketUtils.WriteVarInt(buffer, 0x00);
                    PacketUtils.WriteString(buffer, jsonResponse);

                    int packetBodyLength = (int)buffer.Length;

                    using (MemoryStream finalPacket = new MemoryStream())
                    {
                        PacketUtils.WriteVarInt(finalPacket, packetBodyLength);
                        finalPacket.Write(buffer.ToArray(), 0, packetBodyLength);
                        stream.Write(finalPacket.ToArray(), 0, (int)finalPacket.Length);
                    }
                }

                packetLength = PacketUtils.ReadVarInt(stream);
                packetId = PacketUtils.ReadVarInt(stream);

                if (packetId == 0x01)
                {
                    long payload = PacketUtils.ReadLong(stream);
                    Console.WriteLine($"[Ping] Received ping request with payload: {payload}");

                    using (MemoryStream buffer = new MemoryStream())
                    {
                        PacketUtils.WriteVarInt(buffer, 0x01);
                        PacketUtils.WriteLong(buffer, payload);

                        int packetBodyLength = (int)buffer.Length;

                        using (MemoryStream finalPacket = new MemoryStream())
                        {
                            PacketUtils.WriteVarInt(finalPacket, packetBodyLength);
                            finalPacket.Write(buffer.ToArray(), 0, packetBodyLength);
                            stream.Write(finalPacket.ToArray(), 0, (int)finalPacket.Length);
                        }
                    }
                }
            }
        }

        private void HandleLoginRequest(NetworkStream stream)
        {
            Console.WriteLine("Client requested to log in. Handling login...");

            int packetLength = PacketUtils.ReadVarInt(stream);
            int packetId = PacketUtils.ReadVarInt(stream);

            if (packetId == 0x00)
            {
                string playerName = PacketUtils.ReadString(stream);
                Console.WriteLine($"[Login] Player '{playerName}' is trying to log in.");

                Guid playerUuid = GetOfflinePlayerUuid(playerName);
                Console.WriteLine($"[Login] Generated offline UUID for '{playerName}': {playerUuid}");

                using (MemoryStream buffer = new MemoryStream())
                {
                    PacketUtils.WriteVarInt(buffer, 0x02);
                    PacketUtils.WriteUuid(buffer, playerUuid);
                    PacketUtils.WriteString(buffer, playerName);

                    int packetBodyLength = (int)buffer.Length;

                    using (MemoryStream finalPacket = new MemoryStream())
                    {
                        PacketUtils.WriteVarInt(finalPacket, packetBodyLength);
                        finalPacket.Write(buffer.ToArray(), 0, packetBodyLength);
                        stream.Write(finalPacket.ToArray(), 0, (int)finalPacket.Length);
                    }
                }

                Console.WriteLine($"[Login] Successfully sent Login Success packet to '{playerName}'.");

                packetLength = PacketUtils.ReadVarInt(stream);
                packetId = PacketUtils.ReadVarInt(stream);

                if (packetId == 0x03)
                {
                    Console.WriteLine($"[Login] Player '{playerName}' successfully logged in. Switching to Play state.");
                }
                else
                {
                    Console.WriteLine($"[Login] Expected Login Finished packet, but got 0x{packetId:X2}.");
                }
            }
            else
            {
                Console.WriteLine($"[Login] Invalid packet during login. Expected ID 0x00, got 0x{packetId:X2}.");
            }
        }

        private Guid GetOfflinePlayerUuid(string username)
        {
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes($"OfflinePlayer:{username}"));

                hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
                hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

                byte[] uuidBytes = new byte[16];
                Buffer.BlockCopy(hash, 0, uuidBytes, 0, 16);
                return new Guid(uuidBytes);
            }
        }

        private string GetStatusResponseJson()
        {
            var response = new
            {
                version = new
                {
                    name = ServerVersionName,
                    protocol = SupportedProtocolVersion
                },
                players = new
                {
                    max = 100,
                    online = 0,
                    sample = new[]
                    {
                        new { name = "gaegeumchi", id = "4566e69f-c907-48ee-8d71-d7ba5aa36881" }
                    }
                },
                description = new
                {
                    text = "Hello, Orbit Server!"
                },
                favicon = ""
            };

            return JsonConvert.SerializeObject(response);
        }
    }
}