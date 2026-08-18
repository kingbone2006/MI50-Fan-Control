using System;
using System.IO;

namespace FanDiag
{
    public class ExtractDriverFromInstaller
    {
        public static void Run()
        {
            string nsisPath = @"C:\Users\MI50\Downloads\ABDM\Programs\instspeedfan452.exe";
            if (!File.Exists(nsisPath))
            {
                Console.WriteLine("Installer not found");
                return;
            }

            byte[] bytes = File.ReadAllBytes(nsisPath);
            Console.WriteLine($"Read installer: {bytes.Length} bytes");

            // Look for embedded SYS file in the NSIS installer (MZ header of speedfan.sys)
            int sysCount = 0;
            for (int i = 0; i < bytes.Length - 1000; i++)
            {
                if (bytes[i] == 'M' && bytes[i + 1] == 'Z' && bytes[i + 2] == 0x90 && bytes[i + 3] == 0x00)
                {
                    int peOffset = BitConverter.ToInt32(bytes, i + 0x3C);
                    if (peOffset > 0 && peOffset < 500 && i + peOffset + 4 < bytes.Length)
                    {
                        if (bytes[i + peOffset] == 'P' && bytes[i + peOffset + 1] == 'E' && bytes[i + peOffset + 2] == 0 && bytes[i + peOffset + 3] == 0)
                        {
                            ushort machine = BitConverter.ToUInt16(bytes, i + peOffset + 4);
                            ushort numSections = BitConverter.ToUInt16(bytes, i + peOffset + 6);
                            ushort optHeaderSize = BitConverter.ToUInt16(bytes, i + peOffset + 20);

                            // Calculate image size
                            int optHeaderOffset = i + peOffset + 24;
                            uint sizeOfImage = BitConverter.ToUInt32(bytes, optHeaderOffset + 56);

                            Console.WriteLine($"Found PE at 0x{i:X} ({i}): Machine=0x{machine:X}, Sections={numSections}, SizeOfImage={sizeOfImage}");

                            // Find section headers to get total raw size
                            int sectionHeadersOffset = optHeaderOffset + optHeaderSize;
                            int totalFileSize = 0;
                            for (int sec = 0; sec < numSections; sec++)
                            {
                                int secOffset = sectionHeadersOffset + sec * 40;
                                uint rawSize = BitConverter.ToUInt32(bytes, secOffset + 16);
                                uint rawPointer = BitConverter.ToUInt32(bytes, secOffset + 20);
                                totalFileSize = Math.Max(totalFileSize, (int)(rawPointer + rawSize));
                            }

                            if (totalFileSize > 1000 && i + totalFileSize <= bytes.Length)
                            {
                                byte[] sysBytes = new byte[totalFileSize];
                                Array.Copy(bytes, i, sysBytes, 0, totalFileSize);

                                string outPath = $@"C:\Users\MI50\Desktop\fancontrol\src\MI50FanControl\Engine\driver_{sysCount}.sys";
                                File.WriteAllBytes(outPath, sysBytes);
                                Console.WriteLine($"  -> Extracted {totalFileSize} bytes to {outPath}");
                                sysCount++;
                            }
                        }
                    }
                }
            }
        }
    }
}
