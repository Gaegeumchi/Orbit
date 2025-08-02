using System;
using System.IO;
using System.Text;

namespace gaegeumchi.Orbit.Utils
{
    public static class PacketUtils
    {
        public static int ReadVarInt(Stream stream)
        {
            int numRead = 0;
            int result = 0;
            byte read;
            do
            {
                read = (byte)stream.ReadByte();
                int value = (read & 0b01111111);
                result |= (value << (7 * numRead));

                numRead++;
                if (numRead > 5)
                {
                    throw new IOException("VarInt is too big");
                }
            } while ((read & 0b10000000) != 0);

            return result;
        }

        public static string ReadString(Stream stream)
        {
            int length = ReadVarInt(stream);
            byte[] stringBytes = new byte[length];
            stream.Read(stringBytes, 0, length);
            return Encoding.UTF8.GetString(stringBytes);
        }

        public static ushort ReadUShort(Stream stream)
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static long ReadLong(Stream stream)
        {
            byte[] bytes = new byte[8];
            stream.Read(bytes, 0, 8);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt64(bytes, 0);
        }

        public static void WriteVarInt(Stream stream, int value)
        {
            while ((value & ~0x7F) != 0)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        public static void WriteString(Stream stream, string value)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            WriteVarInt(stream, stringBytes.Length);
            stream.Write(stringBytes, 0, stringBytes.Length);
        }

        public static void WriteLong(Stream stream, long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            stream.Write(bytes, 0, 8);
        }

        public static void WriteUuid(Stream stream, Guid uuid)
        {
            byte[] uuidBytes = uuid.ToByteArray();

            byte[] newUuidBytes = new byte[16];
            Array.Copy(uuidBytes, 0, newUuidBytes, 0, 4);
            Array.Reverse(newUuidBytes, 0, 4);

            Array.Copy(uuidBytes, 4, newUuidBytes, 4, 2);
            Array.Reverse(newUuidBytes, 4, 2);

            Array.Copy(uuidBytes, 6, newUuidBytes, 6, 2);
            Array.Reverse(newUuidBytes, 6, 2);

            Array.Copy(uuidBytes, 8, newUuidBytes, 8, 8);

            stream.Write(newUuidBytes, 0, 16);
        }
    }
}