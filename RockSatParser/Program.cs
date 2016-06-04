﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RockSatParser
{
    class Program
    {
        static void Main(string[] args)
        {

            //set up files with headers
            using (var tempGeiger = new FileStream("geiger.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(seconds),shielded, unshielded\n");
                tempGeiger.Write(header,0,header.Length);
            }
            using (var tempAccel = new FileStream("accel.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(seconds),x,y,z\n");
                tempAccel.Write(header,0,header.Length);
            }
            using (var tempBNO = new FileStream("bno.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(seconds),gyro_x,gyro_y,gyro_z,accel_x,accel_y,accel_z,temp\n");
                tempBNO.Write(header, 0, header.Length);
            }

            Console.WriteLine("RockSat Data Parser, v0.1");
            Console.WriteLine("Make sure data file is in same directory as this utility.");
            Console.WriteLine("Enter the full file name, including file extension. (For data.dat, enter \"data.dat\"");
            var filename = Console.ReadLine();

            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                fileStream.Seek(0, SeekOrigin.Begin); // make sure we're at the beginning of the file
                while (fileStream.Position != fileStream.Length)
                {
                    //first packet should be start packet (0xff)
                    if (fileStream.ReadByte() != 0xFF) {
                        Console.WriteLine("Error... expected data packet start, received something else...");
                        while (fileStream.ReadByte() != 0xFF && fileStream.Position != fileStream.Length) ;
                    }
                    //

                    var packetType = fileStream.ReadByte();

                    var size_msb = fileStream.ReadByte();
                    var size_lsb = fileStream.ReadByte();

                    var dataSize = (size_msb << 8) + size_lsb;

                    var dataContainer = new byte[dataSize + 6]; //6 bytes for 2 time stamps.

                    for (var i = 0; i < dataSize + 6; i++) {
                        dataContainer[i] = (byte)fileStream.ReadByte();
                    }

                    parseData(packetType, dataSize, dataContainer);
                }
            }

            Console.WriteLine("Finished! ");
            Console.ReadKey();
        }

        private static void parseData(int packetType,int dataSize, byte[] data)
        {
            

            var startHours = data[0];
            var startMin = data[1];
            var startSec = data[2];

            var endSec = data[data.Length - 1];
            var endMin = data[data.Length - 2];
            var endHour = data[data.Length - 3];

            var startSeconds = startHours*360 + startMin*60 + startSec;
            var endSeconds = endHour*360 + endMin*60 + endSec;
            var delta = Math.Abs(endSeconds - startSeconds);

            var step = delta/dataSize;

            var currentStep = startSeconds;

            switch (packetType) {
                case 0x02: //BNO

                    Console.Write("BNO packet found. Processing...");
                    using (var fileTest = new FileStream("bno.csv", FileMode.Append))
                    {
                        for (int i = 0; i < dataSize; i += 7)
                        {
                            
                            var currentline = currentStep.ToString() + "," + data[i + 3] + "," + data[i + 4] +","+ data[i + 5] + "," + data[i + 6] + "," + data[i + 7] + "," + data[i + 8] + "," + data[i + 9] + "," + "\n";
                            fileTest.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentStep += step;
                        }
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x00: //GEIGER
                    Console.Write("Geiger packet found. Processing...");
                    using (var fileTest = new FileStream("geiger.csv", FileMode.Append))
                    {
                        for (int i = 0; i < dataSize; i+=2)
                        {
                            var currentline = currentStep.ToString() + "," + data[i + 3] + "," + data[i + 4] + "\n";
                            fileTest.Write(Encoding.UTF8.GetBytes(currentline),0,currentline.Length);
                            currentStep += step;
                        }
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x01: //ACCELDUMP

                    Console.Write("Acceleration packet found. Processing...");
                    using (var fileTest = new FileStream("accel.csv", FileMode.Append))
                    {
                        for (int i = 0; i < dataSize; i += 3)
                        {
                            var currentline = currentStep.ToString() + "," + data[i + 3] + "," + data[i + 4] + "," + data[i + 5] + "\n";
                            fileTest.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentStep += step;
                        }
                    }
                    Console.WriteLine(" finished.");
                    break;
            }
        }
    }
}
