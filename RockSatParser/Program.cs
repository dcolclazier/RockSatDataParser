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
    enum PacketType {
        CustomMagnetometer = 0x00,
        BMPSensorUpdate = 0x01,
        BNOSensorUpdate = 0x02,
        TimeUpdate = 0x03,
        DebugMessage = 0x04,
        ExpensiveMagUpdate = 0x05,
        BatteryUpdate = 0x06,
        HeaterUpdate = 0x07,
        LuminosityUpdate = 0x08
    }


    class Program
    {
        
        


        public static float Map(float original, float fromLo, float fromHi, float toLow, float toHigh)
        {
            return (original - fromLo)*((toHigh - toLow)/(fromHi - fromLo)) + toLow;
        }
        //data + time
        private static int _bmpSize = 16; //yep
        private static int _timeSyncSize = 11; //unused for demosat
        private static int _custMagSize = 18438; //yep
        private static int _bnoSize = 61; //yep
        private static int _eMagSize = 19; //yep
        private static int _batteryUpdateSize = 13;
        private static int _luminosityUpdateSize = 7;
        private static int _heaterUpdateSize = 7;

        private static void Main(string[] args)
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
            using (var tempBno = new FileStream("heater.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),temp,heaterTemp\n");
                tempBno.Write(header, 0, header.Length);
            }
            using (var tempBno = new FileStream("luminosity.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),luminosity\n");
                tempBno.Write(header, 0, header.Length);
            }
            using (var tempBno = new FileStream("battery.csv", FileMode.Create))
            {
                var header = Encoding.UTF8.GetBytes("time(millis),voltage,percent\n");
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
                    var packetT = (PacketType) packetType;

                    var dataSize = 0;
                    switch (packetT) {
                        case PacketType.CustomMagnetometer:
                            dataSize = _custMagSize;
                            break;
                        case PacketType.BMPSensorUpdate:
                            dataSize = _bmpSize;
                            break;
                        case PacketType.BNOSensorUpdate:
                            dataSize = _bnoSize;
                            break;
                        case PacketType.TimeUpdate:
                            dataSize = _timeSyncSize;
                            break;
                        case PacketType.DebugMessage:
                            var sizeMsb = fileStream.ReadByte();
                            var sizeLsb = fileStream.ReadByte();
                            dataSize = (sizeMsb << 8) + sizeLsb;
                            break;
                        case PacketType.ExpensiveMagUpdate:
                            dataSize = _eMagSize;
                            break;
                        case PacketType.BatteryUpdate:
                            dataSize = _batteryUpdateSize;
                            break;
                        case PacketType.HeaterUpdate:
                            dataSize = _heaterUpdateSize;
                            break;
                        case PacketType.LuminosityUpdate:
                            dataSize = _luminosityUpdateSize;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    //switch (packetType) {
                    //    case 0x00:
                    //        dataSize = _custMagSize;
                    //        break;
                    //    case 0x01:
                    //        dataSize = _bmpSize;
                    //        break;
                    //    case 0x02:
                    //        dataSize = _bnoSize;
                    //        break;
                    //    case 0x03:
                    //        dataSize = _timeSyncSize;
                    //        break;
                    //    case 0x04:
                    //        var sizeMsb = fileStream.ReadByte();
                    //        var sizeLsb = fileStream.ReadByte();
                    //        dataSize = (sizeMsb << 8) + sizeLsb;
                    //        break;
                    //    case 0x05:
                    //        dataSize = _eMagSize;
                    //        break;
                    //}
                    //3rd and 4th are size
                    //var sizeMsb = fileStream.ReadByte();
                    //var sizeLsb = fileStream.ReadByte();
                    //var dataSize = (sizeMsb << 8) + sizeLsb;

                    var dataContainer = new byte[dataSize];

                    for (var i = 0; i < dataSize; i++) {
                        dataContainer[i] = (byte) fileStream.ReadByte();
                    }

                    ParseData(packetT, dataSize, dataContainer);
                }
            }

            Console.WriteLine("Finished! ");
            Console.ReadKey();
        }

        private static void ParseData(PacketType packetType, int dataSizewithTimeStamps, byte[] data) {
            //var startMillisBinary = new[] {data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7] };
            //var startMillis = BitConverter.ToInt64(startMillisBinary,0);

            var newStartMillisBinary = new byte[] {data[0], data[1], data[2], 0, 0, 0, 0, 0};
            var startMillis = BitConverter.ToInt64(newStartMillisBinary, 0);

            var startIndex = 3; //3 is where data starts... 0,1,2 have time data.
            switch (packetType) {
                case PacketType.BNOSensorUpdate: //BNO
                    var current = startIndex;
                    Console.Write("BNO packet found. Size: + " + data.Length + ". Processing...");
                    using (var logFile = new FileStream("bno.csv", FileMode.Append)) {
                        //0 time 1 time 2 time 3 data
                        var accelxNeg = data[current++] == 1 ? -1 : 1;
                        var accelvecX = ((data[current++] << 8) + data[current++])*accelxNeg;
                        var accelyNeg = data[current++] == 1 ? -1 : 1;
                        var accelvecY = (data[current++] << 8) + data[current++]*accelyNeg;
                        var accelzNeg = data[current++] == 1 ? -1 : 1;
                        var accelvecZ = (data[current++] << 8) + data[current++]*accelzNeg;
                        //0 time 1 time 2 time 3 data
                        var gravxNeg = data[current++] == 1 ? -1 : 1;
                        var gravvecX = ((data[current++] << 8) + data[current++])*gravxNeg;
                        var gravyNeg = data[current++] == 1 ? -1 : 1;
                        var gravvecY = (data[current++] << 8) + data[current++]*gravyNeg;
                        var gravzNeg = data[current++] == 1 ? -1 : 1;
                        var gravvecZ = (data[current++] << 8) + data[current++]*gravzNeg;
                        //0 time 1 time 2 time 3 data
                        var linearxNeg = data[current++] == 1 ? -1 : 1;
                        var linearvecX = ((data[current++] << 8) + data[current++])*linearxNeg;
                        var linearyNeg = data[current++] == 1 ? -1 : 1;
                        var linearvecY = (data[current++] << 8) + data[current++]*linearyNeg;
                        var linearzNeg = data[current++] == 1 ? -1 : 1;
                        var linearvecZ = (data[current++] << 8) + data[current++]*linearzNeg;
                        //0 time 1 time 2 time 3 data
                        var gyroxNeg = data[current++] == 1 ? -1 : 1;
                        var gyrovecX = ((data[current++] << 8) + data[current++])*gyroxNeg;
                        var gyroyNeg = data[current++] == 1 ? -1 : 1;
                        var gyrovecY = (data[current++] << 8) + data[current++]*gyroyNeg;
                        var gyrozNeg = data[current++] == 1 ? -1 : 1;
                        var gyrovecZ = (data[current++] << 8) + data[current++]*gyrozNeg;
                        //0 time 1 time 2 time 3 data
                        var magxNeg = data[current++] == 1 ? -1 : 1;
                        var magvecX = ((data[current++] << 8) + data[current++])*magxNeg;
                        var magyNeg = data[current++] == 1 ? -1 : 1;
                        var magvecY = (data[current++] << 8) + data[current++]*magyNeg;
                        var magzNeg = data[current++] == 1 ? -1 : 1;
                        var magvecZ = (data[current++] << 8) + data[current++]*magzNeg;
                        //0 time 1 time 2 time 3 data
                        var eulerxNeg = data[current++] == 1 ? -1 : 1;
                        var eulervecX = ((data[current++] << 8) + data[current++])*eulerxNeg;
                        var euleryNeg = data[current++] == 1 ? -1 : 1;
                        var eulervecY = (data[current++] << 8) + data[current++]*euleryNeg;
                        var eulerzNeg = data[current++] == 1 ? -1 : 1;
                        var eulervecZ = (data[current++] << 8) + data[current++]*eulerzNeg;

                        var sysCalib = data[current++];
                        var gyrCalib = data[current++];
                        var accCalib = data[current++];
                        var magCalib = data[current];

                        var currentline = startMillis + "," + accelvecX/100f + "," + accelvecY/100f + "," + accelvecZ/100f  + "," + gravvecX/100f + "," + gravvecY/100f + "," + gravvecZ/100f + "," + linearvecX/100f + "," + linearvecY/100f + "," + linearvecZ/100f + "," + gyrovecX/100f + "," + gyrovecY/100f + "," + gyrovecZ/100f + "," + magvecX/100f + "," + magvecY/100f + "," + magvecZ/100f + "," + eulervecX/100f + "," + eulervecY/100f + "," + eulervecZ/100f + "," + sysCalib + "," + gyrCalib + "," + accCalib + "," + magCalib + "\n";

                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.CustomMagnetometer: //Custom Mag Dump
                    Console.Write("Custom Mag packet found. Size: " + data.Length + ". Processing...");
                    var magRawEndTime = new byte[] {data[data.Length - 3], data[data.Length - 2], data[data.Length - 1], 0, 0, 0, 0, 0};
                    var magEndTime = BitConverter.ToInt64(magRawEndTime, 0);
                    var magDelta = Math.Abs(magEndTime - startMillis);

                    var magTimeStampSize = 6;
                    var magDataSize = dataSizewithTimeStamps - magTimeStampSize;
                    double magStep = magDelta/((float) magDataSize/2); // 2bytes each vector, 1 vectors
                    double currentMagStep = startMillis;
                    using (var logFile = new FileStream("cmag.csv", FileMode.Append)) {
                        for (var i = startIndex; i < dataSizewithTimeStamps - startIndex; i += 2) {
                            var magVec = ((data[i] << 8) + data[i + 1]);
                            var currentline = currentMagStep + "," + magVec + "\n"; //currentMagStep holds time
                            logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                            currentMagStep += magStep;
                        }
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.BMPSensorUpdate: //BMP Dump
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
                case PacketType.TimeUpdate: // Time-sync
                    Console.Write("Time-sync packet found. Processing...");
                    using (var logFile = new FileStream("timeSync.csv", FileMode.Append)) {
                        var hours = data[0];
                        var minutes = data[1];
                        var seconds = data[2];

                        var millis = new byte[] {data[3], data[4], data[5], 0, 0, 0, 0, 0};

                        var currentline = hours + ":" + minutes + ":" + seconds + "," + BitConverter.ToInt64(millis, 0) + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.DebugMessage: // Debug
                    Console.Write("Debug message found. Processing...");
                    using (var logFile = new FileStream("debug.csv", FileMode.Append)) {
                        var size = dataSizewithTimeStamps - 3;
                        var millis = new byte[] {data[0], data[1], data[2], 0, 0, 0, 0, 0};
                        var message = new byte[size];
                        for (int i = 0; i < size; i++) {
                            message[i] = data[i + 3];
                        }

                        var currentline = BitConverter.ToInt64(millis, 0) + "," + Encoding.UTF8.GetString(message) + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentline), 0, currentline.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.ExpensiveMagUpdate: // Debug
                    Console.Write("Expensive Mag Packet found. Processing...");
                    using (var logFile = new FileStream("emag.csv", FileMode.Append)) {
                        var size = dataSizewithTimeStamps - 3; //should be 16!

                        //data[0] = echo... data[1] = SOT
                        //data[2] <<8 + data[3] = MX
                        var mX = ((short) ((data[3] << 8) + data[4]))/1000.0;
                        var mY = ((short) ((data[5] << 8) + data[6]))/1000.0;
                        var mZ = ((short) ((data[7] << 8) + data[8]))/1000.0;

                        var temp = ((short) ((data[9] << 8) + data[10]))/100.0;

                        //data 10-11 ANA1, 12 status, 13 checksum, 14-15 EOT

                        var currentLine = startMillis + "," + mX + "," + mY + "," + mZ + "," + temp + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentLine), 0, currentLine.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.BatteryUpdate:
                    Console.Write("Battery Packet found... Size: " + data.Length + ". Processing...");
                    using (var logFile = new FileStream("battery.csv", FileMode.Append))
                    {
                        var voltage = new byte[8];
                        current = startIndex;

                        for (int i = 0; i < voltage.Length; i++)
                        {
                            voltage[i] = data[current++];
                        }
                        var actualVoltage = BitConverter.ToDouble(voltage, 0);
                        var percent = ((data[current++] << 8) + data[current]);

                        var currentLine = startMillis + "," + actualVoltage + "," + percent + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentLine), 0, currentLine.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.HeaterUpdate:
                    Console.Write("Heater Packet found... Size: " + data.Length + ". Processing...");
                    using (var logFile = new FileStream("heater.csv", FileMode.Append))
                    {
                        
                        current = startIndex;

                        var temp = ((data[current++] << 8) + data[current++]);
                        var heaterTemp = ((data[current++] << 8) + data[current]);
                        
                        var currentLine = startMillis + "," + temp + "," + heaterTemp + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentLine), 0, currentLine.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                case PacketType.LuminosityUpdate:
                    Console.Write("Luminosity Packet found... Size: " + data.Length + ". Processing...");
                    using (var logFile = new FileStream("luminosity.csv", FileMode.Append))
                    {
                        var luminosity = new byte[4];
                        current = startIndex;

                        for (int i = 0; i < luminosity.Length; i++) {
                            luminosity[i] = data[current++];
                        }
                        var actualLuminosity = BitConverter.ToUInt32(luminosity, 0);

                        var currentLine = startMillis + "," + actualLuminosity + "\n";
                        logFile.Write(Encoding.UTF8.GetBytes(currentLine), 0, currentLine.Length);
                    }
                    Console.WriteLine(" finished.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(packetType), packetType, null);
            }
        }
    }
}
