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
                var header = Encoding.UTF8.GetBytes("time(millis),shielded, unshielded\n");
                tempGeiger.Write(header,0,header.Length);
            }
            using (var tempAccel = new FileStream("accel.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),x,y,z\n");
                tempAccel.Write(header,0,header.Length);
            }
            using (var tempBNO = new FileStream("bno.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),gyro_x,gyro_y,gyro_z,accel_x,accel_y,accel_z,temp\n");
                tempBNO.Write(header, 0, header.Length);
            }
            using (var temptimeSync = new FileStream("timeSync.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),gyro_x,gyro_y,gyro_z,accel_x,accel_y,accel_z,temp\n");
                temptimeSync.Write(header, 0, header.Length);
            }

            Console.WriteLine("RockSat Data Parser, v.9");
            Console.WriteLine("Make sure data file is in same directory as this utility.");
            Console.WriteLine("Enter the full file name, or drag and drop file onto window and press enter. (For data.dat, enter \"data.dat\"");
            var filename = Console.ReadLine();

            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                fileStream.Seek(0, SeekOrigin.Begin); // make sure we're at the beginning of the file
                while (fileStream.Position != fileStream.Length)
                {
                    //first byte should be start packet (0xff)
                    if (fileStream.ReadByte() != 0xFF) {
                        Console.WriteLine("Error... expected data packet start, received something else...");
                        while (fileStream.ReadByte() != 0xFF && fileStream.Position != fileStream.Length) ;
                    }
                    //

                    //second byte is packet type
                    var packetType = fileStream.ReadByte();

                    //3rd and 4th are size
                    var sizeMsb = fileStream.ReadByte();
                    var sizeLsb = fileStream.ReadByte();
                    var dataSize = (sizeMsb << 8) + sizeLsb;


                    var dataContainer = new byte[dataSize]; //16 bytes for 2 time stamps.

                    for (var i = 0; i < dataSize; i++) {
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
            var startMillisBinary = new[] {data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7] };
            var startMillis = BitConverter.ToInt64(startMillisBinary,0);

            var startIndex = 8;
            switch (packetType) {
                case 0x02: //BNO

                    Console.Write("BNO packet found. Processing...");
                    using (var logFile = new FileStream("bno.csv", FileMode.Append))
                    {
                        
                        var currentline = startMillis + "," + data[startIndex] + "," + data[startIndex + 1] + "," + data[startIndex + 2] + "," + data[startIndex + 3] + "," + data[startIndex + 4] + "," + data[startIndex + 5] + "," + data[startIndex + 6] + "," + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x00: //GEIGER
                    Console.Write("Geiger packet found. Processing...");
                    using (var logFile = new FileStream("geiger.csv", FileMode.Append))
                    {
                        var currentline = startMillis + "," + data[startIndex] + "," + data[startIndex + 1] + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                        
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x01: //ACCELDUMP

                    var endMillisBinary = new[] { data[data.Length - 8], data[data.Length - 7], data[data.Length - 6], data[data.Length - 5], data[data.Length - 4], data[data.Length - 3], data[data.Length - 2], data[data.Length - 1]};
                    var endMillis = BitConverter.ToInt64(endMillisBinary, 0);
                    var delta = Math.Abs(endMillis - startMillis);
                    double step = (float)delta / dataSize;
                    double currentStep = startMillis;

                    Console.Write("Acceleration packet found. Processing...");
                    using (var logFile = new FileStream("accel.csv", FileMode.Append))
                    {
                        for (var i = startIndex; i < dataSize - startIndex; i += 6) //startIndex = size of time data, there's time data at the end, and datasize includes size of both timestamps
                        {
                            var currentX = (data[i] << 8) + data[i+1];
                            var currentY = (data[i+2] << 8) + data[i+3];
                            var currentZ = (data[i+4] << 8) + data[i+5];
                            var currentline = currentStep + "," + currentX + "," + currentY + "," + currentZ + "\n";
                            logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentStep += step;
                        }
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x03: // Time-sync
                    Console.Write("Time-sync packet found. Processing...");
                    using (var logFile = new FileStream("timeSync.csv", FileMode.Append))
                    {
                        var hours = data[0];
                        var minutes = data[1];
                        var seconds = data[2];

                        var millis = new[] {data[3], data[4], data[5], data[6], data[7], data[8], data[9], data[10]};

                        var e_hours = data[11];
                        var e_mins = data[12];
                        var e_sec = data[13];

                        var currentline = hours + ":" + minutes + ":" + seconds + "," + BitConverter.ToInt64(millis,0) + "," + e_hours + ":" + e_mins + ":" + e_sec;
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
            }
        }
    }
}
