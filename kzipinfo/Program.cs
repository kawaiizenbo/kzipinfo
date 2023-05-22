using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;

namespace kzipinfo
{
    internal class Program
    {
        readonly static Dictionary<byte, string> versionTable = new Dictionary<byte, string>()
        {
            { 0x01, "v1.0.x" }
        };
        readonly static Dictionary<byte, string> compressionTable = new Dictionary<byte, string>()
        {
            { 0x00, "Raw" },
            { 0x01, "RLE" },
            { 0x02, "Deflate" }
        };
        static void Main(string[] args)
        {
            MD5 md5_ = MD5.Create();
            string filename = args[0];
            byte[] archive = File.ReadAllBytes(filename);
            if (!($"{(char)archive[0]}{(char)archive[1]}{(char)archive[2]}{(char)archive[3]}" == "KZIP"))
            {
                Console.WriteLine("Invalid archive.");
                return;
            }
            string archiveVersion = versionTable[archive[4]];
            string compressionMode = compressionTable[archive[5]];
            ushort fileCount = (ushort)((archive[6] << 0xFF) + archive[7]);
            Console.WriteLine(
                $"Archive information:\n" +
                $"Archive version: {archiveVersion}\n" +
                $"Compression Mode: {compressionMode}\n" +
                $"File Count: {fileCount}\n"
            );
            int tocReaderOffset = 8;
            for ( int i = 1; i <= fileCount; i++ ) 
            {
                List<byte> offsetBad = new List<byte>();
                for ( int j = 0; j < 8; j++ ) 
                {
                    offsetBad.Add(archive[tocReaderOffset + j] );
                }
                ulong offset = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(offsetBad.ToArray(), 0));
                Console.WriteLine($"Offset: {offset}");
                tocReaderOffset += 8;
                List<byte> nameBad = new List<byte>();
                List<byte> directoryBad = new List<byte>();
                List<byte> timestampBad = new List<byte>();
                List<byte> crcBad = new List<byte>();
                List<byte> md5Bad = new List<byte>();
                List<byte> sizeBad = new List<byte>();
                List<byte> file = new List<byte>();
                for (int j = 0; j < 128; j++)
                {
                    nameBad.Add(archive[offset + (ulong)j]);
                }
                for (int j = 0; j < 128; j++)
                {
                    directoryBad.Add(archive[offset + 128 + (ulong)j]);
                }
                for (int j = 0; j < 4; j++)
                {
                    timestampBad.Add(archive[offset + 256 + (ulong)j]);
                }
                for (int j = 0; j < 4; j++)
                {
                    crcBad.Add(archive[offset + 260 + (ulong)j]);
                }
                crcBad.Reverse();
                for (int j = 0; j < 16; j++)
                {
                    md5Bad.Add(archive[offset + 264 + (ulong)j]);
                }
                for (int j = 0; j < 8; j++)
                {
                    sizeBad.Add(archive[offset + 280 + (ulong)j]);
                }
                ulong sizeBytes = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt64(sizeBad.ToArray(), 0));
                for (ulong j = 0; j <= sizeBytes; j++)
                {
                    file.Add(archive[offset + 288 + j]);
                }
                byte[] crcCalc = Crc32.Hash(file.ToArray());
                byte[] md5Calc = md5_.ComputeHash(file.ToArray());
                bool crc32Good = Enumerable.SequenceEqual(crcCalc, crcBad.ToArray());
                bool md5Good = Enumerable.SequenceEqual(md5Calc, md5Bad.ToArray());
                string fileName = System.Text.Encoding.UTF8.GetString(nameBad.ToArray()).Trim();
                string directory = System.Text.Encoding.UTF8.GetString(directoryBad.ToArray()).Trim();
                DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(timestampBad.ToArray(), 0)));
                string crc32 = BitConverter.ToString(crcBad.ToArray(), 0).Replace("-", "");
                string md5 = BitConverter.ToString(md5Bad.ToArray(), 0).Replace("-", "");
                string size = FormatBytes(sizeBytes);
                Console.WriteLine(
                    $"File Name: {fileName}\n" + 
                    $"{(directory == "" ? "" : $"Subdirectory: {directory}\n")}" + 
                    $"Timestamp: {timestamp.ToLocalTime()}\n" + 
                    $"CRC32: {crc32} ({crc32Good}, Calculated {BitConverter.ToString(crcCalc, 0).Replace("-", "")})\n" +
                    $"MD5: {md5} ({md5Good}, Calculated {BitConverter.ToString(md5Calc, 0).Replace("-", "")})\n" +
                    $"File Size: {size}\n"
                );
            }
        }

        public static string FormatBytes(ulong bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1000; i++, bytes /= 1000)
            {
                dblSByte = bytes / 1000.0;
            }

            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }
    }
}
