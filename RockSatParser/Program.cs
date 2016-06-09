using System;
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

        private static int geigerSize = 2054;
        private static int timeSyncSize = 11;
        private static int accelSize = 18438;
        private static int bnoSize = 12;
        static void Main(string[] args)
        {
            //set up files with headers
            using (var tempGeiger = new FileStream("geiger.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),shielded, unshielded\n");
                tempGeiger.Write(header,0,header.Length);
            }
            using (var tempDebug = new FileStream("debug.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),message\n");
                tempDebug.Write(header,0,header.Length);
            }
            using (var tempAccel = new FileStream("accel.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),x,y,z\n");
                tempAccel.Write(header,0,header.Length);
            }
            using (var tempBno = new FileStream("bno.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),gyro_x,gyro_y,gyro_z,accel_x,accel_y,accel_z,temp\n");
                tempBno.Write(header, 0, header.Length);
            }
            using (var temptimeSync = new FileStream("timeSync.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(RTC),time(ms)\n");
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
                            dataSize = geigerSize;
                            break;
                        case 0x01:
                            dataSize = accelSize;
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

            var startIndex = 3;
            switch (packetType) {
                case 0x02: //BNO
                    var current = startIndex;
                    Console.Write("BNO packet found. Processing...");
                    using (var logFile = new FileStream("bno.csv", FileMode.Append))
                    {
                        var xNeg = data[current++] == 1 ? -1 : 1;
                        var accelX = ((data[current++] << 8) + data[current++])*xNeg;

                        var yNeg = data[current++] == 1 ? -1 : 1;
                        var accelY = (data[current++] << 8) + data[current++] *yNeg;

                        var zNeg = data[current++] == 1 ? -1 : 1;
                        var accelZ = (data[current++] << 8) + data[current] *zNeg;

                        var currentline = startMillis + "," + accelX/100f + "," + accelY/100f + "," + accelZ/100f + "\n";
                        //var currentline = startMillis + "," + data[startIndex] + "," + data[startIndex + 1] + "," + data[startIndex + 2] + "," + data[startIndex + 3] + "," + data[startIndex + 4] + "," + data[startIndex + 5] + "," + data[startIndex + 6] + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x00: //GEIGER
                    Console.Write("Geiger packet found. Processing...");
                    var geigerRawEndTime = new byte[] { data[data.Length - 3], data[data.Length - 2], data[data.Length - 1], 0, 0, 0, 0, 0 };
                    var geigerEndTime = BitConverter.ToInt64(geigerRawEndTime, 0);
                    var geigerDelta = Math.Abs(geigerEndTime - startMillis);

                    var geigerTimeStampSize = 6;
                    var geigerDataSize = dataSizewithTimeStamps - geigerTimeStampSize;
                    double geigerStep = geigerDelta / ((float)geigerDataSize / 2);
                    double currentgeigerStep = startMillis;
                    using (var logFile = new FileStream("geiger.csv", FileMode.Append))
                    {
                        for (int i = startIndex; i < dataSizewithTimeStamps - startIndex; i+=2)
                        {
                            var currentline = currentgeigerStep + "," + data[i] + "," + data[i+1] + "\n";
                            logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentgeigerStep += geigerStep;
                        }

                        //var currentline = startMillis + "," + data[3] + "," + data[4] + "\n";
                        
                        
                    }
                    Console.WriteLine(" finished.");
                    break;
                case 0x01: //ACCELDUMP

                    var accelEndTime = new byte[] {data[data.Length - 3], data[data.Length - 2], data[data.Length - 1], 0,0,0,0,0};
                    var endMillis = BitConverter.ToInt64(accelEndTime, 0);
                    var delta = Math.Abs(endMillis - startMillis);

                    var timeStampSizeCount = 6;
                    var actualSizeofAccelData = dataSizewithTimeStamps - timeStampSizeCount;

                    double step = delta / ((float)actualSizeofAccelData/3);
                    double currentStep = startMillis;

                    Console.Write("Acceleration packet found. Processing...");
                    using (var logFile = new FileStream("accel.csv", FileMode.Append))
                    {
                        for (var i = startIndex; i < dataSizewithTimeStamps - startIndex; i += 6) //startIndex = size of time data, there's time data at the end, and datasize includes size of both timestamps
                        {

                            var rawX = (data[i] << 8) + data[i+1];
                            var rawY = (data[i+2] << 8) + data[i+3];
                            var rawZ = (data[i+4] << 8) + data[i+5];
                            var currentX = map(rawX/1000f, 0, 1, -200, 200);
                            var currentY = map(rawY/1000f, 0, 1, -200, 200);
                            var currentZ = map(rawZ/1000f, 0, 1, -200, 200);

                            var currentline = currentStep + "," + currentX + "," + currentY + "," + currentZ + "\n";
                            logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentStep += step; // bug - happening 3x too often - fixed?
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
            }
        }
    }

 
}
