﻿using System;
using System.Collections.Generic;
using System.Data;
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
        
        


        public static float map(float original, float fromLo, float fromHi, float toLow, float toHigh)
        {
            return (original - fromLo)*((toHigh - toLow)/(fromHi - fromLo)) + toLow;
        }
        //data + time?
        private static int bmpSize = 16; //yep
        private static int timeSyncSize = 11; //unused for demosat
        private static int custMagSize = 18438; //yep
        private static int bnoSize = 61; //yep
        private static int eMagSize = 19; //yep
        static void Main(string[] args)
        {
            //set up files with headers
            using (var tempCMag = new FileStream("cmag.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),AnalogRead\n");
                tempCMag.Write(header,0,header.Length);
            }
            using (var tempEMag = new FileStream("emag.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),Mx, My, Mz, T\n");
                tempEMag.Write(header,0,header.Length);
            }
            using (var tempDebug = new FileStream("debug.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),message\n");
                tempDebug.Write(header,0,header.Length);
            }
            using (var tempBmp = new FileStream("bmp.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),pressure,temp,alt\n");
                tempBmp.Write(header,0,header.Length);
            }
            using (var tempBno = new FileStream("bno.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),accX,accY,accZ,gravX,gravY,gravZ,linAccX,linAccY,linAccZ,gyroX,gyroY,gyroZ,magX,magY,magZ,eulerX,eulerY,eulerZ,sysCalib,gyrCalib,accCalib,magCalib\n");
                tempBno.Write(header, 0, header.Length);
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
                    if (fileStream.ReadByte() != 0xFF)
                    {
                        Console.WriteLine("Error... expected data packet start, received something else...");
                        while (fileStream.ReadByte() != 0xFF && fileStream.Position != fileStream.Length) ;
                    }
                    //

                    //second byte is packet type
                    var packetType = fileStream.ReadByte();
                    var dataSize = 0;
                    switch (packetType)
                    {
                        case 0x00:
                            dataSize = custMagSize;
                            break;
                        case 0x01:
                            dataSize = bmpSize;
                            break;
                        case 0x02:
                            dataSize = bnoSize;
                            break;
                        case 0x03:
                            dataSize = timeSyncSize;
                            break;
                        case 0x04:
                            var sizeMsb = fileStream.ReadByte();
                            var sizeLsb = fileStream.ReadByte();
                            dataSize = (sizeMsb << 8) + sizeLsb;
                            break;
                        case 0x05:
                            dataSize = eMagSize;
                            break;
                    }
                    //3rd and 4th are size
                    //var sizeMsb = fileStream.ReadByte();
                    //var sizeLsb = fileStream.ReadByte();
                    //var dataSize = (sizeMsb << 8) + sizeLsb;



                    var dataContainer = new byte[dataSize]; 

                    for (var i = 0; i < dataSize; i++)
                    {
                        dataContainer[i] = (byte)fileStream.ReadByte();
                    }

                    ParseData(packetType, dataSize, dataContainer);
                }
                
            }

            Console.WriteLine("Finished! ");
            Console.ReadKey();
        }

        private static void ParseData(int packetType,int dataSizewithTimeStamps, byte[] data)
        {

            //var startMillisBinary = new[] {data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7] };
            //var startMillis = BitConverter.ToInt64(startMillisBinary,0);


            var newStartMillisBinary = new byte[] { data[0], data[1], data[2], 0, 0, 0, 0, 0 };
            var startMillis = BitConverter.ToInt64(newStartMillisBinary, 0);

            var startIndex = 3; //3 is where data starts... 0,1,2 have time data.
            switch (packetType) {
                case 0x02: //BNO
                    var current = startIndex;
                    Console.Write("BNO packet found. Size: + " + data.Length + ". Processing...");
                    using (var logFile = new FileStream("bno.csv", FileMode.Append))
                    {
                        //0 time 1 time 2 time 3 data
                            var AccelxNeg = data[current++] == 1 ? -1 : 1;
                            var AccelvecX = ((data[current++] << 8) + data[current++])* AccelxNeg;
                            var AccelyNeg = data[current++] == 1 ? -1 : 1;
                            var AccelvecY = (data[current++] << 8) + data[current++] * AccelyNeg;
                            var AccelzNeg = data[current++] == 1 ? -1 : 1;
                            var AccelvecZ = (data[current++] << 8) + data[current++] * AccelzNeg;
                        //0 time 1 time 2 time 3 data
                            var GravxNeg = data[current++] == 1 ? -1 : 1;
                            var GravvecX = ((data[current++] << 8) + data[current++])* GravxNeg;
                            var GravyNeg = data[current++] == 1 ? -1 : 1;
                            var GravvecY = (data[current++] << 8) + data[current++] * GravyNeg;
                            var GravzNeg = data[current++] == 1 ? -1 : 1;
                            var GravvecZ = (data[current++] << 8) + data[current++] * GravzNeg;
                        //0 time 1 time 2 time 3 data
                            var LinearxNeg = data[current++] == 1 ? -1 : 1;
                            var LinearvecX = ((data[current++] << 8) + data[current++])* LinearxNeg;
                            var LinearyNeg = data[current++] == 1 ? -1 : 1;
                            var LinearvecY = (data[current++] << 8) + data[current++] * LinearyNeg;
                            var LinearzNeg = data[current++] == 1 ? -1 : 1;
                            var LinearvecZ = (data[current++] << 8) + data[current++] * LinearzNeg;
                        //0 time 1 time 2 time 3 data
                            var GyroxNeg = data[current++] == 1 ? -1 : 1;
                            var GyrovecX = ((data[current++] << 8) + data[current++])* GyroxNeg;
                            var GyroyNeg = data[current++] == 1 ? -1 : 1;
                            var GyrovecY = (data[current++] << 8) + data[current++] * GyroyNeg;
                            var GyrozNeg = data[current++] == 1 ? -1 : 1;
                            var GyrovecZ = (data[current++] << 8) + data[current++] * GyrozNeg;
                        //0 time 1 time 2 time 3 data
                            var MagxNeg = data[current++] == 1 ? -1 : 1;
                            var MagvecX = ((data[current++] << 8) + data[current++])* MagxNeg;
                            var MagyNeg = data[current++] == 1 ? -1 : 1;
                            var MagvecY = (data[current++] << 8) + data[current++] * MagyNeg;
                            var MagzNeg = data[current++] == 1 ? -1 : 1;
                            var MagvecZ = (data[current++] << 8) + data[current++] * MagzNeg;
                        //0 time 1 time 2 time 3 data
                            var EulerxNeg = data[current++] == 1 ? -1 : 1;
                            var EulervecX = ((data[current++] << 8) + data[current++])* EulerxNeg;
                            var EuleryNeg = data[current++] == 1 ? -1 : 1;
                            var EulervecY = (data[current++] << 8) + data[current++] * EuleryNeg;
                            var EulerzNeg = data[current++] == 1 ? -1 : 1;
                            var EulervecZ = (data[current++] << 8) + data[current++] * EulerzNeg;

                        var sysCalib = data[current++];
                        var gyrCalib = data[current++];
                        var accCalib = data[current++];
                        var magCalib = data[current];

                        var currentline = startMillis + "," + AccelvecX/100f + "," + AccelvecY/100f + "," +
                                          AccelvecZ/100f + "\n"
                                          + "," + GravvecX/100f + "," + GravvecY/100f + "," + GravvecZ/100f
                                          + "," + LinearvecX/100f + "," + LinearvecY/100f + "," + LinearvecZ/100f
                                          + "," + GyrovecX/100f + "," + GyrovecY/100f + "," + GyrovecZ/100f
                                          + "," + MagvecX/100f + "," + MagvecY/100f + "," + MagvecZ/100f
                                          + "," + EulervecX/100f + "," + EulervecY/100f + "," + EulervecZ/100f
                                          + "," + sysCalib + "," + gyrCalib + "," + accCalib + "," + magCalib + "\n";  

                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x00: //Custom Mag Dump
                    Console.Write("Custom Mag packet found. Size: " + data.Length + ". Processing...");
                    var magRawEndTime = new byte[] { data[data.Length - 3], data[data.Length - 2], data[data.Length - 1], 0, 0, 0, 0, 0 };
                    var magEndTime = BitConverter.ToInt64(magRawEndTime, 0);
                    var magDelta = Math.Abs(magEndTime - startMillis);

                    var magTimeStampSize = 6;
                    var magDataSize = dataSizewithTimeStamps - magTimeStampSize;
                    double magStep = magDelta / ((float)magDataSize / 2); // 2bytes each vector, 1 vectors
                    double currentMagStep = startMillis;
                    using (var logFile = new FileStream("cmag.csv", FileMode.Append)) {
                        for (var i = startIndex; i < dataSizewithTimeStamps - startIndex; i+=2) {
                            var magVec = ((data[i] << 8) + data[i+1]);
                            var currentline = currentMagStep + "," + magVec + "\n"; //currentMagStep holds time
                            logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentMagStep += magStep;
                        }
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x01: //BMP Dump
                    Console.Write("BMP Packet found... Size: " + data.Length + ". Processing...");
                    using (var logFile = new FileStream("bmp.csv", FileMode.Append)) {
                        var pressure = new byte[8];
                        current = startIndex;
                        for (int i = 0; i < pressure.Length; i++) {
                            pressure[i] = data[current++];
                        }
                        var actualPressure = BitConverter.ToDouble(pressure, 0);


                        var tempNeg = data[current++] == 1 ? -1 : 1;
                        var bmpTemp = ((data[current++] << 8) + data[current++])*tempNeg;
                        var altitude = ((data[current++] << 8) + data[current]);

                        var currentLine = startMillis + "," + actualPressure + "," + bmpTemp + "," + altitude + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentLine), 0, currentLine.Length);

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

                        var millis = new byte[] {data[3], data[4], data[5],0,0,0,0,0};

                        var currentline = hours + ":" + minutes + ":" + seconds + "," + BitConverter.ToInt64(millis,0) + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x04: // Debug
                    Console.Write("Debug message found. Processing...");
                    using (var logFile = new FileStream("debug.csv", FileMode.Append))
                    {
                        var size = dataSizewithTimeStamps - 3;
                        var millis = new byte[] {data[0], data[1], data[2],0,0,0,0,0};
                        var message = new byte[size];
                        for (int i = 0; i < size; i++) {
                            message[i] = data[i + 3];
                        }

                        var currentline =BitConverter.ToInt64(millis,0) +"," + Encoding.UTF8.GetString(message) + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x05: // Debug
                    Console.Write("Expensive Mag Packet found. Processing...");
                    using (var logFile = new FileStream("emag.csv", FileMode.Append)) {
                        var size = dataSizewithTimeStamps - 3; //should be 16!

                        var dataStream = new byte[size];
                        //data[0] = echo... data[1] = SOT
                        //data[2] <<8 + data[3] = MX
                        var mX = ((short)((data[3] << 8) + data[4])) / 1000.0;
                        var mY = ((short)((data[5] << 8) + data[6])) / 1000.0;
                        var mZ = ((short)((data[7] << 8) + data[8])) / 1000.0;

                        var temp = ((short)((data[9] << 8) + data[10])) / 100.0;

                        //data 10-11 ANA1, 12 status, 13 checksum, 14-15 EOT

                        var currentLine = startMillis + "," + mX + "," + mY + "," + mZ + "," + temp + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentLine),0,currentLine.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
            }
        }
    }

 
}
