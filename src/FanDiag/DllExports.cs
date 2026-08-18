using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

namespace FanDiag
{
    public class DllExports
    {
        public static void ListExports(string dllPath)
        {
            Console.WriteLine($"\n--- EXPORTS of {dllPath} ---");
            if (!File.Exists(dllPath))
            {
                Console.WriteLine("File not found.");
                return;
            }

            using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
            using var pe = new PEReader(fs);
            var headers = pe.PEHeaders;
            var exportDir = headers.PEHeader.ExportTableDirectory;
            if (exportDir.RelativeVirtualAddress == 0)
            {
                Console.WriteLine("No export table.");
                return;
            }

            // Let's read export section
            int rva = exportDir.RelativeVirtualAddress;
            int offset = RvaToOffset(headers, rva);
            if (offset < 0) return;

            fs.Seek(offset, SeekOrigin.Begin);
            using var reader = new BinaryReader(fs);
            reader.ReadBytes(12); // characteristics, timeDateStamp, majorVersion, minorVersion
            int nameRva = reader.ReadInt32();
            int ordinalBase = reader.ReadInt32();
            int numberOfFunctions = reader.ReadInt32();
            int numberOfNames = reader.ReadInt32();
            int addressOfFunctionsRva = reader.ReadInt32();
            int addressOfNamesRva = reader.ReadInt32();
            int addressOfNameOrdinalsRva = reader.ReadInt32();

            int namesOffset = RvaToOffset(headers, addressOfNamesRva);
            int ordinalsOffset = RvaToOffset(headers, addressOfNameOrdinalsRva);

            List<string> exports = new List<string>();
            for (int i = 0; i < numberOfNames; i++)
            {
                fs.Seek(namesOffset + i * 4, SeekOrigin.Begin);
                int funcNameRva = reader.ReadInt32();
                int funcNameOffset = RvaToOffset(headers, funcNameRva);
                if (funcNameOffset > 0)
                {
                    fs.Seek(funcNameOffset, SeekOrigin.Begin);
                    List<byte> bytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != 0)
                    {
                        bytes.Add(b);
                    }
                    string name = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
                    exports.Add(name);
                }
            }

            Console.WriteLine($"Total exports: {exports.Count}");
            foreach (var name in exports)
            {
                if (name.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("PMLog", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Overdrive", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Sensor", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Adapter", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  {name}");
                }
            }
        }

        private static int RvaToOffset(PEHeaders headers, int rva)
        {
            foreach (var section in headers.SectionHeaders)
            {
                if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.VirtualSize)
                {
                    return section.PointerToRawData + (rva - section.VirtualAddress);
                }
            }
            return -1;
        }
    }
}
